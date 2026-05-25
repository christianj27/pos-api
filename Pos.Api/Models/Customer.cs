namespace Pos.Api.Models;

public class Customer
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? Address { get; set; }
    public bool IsActive { get; set; } = true;
    public bool IsConfidential { get; set; } = false;
    public decimal InitialDebt { get; set; } = 0m;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<CustomerPricing> Pricings { get; set; } = [];
    public ICollection<Transaction> Transactions { get; set; } = [];
    public ICollection<DebtPayment> DebtPayments { get; set; } = [];
    public ICollection<ContainerLoan> ContainerLoans { get; set; } = [];
    public ICollection<DeliveryAssignment> Assignments { get; set; } = [];
}
