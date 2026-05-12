namespace Pos.Api.DTOs.Customers;

public record ContainerLoanNetItem(
    Guid ProductId,
    string ProductName,
    string Unit,
    int NetQuantity
);

public record ContainerLoanSummaryResponse(
    Guid CustomerId,
    string CustomerName,
    IEnumerable<ContainerLoanNetItem> Items
);
