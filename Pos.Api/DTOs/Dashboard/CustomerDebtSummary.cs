namespace Pos.Api.DTOs.Dashboard;

public record CustomerDebtSummary(
    Guid CustomerId,
    string CustomerName,
    decimal OutstandingDebt
);
