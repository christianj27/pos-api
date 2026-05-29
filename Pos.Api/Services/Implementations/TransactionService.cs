using Microsoft.EntityFrameworkCore;
using Pos.Api.Data;
using Pos.Api.Data.Enums;
using Pos.Api.DTOs.Transactions;
using Pos.Api.Models;
using Pos.Api.Services.Interfaces;

namespace Pos.Api.Services.Implementations;

public class TransactionService(AppDbContext db) : ITransactionService
{
    public async Task<IEnumerable<TransactionResponse>> GetAllAsync(Guid userId, string role, DateOnly? date)
    {
        var filter = date ?? WibTimeZone.TodayWib();
        var (start, end) = WibTimeZone.GetUtcDayBounds(filter);

        var q = db.Transactions
            .Include(t => t.Customer)
            .Include(t => t.Staff)
            .Include(t => t.Location)
            .Include(t => t.Items).ThenInclude(i => i.Product)
            .Where(t => t.CreatedAt >= start && t.CreatedAt < end);

        if (role is "kurir" or "kasir")
            q = q.Where(t => t.StaffId == userId);

        return await q
            .OrderByDescending(t => t.CreatedAt)
            .Select(t => MapToResponse(t))
            .ToListAsync();
    }

    public async Task<TransactionDetailResponse?> GetByIdAsync(Guid id, Guid userId, string role)
    {
        var t = await db.Transactions
            .Include(t => t.Customer)
            .Include(t => t.Staff)
            .Include(t => t.Location)
            .Include(t => t.Items).ThenInclude(i => i.Product)
            .Include(t => t.Payments)
            .Include(t => t.ContainerLoans).ThenInclude(cl => cl.Product)
            .FirstOrDefaultAsync(t => t.Id == id);

        if (t is null) return null;
        if (role is "kurir" or "kasir" && t.StaffId != userId) return null;

        var containerReturns = t.ContainerLoans
            .Where(cl => cl.Quantity < 0)
            .Select(cl => new ContainerReturnDetail(cl.ProductId, cl.Product.Name, Math.Abs(cl.Quantity)));

        return new TransactionDetailResponse(
            t.Id, t.TransactionType.ToString().ToLower(), t.Status.ToString().ToLower(),
            t.CustomerId, t.Customer?.Name, t.StaffId, t.Staff.Name,
            t.LocationId, t.Location.Name, t.PaymentMethod.ToString().ToLower(),
            t.Notes, t.CreatedAt,
            t.Items.Select(i => MapItemToResponse(i)),
            t.TotalAmount, t.PaidAmount, t.DebtAmount,
            t.Payments.Select(p => new PaymentDetailResponse(p.Id, p.Amount, p.Method.ToString().ToLower(), p.ReferenceNo, p.PaidAt)),
            containerReturns);
    }

    public async Task<(TransactionDetailResponse? Transaction, string? Error)> CreateAsync(
        CreateTransactionRequest request, Guid staffId, string role)
    {
        if (!Enum.TryParse<TransactionType>(request.TransactionType, ignoreCase: true, out var txType))
            return (null, "Tipe transaksi tidak valid.");

        if (!Enum.TryParse<PaymentMethod>(request.PaymentMethod, ignoreCase: true, out var payMethod))
            return (null, "Metode pembayaran wajib dipilih.");

        if (!request.Items.Any())
            return (null, "Tambahkan minimal satu produk.");

        // Role enforcement
        if (role == "kurir" && txType != TransactionType.Delivery)
            return (null, "Tipe transaksi tidak valid untuk peran Anda.");
        if (role == "kasir" && txType != TransactionType.Counter)
            return (null, "Tipe transaksi tidak valid untuk peran Anda.");

        var totalAmount = request.Items.Sum(i => i.Quantity * i.UnitPrice);

        if (request.PaidAmount < 0 || request.PaidAmount > totalAmount)
            return (null, "Jumlah bayar tidak boleh melebihi total transaksi.");

        await using var dbTx = await db.Database.BeginTransactionAsync();
        try
        {
            var transaction = new Transaction
            {
                TransactionType = txType,
                CustomerId = request.CustomerId,
                StaffId = staffId,
                LocationId = request.LocationId,
                Status = TransactionStatus.Completed,
                PaymentMethod = payMethod,
                TotalAmount = totalAmount,
                PaidAmount = request.PaidAmount,
                DebtAmount = totalAmount - request.PaidAmount,
                Notes = request.Notes,
                CompletedAt = DateTime.UtcNow
            };
            db.Transactions.Add(transaction);
            await db.SaveChangesAsync();

            // Items + dispatch stock movements
            foreach (var item in request.Items)
            {
                db.TransactionItems.Add(new TransactionItem
                {
                    TransactionId = transaction.Id,
                    ProductId = item.ProductId,
                    Quantity = item.Quantity,
                    UnitPrice = item.UnitPrice
                });

                var product = await db.Products.FindAsync(item.ProductId);
                var containerStatus = product?.Category == ProductCategory.Refillable
                    ? ContainerStatus.Filled
                    : ContainerStatus.Na;

                db.StockMovements.Add(new StockMovement
                {
                    ProductId = item.ProductId,
                    MovementType = MovementType.Dispatch,
                    ContainerStatus = containerStatus,
                    Quantity = item.Quantity,
                    FromLocationId = request.LocationId,
                    TransactionId = transaction.Id,
                    CreatedBy = staffId
                });

                // Container loan if refillable and customer selected
                if (product?.Category == ProductCategory.Refillable && request.CustomerId.HasValue)
                {
                    db.ContainerLoans.Add(new ContainerLoan
                    {
                        TransactionId = transaction.Id,
                        CustomerId = request.CustomerId.Value,
                        ProductId = item.ProductId,
                        Quantity = item.Quantity,
                        CreatedBy = staffId
                    });
                }
            }

            // Payment record
            if (request.PaidAmount > 0)
            {
                db.Payments.Add(new Payment
                {
                    TransactionId = transaction.Id,
                    Amount = request.PaidAmount,
                    Method = payMethod
                });
            }

            // Container returns
            if (request.ContainerReturns is not null && request.CustomerId.HasValue)
            {
                foreach (var ret in request.ContainerReturns.Where(r => r.Quantity > 0))
                {
                    db.ContainerLoans.Add(new ContainerLoan
                    {
                        TransactionId = transaction.Id,
                        CustomerId = request.CustomerId.Value,
                        ProductId = ret.ProductId,
                        Quantity = -ret.Quantity,
                        CreatedBy = staffId
                    });

                    db.StockMovements.Add(new StockMovement
                    {
                        ProductId = ret.ProductId,
                        MovementType = MovementType.Receive,
                        ContainerStatus = ContainerStatus.Empty,
                        Quantity = ret.Quantity,
                        ToLocationId = request.LocationId,
                        TransactionId = transaction.Id,
                        CreatedBy = staffId
                    });
                }
            }

            // Debt payment
            if (request.DebtPaymentAmount.HasValue && request.DebtPaymentAmount > 0 && request.CustomerId.HasValue)
            {
                db.DebtPayments.Add(new DebtPayment
                {
                    CustomerId = request.CustomerId.Value,
                    Amount = request.DebtPaymentAmount.Value,
                    Method = payMethod,
                    CreatedBy = staffId
                });
            }

            await db.SaveChangesAsync();
            await dbTx.CommitAsync();

            var detail = await GetByIdAsync(transaction.Id, staffId, "owner");
            return (detail, null);
        }
        catch
        {
            await dbTx.RollbackAsync();
            throw;
        }
    }

