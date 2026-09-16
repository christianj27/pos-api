namespace Pos.Api.DTOs.Expenses;

public record ExpenseResponse(
    Guid Id,
    string Category,
    string Description,
    decimal Amount,
    DateOnly ExpenseDate,
    string CreatedByName,
    DateTime CreatedAt
);
