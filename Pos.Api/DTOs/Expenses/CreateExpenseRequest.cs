namespace Pos.Api.DTOs.Expenses;

public record CreateExpenseRequest(
    string Category,
    string Description,
    decimal Amount,
    DateOnly ExpenseDate
);