    public async Task<(bool Success, string? Error)> UpdateStatusAsync(
        Guid id, UpdateTransactionStatusRequest request, Guid userId, string role)
    {
        if (!Enum.TryParse<TransactionStatus>(request.Status, ignoreCase: true, out var newStatus) ||
            newStatus != TransactionStatus.Cancelled)
            return (false, "Status yang valid hanya 'cancelled'.");

        var transaction = await db.Transactions
            .Include(t => t.Items).ThenInclude(i => i.Product)
            .Include(t => t.ContainerLoans)
            .FirstOrDefaultAsync(t => t.Id == id);

        if (transaction is null) return (false, "Transaction not found.");
        if (transaction.Status == TransactionStatus.Cancelled) return (false, "Transaksi sudah dibatalkan.");

        if (role is not "owner")
            return (false, "Hanya owner yang dapat membatalkan transaksi.");

        await using var dbTx = await db.Database.BeginTransactionAsync();
        try
        {
            transaction.Status = TransactionStatus.Cancelled;

            // Reverse dispatch movements ? receive back to source
            foreach (var item in transaction.Items)
            {
                var containerStatus = item.Product.Category == ProductCategory.Refillable
                    ? ContainerStatus.Filled
                    : ContainerStatus.Na;

                db.StockMovements.Add(new StockMovement
                {
                    ProductId = item.ProductId,
                    MovementType = MovementType.Receive,
                    ContainerStatus = containerStatus,
                    Quantity = item.Quantity,
                    ToLocationId = transaction.LocationId,
                    TransactionId = transaction.Id,
                    CreatedBy = userId
                });
            }

            // Reverse container loans (positive loans ? negative, container returns ? positive)
            foreach (var loan in transaction.ContainerLoans.ToList())
            {
                db.ContainerLoans.Add(new ContainerLoan
                {
                    TransactionId = transaction.Id,
                    CustomerId = loan.CustomerId,
                    ProductId = loan.ProductId,
                    Quantity = -loan.Quantity,
                    CreatedBy = userId
                });

                // If original was a return (negative), also reverse the empty stock movement
                if (loan.Quantity < 0)
                {
                    db.StockMovements.Add(new StockMovement
                    {
                        ProductId = loan.ProductId,
                        MovementType = MovementType.Dispatch,
                        ContainerStatus = ContainerStatus.Empty,
                        Quantity = Math.Abs(loan.Quantity),
                        FromLocationId = transaction.LocationId,
                        TransactionId = transaction.Id,
                        CreatedBy = userId
                    });
                }
            }

            await db.SaveChangesAsync();
            await dbTx.CommitAsync();
            return (true, null);
        }
        catch
        {
            await dbTx.RollbackAsync();
            throw;
        }
    }

    private static TransactionResponse MapToResponse(Transaction t) =>
        new(t.Id, t.TransactionType.ToString().ToLower(), t.CustomerId, t.Customer?.Name,
            t.StaffId, t.Staff.Name, t.LocationId, t.Location.Name,
            t.Status.ToString().ToLower(), t.PaymentMethod.ToString().ToLower(),
            t.TotalAmount, t.PaidAmount, t.DebtAmount, t.Notes, t.CreatedAt,
            t.Items.Select(i => MapItemToResponse(i)));

    private static TransactionItemResponse MapItemToResponse(TransactionItem i) =>
        new(i.Id, i.ProductId, i.Product.Name, i.Product.Unit,
            i.Quantity, i.UnitPrice, i.Quantity * i.UnitPrice);
}
