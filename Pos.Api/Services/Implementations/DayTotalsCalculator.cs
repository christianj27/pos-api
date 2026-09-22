using Microsoft.EntityFrameworkCore;
using Pos.Api.Data;
using Pos.Api.Data.Enums;

namespace Pos.Api.Services.Implementations;

public sealed record SettlementMethodTotal(PaymentMethod Method, decimal ExpectedAmount);

public sealed record SettlementDayTotals(
    IReadOnlyList<SettlementMethodTotal> Methods,
    decimal CashIn,
    decimal CashOut,
    decimal CashAdjustments,
    decimal ExpectedCash,
    decimal TransferExpected,
    decimal QrisExpected,
    decimal NewDebtTotal,
    decimal DebtPaymentTotal);

public sealed record SettlementStockTotal(
    Guid ProductId,
    bool IsRefillable,
    int ExpectedFilled,
    int ExpectedEmpty,
    int ContainersOut,
    int ContainersReturned);

/// <summary>
/// Single source of truth for "what happened on business date D for user U" (FR-STL-004/006).
/// Cash is attributed from the Payment ledger by collection time and collector, so an instalment
/// recorded today against an old invoice counts on today's settlement instead of rewriting history.
/// </summary>
public class DayTotalsCalculator(AppDbContext db)
{
    public async Task<SettlementDayTotals> GetDayTotalsAsync(Guid userId, DateOnly businessDate)
    {
        var (start, end) = WibTimeZone.GetUtcDayBounds(businessDate);

        // Payments collected by this user, dated by when the money arrived. Historical rows without a
        // collector fall back to the transaction's staff so old data still attributes correctly.
        var payments = await db.Payments
            .Include(p => p.Transaction)
            .Where(p => p.PaidAt >= start && p.PaidAt < end
                        && p.Transaction.Status != TransactionStatus.Cancelled
                        && (p.CreatedBy == userId || (p.CreatedBy == null && p.Transaction.StaffId == userId)))
            .Select(p => new { p.Amount, p.Method })
            .ToListAsync();

        var debtPayments = await db.DebtPayments
            .Include(dp => dp.Transaction)
            .Where(dp => dp.CreatedAt >= start && dp.CreatedAt < end
                         && dp.CreatedBy == userId
                         && (dp.Transaction == null || dp.Transaction.Status != TransactionStatus.Cancelled))
            .Select(dp => new { dp.Amount, dp.Method })
            .ToListAsync();

        // Cash out carries no payment method on the record, so it is assumed to have been paid in cash.
        var expenseTotal = await db.Expenses
            .Where(x => x.ExpenseDate == businessDate && x.CreatedBy == userId)
            .SumAsync(x => (decimal?)x.Amount) ?? 0m;

        var purchaseTotal = await db.StockMovements
            .Where(m => m.PurchaseCost > 0
                        && m.CreatedAt >= start && m.CreatedAt < end
                        && m.CreatedBy == userId && !m.IsReversed)
            .SumAsync(m => m.PurchaseCost) ?? 0m;

        var adjustments = await db.CashAdjustments
            .Where(a => a.UserId == userId && a.BusinessDate == businessDate)
            .SumAsync(a => (decimal?)a.Amount) ?? 0m;

        decimal CollectedBy(PaymentMethod method) =>
            payments.Where(p => p.Method == method).Sum(p => p.Amount)
            + debtPayments.Where(dp => dp.Method == method).Sum(dp => dp.Amount);

        var cashIn = CollectedBy(PaymentMethod.Cash);
        var transferIn = CollectedBy(PaymentMethod.Transfer);
        var qrisIn = CollectedBy(PaymentMethod.Qris);
        var cashOut = expenseTotal + purchaseTotal;
        var expectedCash = cashIn - cashOut + adjustments;

        // New customer debt created by this user's sales on the day.
        var newDebt = await db.Transactions
            .Where(t => t.StaffId == userId && t.CreatedAt >= start && t.CreatedAt < end
                        && t.Status == TransactionStatus.Completed && t.DebtAmount > 0)
            .SumAsync(t => (decimal?)t.DebtAmount) ?? 0m;

        var methods = new List<SettlementMethodTotal>
        {
            new(PaymentMethod.Cash, expectedCash),
            new(PaymentMethod.Transfer, transferIn),
            new(PaymentMethod.Qris, qrisIn)
        };

        return new SettlementDayTotals(
            methods,
            cashIn,
            cashOut,
            adjustments,
            expectedCash,
            transferIn,
            qrisIn,
            newDebt,
            debtPayments.Sum(dp => dp.Amount));
    }

    /// <summary>
    /// Expected on-truck stock for the user's vehicle plus the day's container-loan movement.
    /// The balance follows the same rule as <c>StockService.GetLevelsAsync</c>: reversals are
    /// compensating movements, so <c>IsReversed</c> rows are intentionally included.
    /// </summary>
    public async Task<IReadOnlyList<SettlementStockTotal>> GetStockTotalsAsync(
        Guid userId, Guid vehicleLocationId, DateOnly businessDate)
    {
        var (start, end) = WibTimeZone.GetUtcDayBounds(businessDate);

        var movements = await db.StockMovements
            .Where(m => m.CreatedAt < end
                        && (m.ToLocationId == vehicleLocationId || m.FromLocationId == vehicleLocationId))
            .Select(m => new { m.ProductId, m.ToLocationId, m.FromLocationId, m.ContainerStatus, m.Quantity })
            .ToListAsync();

        var loans = await db.ContainerLoans
            .Where(cl => cl.CreatedBy == userId && cl.CreatedAt >= start && cl.CreatedAt < end && !cl.IsReversed)
            .Select(cl => new { cl.ProductId, cl.Quantity })
            .ToListAsync();

        var categories = await db.Products
            .Where(p => p.IsActive)
            .Select(p => new { p.Id, p.Category })
            .ToDictionaryAsync(p => p.Id, p => p.Category);

        var productIds = movements.Select(m => m.ProductId)
            .Concat(loans.Select(l => l.ProductId))
            .Distinct()
            .ToList();

        var result = new List<SettlementStockTotal>();

        foreach (var productId in productIds)
        {
            if (!categories.TryGetValue(productId, out var category)) continue;
            var isRefillable = category == ProductCategory.Refillable;

            int Balance(ContainerStatus status) =>
                movements
                    .Where(m => m.ProductId == productId && m.ContainerStatus == status)
                    .Sum(m => (m.ToLocationId == vehicleLocationId ? m.Quantity : 0)
                              - (m.FromLocationId == vehicleLocationId ? m.Quantity : 0));

            var filled = isRefillable ? Balance(ContainerStatus.Filled) : Balance(ContainerStatus.Na);
            var empty = isRefillable ? Balance(ContainerStatus.Empty) : 0;

            var outQty = loans.Where(l => l.ProductId == productId && l.Quantity > 0).Sum(l => l.Quantity);
            var returnedQty = loans.Where(l => l.ProductId == productId && l.Quantity < 0).Sum(l => -l.Quantity);

            if (filled == 0 && empty == 0 && outQty == 0 && returnedQty == 0) continue;

            result.Add(new SettlementStockTotal(productId, isRefillable, filled, empty, outQty, returnedQty));
        }

        return result;
    }
}
