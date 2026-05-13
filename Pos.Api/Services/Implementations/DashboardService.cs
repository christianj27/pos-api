using Microsoft.EntityFrameworkCore;
using Pos.Api.Data;
using Pos.Api.Data.Enums;
using Pos.Api.DTOs.Dashboard;
using Pos.Api.DTOs.Transactions;
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

        // Low stock (warehouse only, active products - 5)
        var warehouseLoc = await db.Locations.FirstOrDefaultAsync(l => l.Type == LocationType.Warehouse && l.IsActive);
        int lowStockCount = 0;
        if (warehouseLoc is not null)
        {
            var levels = await GetWarehouseStockAsync(warehouseLoc.Id);
            lowStockCount = levels.Count(s => (s.QuantityTotal ?? s.QuantityFilled ?? 0) <= 5);
        }

        var stats = new DashboardStatCard(todayRevenue, todayPurchaseCost, todayDebtCollected, lowStockCount, prevRevenue);

        // Weekly chart — 7 days ending on selected date
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

        // Recent transactions for selected date (10 most recent)
        var recentTxns = await db.Transactions
            .Include(t => t.Customer).Include(t => t.Staff).Include(t => t.Location)
            .Include(t => t.Items).ThenInclude(i => i.Product)
            .Where(t => t.CreatedAt >= start && t.CreatedAt < end)
            .OrderByDescending(t => t.CreatedAt)
            .Take(10)
            .ToListAsync();

        var recentResponses = recentTxns.Select(t => new TransactionResponse(
            t.Id, t.TransactionType.ToString().ToLower(), t.CustomerId, t.Customer?.Name,
            t.StaffId, t.Staff.Name, t.LocationId, t.Location.Name,
            t.Status.ToString().ToLower(), t.PaymentMethod.ToString().ToLower(),
            t.TotalAmount, t.PaidAmount, t.DebtAmount, t.Notes, t.CreatedAt,
            t.Items.Select(i => new TransactionItemResponse(i.Id, i.ProductId, i.Product.Name,
                i.Product.Unit, i.Quantity, i.UnitPrice, i.Quantity * i.UnitPrice))));

        // Warehouse stock (current state, not date-filtered)
        var warehouseStock = warehouseLoc is not null
            ? await GetWarehouseStockAsync(warehouseLoc.Id)
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

        return new DashboardResponse(stats, weeklyChart, recentResponses, warehouseStock, customerDebts);
    }

    private async Task<IEnumerable<WarehouseStockItem>> GetWarehouseStockAsync(Guid warehouseId)
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
                    In(ContainerStatus.Filled) - Out(ContainerStatus.Filled),
                    In(ContainerStatus.Empty) - Out(ContainerStatus.Empty), null);

            return new WarehouseStockItem(p.Id, p.Name, p.Unit, p.Category.ToString().ToLower(),
                null, null, In(ContainerStatus.Na) - Out(ContainerStatus.Na));
        });
    }
}
