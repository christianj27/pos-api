namespace Pos.Api.Models;

public class DeliveryAssignmentItem
{
    public Guid Id { get; set; }
    public Guid AssignmentId { get; set; }
    public Guid ProductId { get; set; }
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }

    public DeliveryAssignment Assignment { get; set; } = null!;
    public Product Product { get; set; } = null!;
}
