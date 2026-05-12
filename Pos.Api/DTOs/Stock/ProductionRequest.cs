namespace Pos.Api.DTOs.Stock;

public record ProductionRequest(
    Guid ProductId,
    Guid LocationId,
    int Quantity,
    decimal? ProductionCost,
    string? Note
);
