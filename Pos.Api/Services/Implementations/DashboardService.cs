using Microsoft.EntityFrameworkCore;
using Pos.Api.Data;
using Pos.Api.Data.Enums;
using Pos.Api.DTOs.Dashboard;
using Pos.Api.Services.Interfaces;

namespace Pos.Api.Services.Implementations;

public class DashboardService(AppDbContext db) : IDashboardService
{
    public async Task<DashboardResponse> GetDashboardAsync(DateOnly date)
    {
        var (start, end) = WibTimeZone.GetUtcDayBounds(date);

        // Summary stats for selected date
        var todayRevenue = await db.Transactions
            .Where(t => t.Status == TransactionStatus.Completed && t.CreatedAt >= start && t.CreatedAt < end)
            .SumAsync(t => t.PaidAmount);

        var todayTransactions = await db.Transactions
            .CountAsync(t => t.Status == TransactionStatus.Completed && t.CreatedAt >= start && t.CreatedAt < end);

        var todayPurchaseCost = await db.StockMovements
            .Where(m => m.PurchaseCost != null && m.CreatedAt >= start && m.CreatedAt < end)
            .SumAsync(m => m.PurchaseCost ?? 0);

        var todayDebtCollected = await db.DebtPayments
            .Where(dp => dp.CreatedAt >= start && dp.CreatedAt < end)
            .SumAsync(dp => dp.Amount);

        // Previous day revenue
        var prevDate = date.AddDays(-1);
        var (prevStart, prevEnd) = WibTimeZone.GetUtcDayBounds(prevDate);
        var prevRevenue = await db.Transactions
            .Where(t => t.Status == TransactionStatus.Completed && t.CreatedAt >= prevStart && t.CreatedAt < prevEnd)
            .SumAsync(t => t.PaidAmount);

        // Low stock (warehouse only, active products <= 5)
        var warehouseLoc = await db.Locations.FirstOrDefaultAsync(l => l.Type == LocationType.Warehouse && l.IsActive);
        int lowStockCount = 0;
        if (warehouseLoc is not null)
        {
            var levels = await GetWarehouseStockAsync(warehouseLoc.Id, warehouseLoc.Name);
            lowStockCount = levels.Count(s => (s.QuantityTotal ?? s.QuantityFilled ?? 0) <= 5);
        }

        // Weekly chart -- 7 days ending on selected date
        var weeklyChart = new List<WeeklyChartEntry>();
        for (int i = 6; i >= 0; i--)
        {
            var day = date.AddDays(-i);
            var (dStart, dEnd) = WibTimeZone.GetUtcDayBounds(day);

            var revenue = await db.Transactions
                .Where(t => t.Status == TransactionStatus.Completed && t.CreatedAt >= dStart && t.CreatedAt < dEnd)
                .SumAsync(t => t.PaidAmount);

            var txCount = await db.Transactions
                .CountAsync(t => t.Status == TransactionStatus.Completed && t.CreatedAt >= dStart && t.CreatedAt < dEnd);

            var purchaseCost = await db.StockMovements
                .Where(m => m.PurchaseCost != null && m.CreatedAt >= dStart && m.CreatedAt < dEnd)
                .SumAsync(m => m.PurchaseCost ?? 0);

            weeklyChart.Add(new WeeklyChartEntry(day.ToString("yyyy-MM-dd"), revenue, txCount, purchaseCost));
        }

        // Recent transactions for selected date (10 most recent) -- slim projection
        var recentTxns = await db.Transactions
            .Include(t => t.Customer)
            .Include(t => t.Staff)
            .Where(t => t.CreatedAt >= start && t.CreatedAt < end)
            .OrderByDescending(t => t.CreatedAt)
            .Take(10)
            .ToListAsync();

        var recentResponses = recentTxns.Select(t => new RecentTransactionDashboardItem(
            t.Id, t.TransactionType.ToString().ToLower(), t.CustomerId, t.Customer?.Name,
            t.Staff.Name, t.Status.ToString().ToLower(),
            t.TotalAmount, t.PaidAmount, t.CreatedAt));

        // Staff revenue for selected date (FR-DSH-010)
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

        var staffRevenue = staffAggregates
            .Select(a => new StaffRevenueSummary(
                a.StaffId,
                staffNames.GetValueOrDefault(a.StaffId, "Unknown"),
                a.Revenue,
                a.Count))
            .OrderByDescending(s => s.Revenue)
            .ToList();

        // Warehouse stock (current state, not date-filtered)
        var warehouseStock = warehouseLoc is not null
            ? await GetWarehouseStockAsync(warehouseLoc.Id, warehouseLoc.Name)
            : Enumerable.Empty<WarehouseStockItem>();

        // Customer debts (current state, sorted by debt desc)
        var customers = await db.Customers.Where(c => c.IsActive).ToListAsync();
        var customerDebts = new List<CustomerDebtSummary>();
        foreach (var c in customers)
        {
            var txDebt = await db.Transactions
                .Where(t => t.CustomerId == c.Id && t.Status != TransactionStatus.Cancelled)
                .SumAsync(t => t.DebtAmount);
            var debtPaid = await db.DebtPayments
                .Where(dp => dp.CustomerId == c.Id)
                .SumAsync(dp => dp.Amount);
            var outstanding = txDebt - debtPaid;
            if (outstanding > 0)
                customerDebts.Add(new CustomerDebtSummary(c.Id, c.Name, outstanding));
        }
        customerDebts.Sort((a, b) => b.OutstandingDebt.CompareTo(a.OutstandingDebt));

        var totalOutstandingDebt = customerDebts.Sum(d => d.OutstandingDebt);

        return new DashboardResponse(
            todayRevenue, todayTransactions, todayPurchaseCost, todayDebtCollected,
            lowStockCount, totalOutstandingDebt, prevRevenue,
            weeklyChart, recentResponses, warehouseStock, customerDebts, staffRevenue);
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
