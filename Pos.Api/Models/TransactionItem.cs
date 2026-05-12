namespace Pos.Api.Models;

public class TransactionItem
{
    public Guid Id { get; set; }
    public Guid TransactionId { get; set; }
    public Guid ProductId { get; set; }
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }

    public Transaction Transaction { get; set; } = null!;
    public Product Product { get; set; } = null!;
}
