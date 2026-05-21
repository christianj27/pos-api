using Pos.Api.Data.Enums;

namespace Pos.Api.Models;

public class DeliveryAssignment
{
    public Guid Id { get; set; }
    public Guid KurirId { get; set; }
    public Guid CustomerId { get; set; }
    public Guid CreatedBy { get; set; }
    public AssignmentStatus Status { get; set; } = AssignmentStatus.Pending;
    public Guid? LocationId { get; set; }
    public Guid? TransactionId { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public User Kurir { get; set; } = null!;
    public Customer Customer { get; set; } = null!;
    public User Creator { get; set; } = null!;
    public Location? Location { get; set; }
    public Transaction? Transaction { get; set; }
    public ICollection<DeliveryAssignmentItem> Items { get; set; } = [];
}
