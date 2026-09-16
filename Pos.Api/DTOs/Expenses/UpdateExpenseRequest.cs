namespace Pos.Api.DTOs.Expenses;

public record UpdateExpenseRequest(
    string Category,
    string Description,
    decimal Amount,
    DateOnly ExpenseDate
);
