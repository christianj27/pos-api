using Pos.Api.Data.Enums;

namespace Pos.Api.Models;

/// <summary>Owner-recorded operational expense (FR-CSH-006). Surfaces as a cash_out/operational_expense entry.</summary>
public class Expense
{
    public Guid Id { get; set; }
    public ExpenseCategory Category { get; set; }
    public string Description { get; set; } = string.Empty;
    public decimal Amount { get; set; }

    /// <summary>Business date (WIB). Drives which day the expense appears under in Arus Kas.</summary>
    public DateOnly ExpenseDate { get; set; }

    public Guid CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public User Creator { get; set; } = null!;
}
