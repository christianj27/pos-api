namespace Pos.Api.DTOs.CashFlow;

public record CashFlowEntryResponse(
    Guid index,
    Guid Id,
    string FlowType,
    string Category,
    decimal Amount,
    string Description,
    Guid? ReferenceId,
    string CreatedByName,
    DateTime CreatedAt
);
