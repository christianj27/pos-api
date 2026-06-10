namespace Pos.Api.Models;

public class ContainerLoan
{
    public Guid Id { get; set; }
    public Guid? TransactionId { get; set; }
    public Guid CustomerId { get; set; }
    public Guid ProductId { get; set; }
    public int Quantity { get; set; }
    public string? Note { get; set; }
    public bool IsReversed { get; set; } = false;
    public Guid CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Transaction? Transaction { get; set; }
    public Customer Customer { get; set; } = null!;
    public Product Product { get; set; } = null!;
    public User Creator { get; set; } = null!;
}
