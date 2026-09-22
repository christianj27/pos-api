namespace Pos.Api.DTOs.Settlements;

public record SettlementMethodLineRequest(
    string Method,
    decimal CountedAmount
);

public record SettlementStockLineRequest(
    Guid ProductId,
    int CountedFilled,
    int CountedEmpty
);

/// <summary>
/// The user's "Tutup Kas" submission. <c>BusinessDate</c> defaults to today (WIB); a blocked user
/// closes the previous day instead. <c>CountedCash</c> is what the user physically counted.
/// </summary>
public record SubmitSettlementRequest(
    DateOnly? BusinessDate,
    decimal CountedCash,
    IEnumerable<SettlementMethodLineRequest>? MethodLines,
    IEnumerable<SettlementStockLineRequest>? StockLines,
    string? Note
);

public record RejectSettlementRequest(string Reason);

public record ReopenSettlementRequest(string Reason);

/// <summary>Owner-booked "Selisih Kas" correction. Negative = short (cash out), positive = over (cash in).</summary>
public record CashAdjustmentRequest(
    Guid UserId,
    DateOnly BusinessDate,
    decimal Amount,
    string Reason
);
