namespace Pos.Api.DTOs.Stock;

public record TransferRequest(
    Guid ProductId,
    string ContainerStatus,
    int Quantity,
    Guid FromLocationId,
    Guid ToLocationId,
    string? Note
);
