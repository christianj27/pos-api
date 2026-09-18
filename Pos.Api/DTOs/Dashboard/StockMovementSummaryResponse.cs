namespace Pos.Api.DTOs.Dashboard;

/// <summary>
/// FR-DSH-012 "Pergerakan Stok" summary for a resolved period (see <see cref="Services.StockPeriodRange"/>).
/// </summary>
public record StockMovementSummaryResponse(
    string Period,
    string StartDate,
    string EndDate,
    IEnumerable<StockProductSummary> Items
);
