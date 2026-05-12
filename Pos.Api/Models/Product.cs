using Pos.Api.Data.Enums;

namespace Pos.Api.Models;

public class Product
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public ProductCategory Category { get; set; }
    public ProductionType? ProductionType { get; set; }
    public ProductType Type { get; set; }
    public string Unit { get; set; } = string.Empty;
    public decimal BasePrice { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<CustomerPricing> CustomerPricings { get; set; } = [];
    public ICollection<StockMovement> StockMovements { get; set; } = [];
    public ICollection<TransactionItem> TransactionItems { get; set; } = [];
    public ICollection<ContainerLoan> ContainerLoans { get; set; } = [];
    public ICollection<DeliveryAssignmentItem> AssignmentItems { get; set; } = [];
}
