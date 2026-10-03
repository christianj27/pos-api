using Microsoft.EntityFrameworkCore;
using Pos.Api.Data;
using Pos.Api.Data.Enums;
using Pos.Api.DTOs.Customers;
using Pos.Api.Models;
using Pos.Api.Services.Interfaces;

namespace Pos.Api.Services.Implementations;

public class CustomerService(AppDbContext db) : ICustomerService
{
    public async Task<IEnumerable<CustomerResponse>> GetAllAsync(bool activeOnly = true, string? userRole = null)
    {
        var q = db.Customers.AsQueryable();
        if (userRole != "owner") q = q.Where(c => !c.IsConfidential);
        if (activeOnly) q = q.Where(c => c.IsActive);

        var customers = await q.OrderBy(c => c.IsActive ? 0 : 1).ThenBy(c => c.Name).ToListAsync();
        var customerIds = customers.Select(c => c.Id).ToList();

        var totalDebts = await db.Transactions
            .Where(t => customerIds.Contains(t.CustomerId!.Value) && t.Status != Data.Enums.TransactionStatus.Cancelled)
            .GroupBy(t => t.CustomerId!.Value)
            .Select(g => new { CustomerId = g.Key, Total = g.Sum(t => t.DebtAmount) })
            .ToDictionaryAsync(x => x.CustomerId, x => x.Total);

        var totalPaid = await db.DebtPayments
            .Include(dp => dp.Transaction)
            .Where(dp => customerIds.Contains(dp.CustomerId) && (dp.Transaction == null || dp.Transaction.Status != Data.Enums.TransactionStatus.Cancelled))
            .GroupBy(dp => dp.CustomerId)
            .Select(g => new { CustomerId = g.Key, Total = g.Sum(dp => dp.Amount) })
            .ToDictionaryAsync(x => x.CustomerId, x => x.Total);

        return customers.Select(c =>
        {
            var debt = totalDebts.GetValueOrDefault(c.Id, 0m);
            var paid = totalPaid.GetValueOrDefault(c.Id, 0m);
            return MapToResponse(c, c.InitialDebt + debt - paid);
        });
    }

    public async Task<CustomerResponse?> GetByIdAsync(Guid id)
    {
        var c = await db.Customers.FindAsync(id);
        if (c is null) return null;

        var totalDebt = await db.Transactions
            .Where(t => t.CustomerId == id && t.Status != Data.Enums.TransactionStatus.Cancelled)
            .SumAsync(t => t.DebtAmount);
        var totalPaid = await db.DebtPayments
            .Include(dp => dp.Transaction)
            .Where(dp => dp.CustomerId == id && (dp.Transaction == null || dp.Transaction.Status != Data.Enums.TransactionStatus.Cancelled))
            .SumAsync(dp => dp.Amount);

        return MapToResponse(c, c.InitialDebt + totalDebt - totalPaid);
    }

    public async Task<(CustomerResponse? Customer, string? Error)> CreateAsync(CreateCustomerRequest request)
    {
        if (request.InitialDebt.HasValue && request.InitialDebt.Value < 0)
            return (null, "Saldo awal hutang tidak boleh negatif.");

        var customer = new Customer
        {
            Name = request.Name.Trim(),
            Phone = request.Phone?.Trim(),
            Address = request.Address?.Trim(),
            InitialDebt = request.InitialDebt ?? 0m,
            IsConfidential = request.IsConfidential ?? false
        };
        db.Customers.Add(customer);
        await db.SaveChangesAsync();
        return (MapToResponse(customer, customer.InitialDebt), null);
    }

    public async Task<(CustomerResponse? Customer, string? Error)> UpdateAsync(Guid id, UpdateCustomerRequest request)
    {
        var customer = await db.Customers.FindAsync(id);
        if (customer is null) return (null, "Customer not found.");

        if (request.InitialDebt.HasValue && request.InitialDebt.Value < 0)
            return (null, "Saldo awal hutang tidak boleh negatif.");

        customer.Name = request.Name.Trim();
        customer.Phone = request.Phone?.Trim();
        customer.Address = request.Address?.Trim();
        customer.IsActive = request.IsActive;
        if (request.InitialDebt.HasValue)
            customer.InitialDebt = request.InitialDebt.Value;
        if (request.IsConfidential.HasValue)
            customer.IsConfidential = request.IsConfidential.Value;
        await db.SaveChangesAsync();

        var totalDebt = await db.Transactions
            .Where(t => t.CustomerId == id && t.Status != Data.Enums.TransactionStatus.Cancelled)
            .SumAsync(t => t.DebtAmount);
        var totalPaid = await db.DebtPayments
            .Include(dp => dp.Transaction)
            .Where(dp => dp.CustomerId == id && (dp.Transaction == null || dp.Transaction.Status != Data.Enums.TransactionStatus.Cancelled))
            .SumAsync(dp => dp.Amount);

        return (MapToResponse(customer, customer.InitialDebt + totalDebt - totalPaid), null);
    }

