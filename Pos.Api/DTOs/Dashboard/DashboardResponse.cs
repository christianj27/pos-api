using Pos.Api.DTOs.Transactions;

namespace Pos.Api.DTOs.Dashboard;

public record DashboardStatCard(
    decimal TodayRevenue,
    decimal TodayPurchaseCost,
    decimal TodayDebtCollected,
    int LowStockCount,
    decimal PreviousDayRevenue
);

public record WarehouseStockItem(
    Guid ProductId,
    string ProductName,
    string Unit,
    string Category,
    int? QuantityFilled,
    int? QuantityEmpty,
    int? QuantityTotal
);

public record DashboardResponse(
    DashboardStatCard Stats,
    IEnumerable<WeeklyChartEntry> WeeklyChart,
    IEnumerable<TransactionResponse> RecentTransactions,
    IEnumerable<WarehouseStockItem> WarehouseStock,
    IEnumerable<CustomerDebtSummary> CustomerDebts
);
