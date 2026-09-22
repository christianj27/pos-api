using Pos.Api.Data.Enums;

namespace Pos.Api.Models;

public class Payment
{
    public Guid Id { get; set; }
    public Guid TransactionId { get; set; }
    public decimal Amount { get; set; }
    public PaymentMethod Method { get; set; }
    public string? ReferenceNo { get; set; }
    public DateTime PaidAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// The user who collected this money. Drives which user's daily settlement the cash counts towards
    /// (FR-STL-007). Nullable only for rows that predate the settlement feature — those fall back to the
    /// transaction's <see cref="Transaction.StaffId"/>.
    /// </summary>
    public Guid? CreatedBy { get; set; }

    public Transaction Transaction { get; set; } = null!;
    public User? Creator { get; set; }
}
