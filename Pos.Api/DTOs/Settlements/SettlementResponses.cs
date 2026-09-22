namespace Pos.Api.DTOs.Settlements;

public record SettlementMethodLineResponse(
    Guid Id,
    string Method,
    decimal ExpectedAmount,
    decimal CountedAmount,
    decimal Variance
);

public record SettlementStockLineResponse(
    Guid Id,
    Guid ProductId,
    string ProductName,
    string ProductUnit,
    bool IsRefillable,
    int ExpectedFilled,
    int CountedFilled,
    int ExpectedEmpty,
    int CountedEmpty,
    int ContainersOut,
    int ContainersReturned
);

/// <summary>Live figures for a business date, recomputed from the ledger (never from the snapshot).</summary>
public record SettlementPreviewResponse(
    string BusinessDate,
    string Status,
    Guid? SettlementId,
    decimal ExpectedCash,
    decimal CountedCash,
    decimal CashVariance,
    decimal CashIn,
    decimal CashOut,
    decimal CashAdjustments,
    decimal TransferExpected,
    decimal QrisExpected,
    decimal NewDebtTotal,
    decimal DebtPaymentTotal,
    Guid? VehicleLocationId,
    string? VehicleLocationName,
    IEnumerable<SettlementMethodLineResponse> Methods,
    IEnumerable<SettlementStockLineResponse> Stocks
);

public record SettlementResponse(
    Guid Id,
    Guid UserId,
    string UserName,
    string BusinessDate,
    string Status,
    decimal ExpectedCash,
    decimal CountedCash,
    decimal CashVariance,
    decimal TransferExpected,
    decimal QrisExpected,
    decimal NewDebtTotal,
    decimal DebtPaymentTotal,
    string? Note,
    DateTime? SubmittedAt,
    DateTime? ReviewedAt,
    string? ReviewerName,
    string? ReviewNote,
    DateTime CreatedAt
);

public record AuditLogResponse(
    Guid Id,
    string Action,
    string? Reason,
    string? ChangesJson,
    string ActorName,
    DateTime CreatedAt
);

public record SettlementDetailResponse(
    SettlementResponse Settlement,
    IEnumerable<SettlementMethodLineResponse> Methods,
    IEnumerable<SettlementStockLineResponse> Stocks,
    IEnumerable<AuditLogResponse> AuditTrail
);

/// <summary>Drives the "you must settle first" banner on the frontend. Never blocks the owner.</summary>
public record SettlementStatusResponse(
    bool Blocked,
    string? BlockingBusinessDate,
    string? BlockingStatus,
    Guid? BlockingSettlementId,
    string? Message
);
