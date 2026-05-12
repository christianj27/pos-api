namespace Pos.Api.DTOs.Customers;

public record CustomerDebtSummaryResponse(
    Guid CustomerId,
    string CustomerName,
    decimal OutstandingDebt
);
