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

    public Transaction Transaction { get; set; } = null!;
}
