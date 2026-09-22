namespace Pos.Api.Models;

/// <summary>
/// Owner-only "Selisih Kas" correction (FR-STL-009). Because cash variance must be zero to close a day,
/// a genuinely short or over amount is booked here instead of being hidden in the transactions:
/// a negative amount is cash out (short), a positive amount is cash in (over).
/// </summary>
public class CashAdjustment
{
    public Guid Id { get; set; }

    /// <summary>The user whose cash is being corrected.</summary>
    public Guid UserId { get; set; }
    public DateOnly BusinessDate { get; set; }

    /// <summary>Signed amount: negative = short (cash out), positive = over (cash in).</summary>
    public decimal Amount { get; set; }

    public string Reason { get; set; } = string.Empty;
    public Guid CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public User User { get; set; } = null!;
    public User Creator { get; set; } = null!;
}
