namespace Pos.Api.DTOs.Assignments;

public record AssignmentItemResponse(
    Guid ProductId,
    string ProductName,
    string ProductUnit,
    int Quantity,
    decimal UnitPrice
);

public record AssignmentResponse(
    Guid Id,
    string Status,
    Guid KurirId,
    string KurirName,
    Guid CustomerId,
    string CustomerName,
    Guid? LocationId,
    string? LocationName,
    string? Notes,
    Guid? TransactionId,
    DateTime CreatedAt,
    IEnumerable<AssignmentItemResponse> Items
);
