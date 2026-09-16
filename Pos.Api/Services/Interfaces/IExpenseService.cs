using Pos.Api.DTOs.Expenses;

namespace Pos.Api.Services.Interfaces;

public interface IExpenseService
{
    /// <summary>Lists expenses recorded for the given business date (defaults to today WIB).</summary>
    Task<IEnumerable<ExpenseResponse>> GetAllAsync(DateOnly? date);

    Task<(ExpenseResponse? Expense, string? Error)> CreateAsync(CreateExpenseRequest request, Guid createdBy);

    Task<(ExpenseResponse? Expense, bool NotFound, string? Error)> UpdateAsync(Guid id, UpdateExpenseRequest request);

    Task<(bool Success, bool NotFound, string? Error)> DeleteAsync(Guid id);
}
