namespace Pos.Api.DTOs.Dashboard;

public record RecentTransactionDashboardItem(
    Guid Id,
    string Type,
    Guid? CustomerId,
    string? CustomerName,
    string CreatedByName,
    string Status,
    decimal TotalAmount,
    decimal PaidAmount,
    DateTime CreatedAt
);

public record WarehouseStockItem(
    Guid ProductId,
    string ProductName,
    string ProductUnit,
    string ProductCategory,
    Guid LocationId,
    string LocationName,
    int? QuantityFilled,
    int? QuantityEmpty,
    int? QuantityTotal
);

public record DashboardResponse(
    decimal TodayRevenue,
    int TodayTransactions,
    decimal TodayPurchaseCost,
    decimal TodayDebtCollected,
    int LowStockCount,
    decimal TotalOutstandingDebt,
    decimal PreviousDayRevenue,
    IEnumerable<WeeklyChartEntry> WeeklyChart,
    IEnumerable<RecentTransactionDashboardItem> RecentTransactions,
    IEnumerable<WarehouseStockItem> WarehouseStock,
    IEnumerable<CustomerDebtSummary> CustomerDebts
);
