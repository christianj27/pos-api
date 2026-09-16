namespace Pos.Api.DTOs.Dashboard;

public record ContainerLoanSummaryItem(
    Guid CustomerId,
    string CustomerName,
    Guid ProductId,
    string ProductName,
    string ProductUnit,
    int NetQuantity
);
