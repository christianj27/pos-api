using Pos.Api.Data.Enums;

namespace Pos.Api.Models;

public class Location
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public LocationType Type { get; set; }
    public Guid? AssignedTo { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public User? AssignedUser { get; set; }
    public ICollection<StockMovement> IncomingMovements { get; set; } = [];
    public ICollection<StockMovement> OutgoingMovements { get; set; } = [];
    public ICollection<Transaction> Transactions { get; set; } = [];
}
