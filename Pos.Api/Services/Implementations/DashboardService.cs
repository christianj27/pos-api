using Microsoft.EntityFrameworkCore;
using Pos.Api.Data;
using Pos.Api.Data.Enums;
using Pos.Api.DTOs.Dashboard;
using Pos.Api.Models;
using Pos.Api.Services.Interfaces;

namespace Pos.Api.Services.Implementations;

public class DashboardService(AppDbContext db) : IDashboardService
{
    public async Task<DashboardResponse> GetDashboardAsync(DateOnly date, Guid userId, string role)
    {
        var isOwner = role == "owner";
        var (start, end) = WibTimeZone.GetUtcDayBounds(date);

        // Summary stats — scoped to userId for non-owners
        IQueryable<Transaction> dayTxBase = db.Transactions
            .Where(t => t.Status == TransactionStatus.Completed && t.CreatedAt >= start && t.CreatedAt < end);
        if (!isOwner) dayTxBase = dayTxBase.Where(t => t.StaffId == userId);

        var todayRevenue      = await dayTxBase.SumAsync(t => t.PaidAmount);
        var todayTransactions = await dayTxBase.CountAsync();

        IQueryable<StockMovement> dayMvBase = db.StockMovements
            .Where(m => m.PurchaseCost != null && m.CreatedAt >= start && m.CreatedAt < end && !m.IsReversed);
        if (!isOwner) dayMvBase = dayMvBase.Where(m => m.CreatedBy == userId);
        var todayPurchaseCost = await dayMvBase.SumAsync(m => m.PurchaseCost ?? 0);

        IQueryable<DebtPayment> dayDpBase = db.DebtPayments
            .Where(dp => dp.CreatedAt >= start && dp.CreatedAt < end);
        if (!isOwner) dayDpBase = dayDpBase.Where(dp => dp.CreatedBy == userId);
        var todayDebtCollected = await dayDpBase.SumAsync(dp => dp.Amount);

        // Previous day revenue
        var prevDate = date.AddDays(-1);
        var (prevStart, prevEnd) = WibTimeZone.GetUtcDayBounds(prevDate);
        IQueryable<Transaction> prevTxBase = db.Transactions
            .Where(t => t.Status == TransactionStatus.Completed && t.CreatedAt >= prevStart && t.CreatedAt < prevEnd);
        if (!isOwner) prevTxBase = prevTxBase.Where(t => t.StaffId == userId);
        var prevRevenue = await prevTxBase.SumAsync(t => t.PaidAmount);

        // Low stock (warehouse only, active products <= 5) — always store-wide
        var warehouseLoc = await db.Locations.FirstOrDefaultAsync(l => l.Type == LocationType.Warehouse && l.IsActive);
        int lowStockCount = 0;
        if (warehouseLoc is not null)
        {
            var levels = await GetWarehouseStockAsync(warehouseLoc.Id, warehouseLoc.Name);
            lowStockCount = levels.Count(s => (s.QuantityTotal ?? s.QuantityFilled ?? 0) <= 5);
        }

        // Weekly chart — 7 days ending on selected date, scoped to userId for non-owners
        var weeklyChart = new List<WeeklyChartEntry>();
        for (int i = 6; i >= 0; i--)
        {
            var day = date.AddDays(-i);
            var (dStart, dEnd) = WibTimeZone.GetUtcDayBounds(day);

            IQueryable<Transaction> wTxQuery = db.Transactions
                .Where(t => t.Status == TransactionStatus.Completed && t.CreatedAt >= dStart && t.CreatedAt < dEnd);
            if (!isOwner) wTxQuery = wTxQuery.Where(t => t.StaffId == userId);
            var revenue = await wTxQuery.SumAsync(t => t.PaidAmount);
            var txCount = await wTxQuery.CountAsync();

            IQueryable<StockMovement> wMvQuery = db.StockMovements
                .Where(m => m.PurchaseCost != null && m.CreatedAt >= dStart && m.CreatedAt < dEnd && !m.IsReversed);
            if (!isOwner) wMvQuery = wMvQuery.Where(m => m.CreatedBy == userId);
            var purchaseCost = await wMvQuery.SumAsync(m => m.PurchaseCost ?? 0);

            weeklyChart.Add(new WeeklyChartEntry(day.ToString("yyyy-MM-dd"), revenue, txCount, purchaseCost));
        }

        // Recent transactions — scoped to userId for non-owners
        IQueryable<Transaction> recentQuery = db.Transactions
            .Include(t => t.Customer)
            .Include(t => t.Staff)
            .Where(t => t.CreatedAt >= start && t.CreatedAt < end && t.Status == TransactionStatus.Completed);
        if (!isOwner) recentQuery = recentQuery.Where(t => t.StaffId == userId);
        var recentTxns = await recentQuery
            .OrderByDescending(t => t.CreatedAt)
            .Take(10)
            .ToListAsync();

        var recentResponses = recentTxns.Select(t => new RecentTransactionDashboardItem(
            t.Id, t.TransactionType.ToString().ToLower(), t.CustomerId, t.Customer?.Name,
            t.Staff.Name, t.Status.ToString().ToLower(),
            t.TotalAmount, t.PaidAmount, t.CreatedAt));

        // Staff revenue (FR-DSH-010) — owner only
        List<StaffRevenueSummary> staffRevenue = [];
        if (isOwner)
        {
            var staffAggregates = await db.Transactions
                .Where(t => t.Status == TransactionStatus.Completed && t.CreatedAt >= start && t.CreatedAt < end)
                .GroupBy(t => t.StaffId)
                .Select(g => new { StaffId = g.Key, Revenue = g.Sum(t => t.PaidAmount), Count = g.Count() })
                .ToListAsync();

            var staffIds = staffAggregates.Select(a => a.StaffId).ToList();
            var staffNames = await db.Users
                .Where(u => staffIds.Contains(u.Id))
                .Select(u => new { u.Id, u.Name })
                .ToDictionaryAsync(u => u.Id, u => u.Name);

            staffRevenue = staffAggregates
                .Select(a => new StaffRevenueSummary(
                    a.StaffId,
                    staffNames.GetValueOrDefault(a.StaffId, "Unknown"),
                    a.Revenue,
                    a.Count))
                .OrderByDescending(s => s.Revenue)
                .ToList();
        }

        // Warehouse stock (current state, not date-filtered) — always store-wide
        var warehouseStock = warehouseLoc is not null
            ? await GetWarehouseStockAsync(warehouseLoc.Id, warehouseLoc.Name)
            : Enumerable.Empty<WarehouseStockItem>();

        // Customer debts (current state, sorted by debt desc) — always store-wide
        var customers = await db.Customers.Where(c => c.IsActive).ToListAsync();
        var customerDebts = new List<CustomerDebtSummary>();
        foreach (var c in customers)
        {
            var initialDebt = c.InitialDebt;
            var txDebt = await db.Transactions
                .Where(t => t.CustomerId == c.Id && t.Status != TransactionStatus.Cancelled)
                .SumAsync(t => t.DebtAmount);
            var debtPaid = await db.DebtPayments
                .Where(dp => dp.CustomerId == c.Id)
                .SumAsync(dp => dp.Amount);
            var outstanding = initialDebt + txDebt - debtPaid;
            if (outstanding > 0)
                customerDebts.Add(new CustomerDebtSummary(c.Id, c.Name, outstanding));
        }
        customerDebts.Sort((a, b) => b.OutstandingDebt.CompareTo(a.OutstandingDebt));

        var totalOutstandingDebt = customerDebts.Sum(d => d.OutstandingDebt);

        // Daily stock movement summary (FR-DSH-012) — sold and received per product, excluding cancelled movements
        // Sold:     Dispatch movements. Refillable = filled container qty; Simple = all dispatch qty.
        // Received: Inbound movements (ToLocationId != null && FromLocationId == null).
        //           Refillable = filled container qty only; Simple = all inbound qty.
        var dayMovements = await db.StockMovements
            .Include(m => m.Product)
            .Where(m => m.CreatedAt >= start && m.CreatedAt < end && !m.IsReversed && !m.IsReversal)
            .ToListAsync();

        var dailyStockSummary = dayMovements
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

                var totalReceived = isRefillable
                    ? pg.Where(m => m.ToLocationId != null && m.FromLocationId == null
                                 && m.ContainerStatus == ContainerStatus.Filled)
                         .Sum(m => m.Quantity)
                    : pg.Where(m => m.ToLocationId != null && m.FromLocationId == null)
                         .Sum(m => m.Quantity);

                return new DailyStockProductSummary(
                    pg.Key.ProductId,
                    pg.Key.Name,
                    pg.Key.Unit,
                    pg.Key.Category.ToString().ToLower(),
                    totalReceived,
                    totalSold);
            })
            .Where(s => s.TotalReceived > 0 || s.TotalSold > 0)
            .OrderBy(s => s.ProductName)
            .ToList();

        // Payment method breakdown (FR-DSH-013) — scoped to userId for non-owners
        var paymentMethodBreakdown = new List<PaymentMethodBreakdownItem>();
        IQueryable<Transaction> paymentQuery = db.Transactions
            .Where(t => t.Status == TransactionStatus.Completed && t.CreatedAt >= start && t.CreatedAt < end);
        if (!isOwner) paymentQuery = paymentQuery.Where(t => t.StaffId == userId);
        
        var paymentAggregates = await paymentQuery
            .GroupBy(t => t.PaymentMethod)
            .Select(g => new { Method = g.Key, Amount = g.Sum(t => t.PaidAmount), Count = g.Count() })
            .ToListAsync();

        // Map payment methods to labels and order as: cash, transfer, qris
        var methodLabels = new Dictionary<string, string>
        {
            { "cash", "Tunai" },
            { "transfer", "Transfer" },
            { "qris", "QRIS" }
        };

        foreach (var method in new[] { "cash", "transfer", "qris" })
        {
            var agg = paymentAggregates.FirstOrDefault(a => a.Method.ToString().ToLower() == method);
            paymentMethodBreakdown.Add(new PaymentMethodBreakdownItem(
                method,
                methodLabels.GetValueOrDefault(method, method),
                agg?.Amount ?? 0,
                agg?.Count ?? 0
            ));
        }

        return new DashboardResponse(
            todayRevenue, todayTransactions, todayPurchaseCost, todayDebtCollected,
            lowStockCount, totalOutstandingDebt, prevRevenue,
            weeklyChart, recentResponses, warehouseStock, customerDebts, staffRevenue, dailyStockSummary,
            paymentMethodBreakdown);
    }

    private async Task<IEnumerable<WarehouseStockItem>> GetWarehouseStockAsync(Guid warehouseId, string locationName)
    {
        var products = await db.Products.Where(p => p.IsActive).ToListAsync();

        var inbound = await db.StockMovements
            .Where(m => m.ToLocationId == warehouseId)
            .GroupBy(m => new { m.ProductId, m.ContainerStatus })
            .Select(g => new { g.Key.ProductId, g.Key.ContainerStatus, Total = g.Sum(m => m.Quantity) })
            .ToListAsync();

        var outbound = await db.StockMovements
            .Where(m => m.FromLocationId == warehouseId)
            .GroupBy(m => new { m.ProductId, m.ContainerStatus })
            .Select(g => new { g.Key.ProductId, g.Key.ContainerStatus, Total = g.Sum(m => m.Quantity) })
            .ToListAsync();

        return products.Select(p =>
        {
            int In(ContainerStatus s) => inbound.Where(x => x.ProductId == p.Id && x.ContainerStatus == s).Sum(x => x.Total);
            int Out(ContainerStatus s) => outbound.Where(x => x.ProductId == p.Id && x.ContainerStatus == s).Sum(x => x.Total);

            if (p.Category == ProductCategory.Refillable)
                return new WarehouseStockItem(p.Id, p.Name, p.Unit, p.Category.ToString().ToLower(),
                    warehouseId, locationName,
                    In(ContainerStatus.Filled) - Out(ContainerStatus.Filled),
                    In(ContainerStatus.Empty) - Out(ContainerStatus.Empty), null);

            return new WarehouseStockItem(p.Id, p.Name, p.Unit, p.Category.ToString().ToLower(),
                warehouseId, locationName,
                null, null, In(ContainerStatus.Na) - Out(ContainerStatus.Na));
        });
    }
}
