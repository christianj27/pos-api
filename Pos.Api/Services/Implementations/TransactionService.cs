using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Pos.Api.Data;
using Pos.Api.Data.Enums;
using Pos.Api.DTOs.Transactions;
using Pos.Api.Models;
using Pos.Api.Services.Interfaces;

namespace Pos.Api.Services.Implementations;

public class TransactionService(AppDbContext db, ISettlementGuard guard) : ITransactionService
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

        // Settlement gates (FR-STL-002): no new transaction while an earlier day is still unapproved,
        // and never write into a day the owner has already approved.
        var newWorkBlock = await guard.EnsureNewWorkAllowedAsync(staffId);
        if (newWorkBlock is not null) return (null, newWorkBlock);

        var businessDate = WibTimeZone.TodayWib();
        var dayLock = await guard.EnsureDayWritableAsync(staffId, businessDate);
        if (dayLock is not null) return (null, dayLock);

        await guard.TouchDayAsync(staffId, businessDate);

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
                    TransactionId = transaction.Id,
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

        // Gate B: an approved business date is frozen until the owner reopens it.
        var dayLock = await guard.EnsureDayWritableAsync(
            transaction.StaffId, WibTimeZone.ToWibDate(transaction.CreatedAt));
        if (dayLock is not null) return (false, dayLock);

        var stockMovements = await db.StockMovements
            .Where(sm => sm.TransactionId == id).ToListAsync();

        if (role is not "owner")
            return (false, "Hanya owner yang dapat membatalkan transaksi.");

        await using var dbTx = await db.Database.BeginTransactionAsync();
        try
        {
            transaction.Status = TransactionStatus.Cancelled;

            // Update stock movements to mark as reversed
            foreach (var sm in stockMovements)
            {
                sm.IsReversed = true;
                db.StockMovements.Update(sm);
            }

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
                    IsReversal = true,
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
                        IsReversal = true,
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

    /// <summary>
    /// Corrects an open-day transaction (FR-STL-008). The creator may fix their own transactions and the
    /// owner may fix any transaction, but only while the business date is not approved — that is the
    /// intended way for a user to make counted cash match the system's expectation.
    /// </summary>
    public async Task<(TransactionDetailResponse? Transaction, string? Error)> EditAsync(
        Guid id, EditTransactionRequest request, Guid userId, string role)
    {
        if (string.IsNullOrWhiteSpace(request.Reason))
            return (null, "Alasan perubahan wajib diisi.");

        var items = request.Items?.ToList() ?? [];
        if (items.Count == 0)
            return (null, "Tambahkan minimal satu produk.");

        if (!Enum.TryParse<PaymentMethod>(request.PaymentMethod, ignoreCase: true, out var payMethod))
            return (null, "Metode pembayaran wajib dipilih.");

        var transaction = await db.Transactions
            .Include(t => t.Items)
            .Include(t => t.ContainerLoans)
            .Include(t => t.Payments)
            .Include(t => t.StockMovements)
            .FirstOrDefaultAsync(t => t.Id == id);

        if (transaction is null) return (null, "Transaksi tidak ditemukan.");
        if (transaction.Status == TransactionStatus.Cancelled) return (null, "Transaksi sudah dibatalkan.");

        if (role is not "owner" && transaction.StaffId != userId)
            return (null, "Anda hanya dapat mengubah transaksi Anda sendiri.");

        var dayLock = await guard.EnsureDayWritableAsync(
            transaction.StaffId, WibTimeZone.ToWibDate(transaction.CreatedAt));
        if (dayLock is not null) return (null, dayLock);

        var newTotal = items.Sum(i => i.Quantity * i.UnitPrice);

        if (request.PaidAmount < 0 || request.PaidAmount > newTotal)
            return (null, "Jumlah bayar tidak boleh melebihi total transaksi.");

        // Instalments are the payment ledger's business: rewriting several of them here would silently
        // move cash between business dates. A single-payment transaction can be corrected in place.
        if (transaction.Payments.Count > 1 && request.PaidAmount != transaction.PaidAmount)
            return (null, "Transaksi ini memiliki beberapa pembayaran. Ubah jumlah bayar lewat pembayaran tambahan.");

        var changesJson = JsonSerializer.Serialize(new
        {
            before = new
            {
                totalAmount = transaction.TotalAmount,
                paidAmount = transaction.PaidAmount,
                paymentMethod = transaction.PaymentMethod.ToString().ToLower(),
                notes = transaction.Notes,
                items = transaction.Items.Select(i => new { i.ProductId, i.Quantity, i.UnitPrice })
            },
            after = new
            {
                totalAmount = newTotal,
                paidAmount = request.PaidAmount,
                paymentMethod = payMethod.ToString().ToLower(),
                notes = request.Notes,
                items = items.Select(i => new { i.ProductId, i.Quantity, i.UnitPrice })
            }
        });

        var products = await db.Products
            .Where(p => items.Select(i => i.ProductId).Contains(p.Id))
            .ToDictionaryAsync(p => p.Id);

        // Restate the dispatch movements. The on-hand balance is the sum of all movement rows, so the old
        // dispatches are compensated by an explicit return movement rather than merely flagged.
        foreach (var movement in transaction.StockMovements
                     .Where(m => m.MovementType == MovementType.Dispatch && !m.IsReversal)
                     .ToList())
        {
            movement.IsReversed = true;

            db.StockMovements.Add(new StockMovement
            {
                ProductId = movement.ProductId,
                MovementType = MovementType.Receive,
                ContainerStatus = movement.ContainerStatus,
                Quantity = movement.Quantity,
                ToLocationId = movement.FromLocationId,
                TransactionId = transaction.Id,
                IsReversal = true,
                CreatedBy = userId
            });
        }

        db.TransactionItems.RemoveRange(transaction.Items);

        // Container loans follow the items: the previous outgoing loans are reversed and re-created with
        // the new quantities. Container returns (negative loans) are not part of the edit.
        foreach (var loan in transaction.ContainerLoans.Where(l => !l.IsReversed && l.Quantity > 0).ToList())
            loan.IsReversed = true;

        foreach (var item in items)
        {
            products.TryGetValue(item.ProductId, out var product);
            var isRefillable = product?.Category == ProductCategory.Refillable;

            db.TransactionItems.Add(new TransactionItem
            {
                TransactionId = transaction.Id,
                ProductId = item.ProductId,
                Quantity = item.Quantity,
                UnitPrice = item.UnitPrice
            });

            db.StockMovements.Add(new StockMovement
            {
                ProductId = item.ProductId,
                MovementType = MovementType.Dispatch,
                ContainerStatus = isRefillable ? ContainerStatus.Filled : ContainerStatus.Na,
                Quantity = item.Quantity,
                FromLocationId = transaction.LocationId,
                TransactionId = transaction.Id,
                CreatedBy = userId
            });

            if (isRefillable && transaction.CustomerId.HasValue)
            {
                db.ContainerLoans.Add(new ContainerLoan
                {
                    TransactionId = transaction.Id,
                    CustomerId = transaction.CustomerId.Value,
                    ProductId = item.ProductId,
                    Quantity = item.Quantity,
                    CreatedBy = userId
                });
            }
        }

        // Keep the payment ledger in step without moving its collection timestamp (and therefore its
        // business date) — the original collector stays attributed.
        var payment = transaction.Payments.FirstOrDefault();

        if (request.PaidAmount == 0m)
        {
            if (payment is not null) db.Payments.Remove(payment);
        }
        else if (payment is null)
        {
            db.Payments.Add(new Payment
            {
                TransactionId = transaction.Id,
                Amount = request.PaidAmount,
                Method = payMethod,
                ReferenceNo = request.ReferenceNo,
                PaidAt = transaction.CreatedAt,
                CreatedBy = transaction.StaffId
            });
        }
        else
        {
            payment.Amount = request.PaidAmount;
            payment.Method = payMethod;
            payment.ReferenceNo = request.ReferenceNo;
        }

        transaction.TotalAmount = newTotal;
        transaction.PaidAmount = request.PaidAmount;
        transaction.DebtAmount = newTotal - request.PaidAmount;
        transaction.PaymentMethod = payMethod;
        transaction.Notes = request.Notes;

        db.AuditLogs.Add(new AuditLog
        {
            EntityType = "transaction",
            EntityId = transaction.Id,
            Action = "edit",
            Reason = request.Reason.Trim(),
            ChangesJson = changesJson,
            ActorId = userId
        });

        await db.SaveChangesAsync();

        var detail = await GetByIdAsync(transaction.Id, transaction.StaffId, "owner");
        return (detail, null);
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
