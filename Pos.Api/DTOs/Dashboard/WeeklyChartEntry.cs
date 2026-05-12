namespace Pos.Api.DTOs.Dashboard;

public record WeeklyChartEntry(
    string Date,
    decimal Revenue,
    int TransactionCount,
    decimal PurchaseCost
);
