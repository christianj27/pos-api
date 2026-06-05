using Pos.Api.Data.Enums;

namespace Pos.Api.Models;

public class DebtPayment
{
    public Guid Id { get; set; }
    public Guid CustomerId { get; set; }
    public decimal Amount { get; set; }
    public PaymentMethod Method { get; set; }
    public string? ReferenceNo { get; set; }
    public string? Note { get; set; }
    public Guid? TransactionId { get; set; }
    public Guid CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Customer Customer { get; set; } = null!;
    public User Creator { get; set; } = null!;
    public Transaction? Transaction { get; set; }
}