    public async Task<(bool Success, string? Error)> DeleteAsync(Guid id)
    {
        var customer = await db.Customers.FindAsync(id);
        if (customer is null) return (false, "Customer not found.");

        // Soft delete: the row is retained so historical transactions, debt history,
        // container loans and customer pricing remain intact.
        customer.IsActive = false;
        await db.SaveChangesAsync();
        return (true, null);
    }

    public async Task<CustomerPricingResponse?> GetPricingAsync(Guid customerId)
    {
        var customer = await db.Customers.FindAsync(customerId);
        if (customer is null) return null;

        var products = await db.Products.Where(p => p.IsActive).ToListAsync();
        var pricings = await db.CustomerPricings
            .Where(cp => cp.CustomerId == customerId)
            .ToDictionaryAsync(cp => cp.ProductId, cp => cp.CustomPrice);

        var items = products.Select(p => new CustomerPricingItemResponse(
            p.Id, p.Name, p.Unit, p.BasePrice,
            pricings.TryGetValue(p.Id, out var cp) ? cp : null));

        return new CustomerPricingResponse(customerId, customer.Name, items);
    }

    public async Task<(bool Success, string? Error)> UpdatePricingAsync(Guid customerId, UpdateCustomerPricingRequest request)
    {
        var customer = await db.Customers.FindAsync(customerId);
        if (customer is null) return (false, "Customer not found.");

        var existing = await db.CustomerPricings
            .Where(cp => cp.CustomerId == customerId)
            .ToListAsync();
        db.CustomerPricings.RemoveRange(existing);

        foreach (var item in request.Items)
        {
            if (item.CustomPrice.HasValue && item.CustomPrice.Value > 0)
            {
                db.CustomerPricings.Add(new CustomerPricing
                {
                    CustomerId = customerId,
                    ProductId = item.ProductId,
                    CustomPrice = item.CustomPrice.Value
                });
            }
        }

        await db.SaveChangesAsync();
        return (true, null);
    }

    public async Task<CustomerDebtSummaryResponse?> GetDebtAsync(Guid customerId)
    {
        var customer = await db.Customers.FindAsync(customerId);
        if (customer is null) return null;

        var totalDebt = await db.Transactions
            .Where(t => t.CustomerId == customerId && t.Status != Data.Enums.TransactionStatus.Cancelled)
            .SumAsync(t => t.DebtAmount);

        var totalPaid = await db.DebtPayments
            .Include(dp => dp.Transaction)
            .Where(dp => dp.CustomerId == customerId && (dp.Transaction == null || dp.Transaction.Status != Data.Enums.TransactionStatus.Cancelled))
            .SumAsync(dp => dp.Amount);

        return new CustomerDebtSummaryResponse(customerId, customer.Name, customer.InitialDebt + totalDebt - totalPaid);
    }

    public async Task<ContainerLoanSummaryResponse?> GetContainerLoansAsync(Guid customerId)
    {
        var customer = await db.Customers.FindAsync(customerId);
        if (customer is null) return null;

        var loans = await db.ContainerLoans
            .Include(cl => cl.Product)
            .Where(cl => cl.CustomerId == customerId)
            .GroupBy(cl => new { cl.ProductId, cl.Product.Name, cl.Product.Unit })
            .Select(g => new ContainerLoanNetItem(g.Key.ProductId, g.Key.Name, g.Key.Unit, g.Sum(cl => cl.Quantity)))
            .Where(item => item.NetQuantity != 0)
            .ToListAsync();

        return new ContainerLoanSummaryResponse(customerId, customer.Name, loans);
    }

    public async Task<CustomerDebtHistoryResponse?> GetDebtHistoryAsync(Guid customerId)
    {
        var customer = await db.Customers.FindAsync(customerId);
        if (customer is null) return null;

        var totalDebt = await db.Transactions
            .Where(t => t.CustomerId == customerId && t.Status != Data.Enums.TransactionStatus.Cancelled)
            .SumAsync(t => t.DebtAmount);
        var totalPaid = await db.DebtPayments
            .Include(dp => dp.Transaction)
            .Where(dp => dp.CustomerId == customerId && (dp.Transaction == null || dp.Transaction.Status != Data.Enums.TransactionStatus.Cancelled))
            .SumAsync(dp => dp.Amount);

        var debtTxns = await db.Transactions
            .Include(t => t.Staff)
            .Where(t => t.CustomerId == customerId
                        && t.DebtAmount > 0
                        && t.Status != Data.Enums.TransactionStatus.Cancelled)
            .OrderByDescending(t => t.CreatedAt)
            .Select(t => new DebtTransactionItem(
                t.Id, t.CreatedAt, t.TransactionType.ToString().ToLower(),
                t.TotalAmount, t.PaidAmount, t.DebtAmount, t.Staff.Name))
            .ToListAsync();

        var payments = await db.DebtPayments
            .Include(dp => dp.Creator)
            .Include(dp => dp.Transaction)
            .Where(dp => dp.CustomerId == customerId && (dp.Transaction == null || dp.Transaction.Status != Data.Enums.TransactionStatus.Cancelled))
            .OrderByDescending(dp => dp.CreatedAt)
            .Select(dp => new DebtPaymentHistoryItem(dp.Id, dp.Amount, dp.Note, dp.Creator.Name, dp.CreatedAt))
            .ToListAsync();

        return new CustomerDebtHistoryResponse(customerId, customer.Name, customer.InitialDebt, customer.InitialDebt + totalDebt - totalPaid, debtTxns, payments);
    }

