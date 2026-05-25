namespace Pos.Api.DTOs.Customers;

public record CustomerResponse(
    Guid Id,
    string Name,
    string? Phone,
    string? Address,
    bool IsActive,
    bool IsConfidential,
    DateTime CreatedAt,
    decimal OutstandingDebt
);
