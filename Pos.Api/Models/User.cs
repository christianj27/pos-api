using Pos.Api.Data.Enums;

namespace Pos.Api.Models;

public class User
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public UserRole Role { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<Location> AssignedLocations { get; set; } = [];
    public ICollection<StockMovement> StockMovements { get; set; } = [];
    public ICollection<Transaction> Transactions { get; set; } = [];
    public ICollection<DebtPayment> DebtPayments { get; set; } = [];
    public ICollection<ContainerLoan> ContainerLoans { get; set; } = [];
    public ICollection<RefreshToken> RefreshTokens { get; set; } = [];
    public ICollection<DeliveryAssignment> CreatedAssignments { get; set; } = [];
    public ICollection<DeliveryAssignment> KurirAssignments { get; set; } = [];
}
