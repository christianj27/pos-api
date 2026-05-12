namespace Pos.Api.DTOs.CashFlow;

public record CashFlowSummaryResponse(
    decimal TotalCashIn,
    decimal TotalCashOut,
    decimal NetCash,
    decimal TotalNewDebt,
    IEnumerable<CashFlowEntryResponse> Entries
);
