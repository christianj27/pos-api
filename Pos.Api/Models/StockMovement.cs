using Pos.Api.Data.Enums;

namespace Pos.Api.Models;

public class StockMovement
{
    public Guid Id { get; set; }
    public Guid ProductId { get; set; }
    public MovementType MovementType { get; set; }
    public ContainerStatus ContainerStatus { get; set; }
    public int Quantity { get; set; }
    public Guid? FromLocationId { get; set; }
    public Guid? ToLocationId { get; set; }
    public decimal? PurchaseCost { get; set; }
    public string? Note { get; set; }
    public Guid? TransactionId { get; set; }
    public Guid? BatchId { get; set; }
    public bool IsReversed { get; set; } = false;
    public bool IsReversal { get; set; } = false;
    public Guid CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Product Product { get; set; } = null!;
    public Location? FromLocation { get; set; }
    public Location? ToLocation { get; set; }
    public Transaction? Transaction { get; set; }
    public User Creator { get; set; } = null!;
}
