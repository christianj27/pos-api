using Pos.Api.Data.Enums;

namespace Pos.Api.Models;

public class Transaction
{
    public Guid Id { get; set; }
    public TransactionType TransactionType { get; set; }
    public Guid? CustomerId { get; set; }
    public Guid StaffId { get; set; }
    public Guid LocationId { get; set; }
    public TransactionStatus Status { get; set; } = TransactionStatus.Completed;
    public PaymentMethod PaymentMethod { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal PaidAmount { get; set; }
    public decimal DebtAmount { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }

    public Customer? Customer { get; set; }
    public User Staff { get; set; } = null!;
    public Location Location { get; set; } = null!;
    public ICollection<TransactionItem> Items { get; set; } = [];
    public ICollection<Payment> Payments { get; set; } = [];
    public ICollection<ContainerLoan> ContainerLoans { get; set; } = [];
    public ICollection<StockMovement> StockMovements { get; set; } = [];
}
