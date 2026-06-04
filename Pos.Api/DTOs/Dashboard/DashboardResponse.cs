namespace Pos.Api.DTOs.Dashboard;

public record PaymentMethodBreakdownItem(
    string Method,
    string Label,
    decimal Amount,
    int Count
);

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

public record StaffRevenueSummary(
    Guid StaffId,
    string StaffName,
    decimal Revenue,
    int TransactionCount
);

public record DailyStockProductSummary(
    Guid ProductId,
    string ProductName,
    string ProductUnit,
    string ProductCategory,
    int TotalReceived,
    int TotalSold
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
    IEnumerable<CustomerDebtSummary> CustomerDebts,
    IEnumerable<StaffRevenueSummary> StaffRevenue,
    IEnumerable<DailyStockProductSummary> DailyStockSummary,
    IEnumerable<PaymentMethodBreakdownItem> PaymentMethodBreakdown
);
