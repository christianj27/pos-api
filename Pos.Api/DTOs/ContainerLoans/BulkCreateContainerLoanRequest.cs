namespace Pos.Api.DTOs.ContainerLoans;

public record BulkContainerLoanItem(
    Guid ProductId,
    int Quantity,
    string ContainerStatus,
    string? Note
);

public record BulkCreateContainerLoanRequest(
    Guid CustomerId,
    Guid LocationId,
    IEnumerable<BulkContainerLoanItem> Items,
    string? Note
);