    /// <summary>
    /// FR-CST-011 "Pergerakan Stok" per customer — per-product totals for an inclusive WIB date range.
    /// Only movements whose transaction belongs to <paramref name="customerId"/> are counted, because
    /// <c>StockMovement</c> has no customer column of its own.
    /// Terjual:      dispatch movements. Refillable = filled container qty; simple = all dispatch qty.
    /// Dikembalikan: inbound empty-container movements (the Step-3 container return written by
    ///               <c>TransactionService</c>) — a customer only ever hands back empty containers.
    /// Cancelled movements (is_reversed / is_reversal) are excluded; zero-activity products are omitted.
    /// </summary>
    public async Task<CustomerStockSummaryResponse?> GetStockSummaryAsync(
        Guid customerId, string period, DateOnly rangeStart, DateOnly rangeEnd)
    {
        var customer = await db.Customers.FindAsync(customerId);
        if (customer is null) return null;

        var (start, _) = WibTimeZone.GetUtcDayBounds(rangeStart);
        var (_, end) = WibTimeZone.GetUtcDayBounds(rangeEnd);

        var movements = await db.StockMovements
            .Include(m => m.Product)
            .Include(m => m.Transaction)
            .Include(m => m.Creator)
            .Where(m => m.Transaction != null
                        && m.Transaction.CustomerId == customerId
                        && m.CreatedAt >= start && m.CreatedAt < end
                        && !m.IsReversed && !m.IsReversal)
            .ToListAsync();

        var items = movements
            .GroupBy(m => new { m.ProductId, m.Product.Name, m.Product.Unit, m.Product.Category })
            .Select(pg =>
            {
                var isRefillable = pg.Key.Category == ProductCategory.Refillable;

                var totalSold = isRefillable
                    ? pg.Where(m => m.MovementType == MovementType.Dispatch
                                 && m.ContainerStatus == ContainerStatus.Filled)
                        .Sum(m => m.Quantity)
                    : pg.Where(m => m.MovementType == MovementType.Dispatch)
                        .Sum(m => m.Quantity);

                var totalReturned = pg.Where(m => m.MovementType == MovementType.Receive
                                               && m.ContainerStatus == ContainerStatus.Empty)
                                      .Sum(m => m.Quantity);

                var staff = pg.GroupBy(m => new { m.CreatedBy, m.Creator.Name })
                    .Select(sg =>
                    {
                        var staffSold = isRefillable
                            ? sg.Where(m => m.MovementType == MovementType.Dispatch
                                         && m.ContainerStatus == ContainerStatus.Filled)
                                .Sum(m => m.Quantity)
                            : sg.Where(m => m.MovementType == MovementType.Dispatch)
                                .Sum(m => m.Quantity);

                        var staffReturned = sg.Where(m => m.MovementType == MovementType.Receive
                                                       && m.ContainerStatus == ContainerStatus.Empty)
                                              .Sum(m => m.Quantity);

                        return new CustomerStockStaffItem(sg.Key.CreatedBy, sg.Key.Name, staffSold, staffReturned);
                    })
                    .Where(s => s.Sold > 0 || s.Returned > 0)
                    .OrderByDescending(s => s.Sold)
                    .ThenBy(s => s.StaffName)
                    .ToList();

                return new CustomerStockProductItem(
                    pg.Key.ProductId,
                    pg.Key.Name,
                    pg.Key.Unit,
                    pg.Key.Category.ToString().ToLower(),
                    totalSold,
                    totalReturned,
                    staff);
            })
            .Where(s => s.TotalSold > 0 || s.TotalReturned > 0)
            .OrderBy(s => s.ProductName)
            .ToList();

        return new CustomerStockSummaryResponse(
            customerId,
            customer.Name,
            period,
            rangeStart.ToString("yyyy-MM-dd"),
            rangeEnd.ToString("yyyy-MM-dd"),
            items);
    }

    private static CustomerResponse MapToResponse(Customer c, decimal outstandingDebt) =>
        new(c.Id, c.Name, c.Phone, c.Address, c.IsActive, c.IsConfidential, c.CreatedAt, outstandingDebt);
}
