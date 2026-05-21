namespace Pos.Api.DTOs.Assignments;

public record AssignmentItemRequest(Guid ProductId, int Quantity, decimal UnitPrice);

public record CreateAssignmentRequest(
    Guid KurirId,
    Guid CustomerId,
    Guid LocationId,
    IEnumerable<AssignmentItemRequest> Items,
    string? Notes
);
