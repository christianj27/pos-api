namespace Pos.Api.DTOs.Customers;

public record CustomerResponse(
    Guid Id,
    string Name,
    string? Phone,
    string? Address,
    bool IsActive,
    DateTime CreatedAt,
    decimal OutstandingDebt
);
