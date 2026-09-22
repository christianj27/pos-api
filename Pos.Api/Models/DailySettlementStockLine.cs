namespace Pos.Api.Models;

/// <summary>
/// Per-product stock and container reconciliation for the user's vehicle (FR-STL-006).
/// The filled/empty expectations are the computed on-truck balance; the container columns record the
/// day's container-loan movement. Variance here is informational — the owner decides whether to reject.
/// </summary>
public class DailySettlementStockLine
{
    public Guid Id { get; set; }
    public Guid SettlementId { get; set; }
    public Guid ProductId { get; set; }

    public int ExpectedFilled { get; set; }
    public int CountedFilled { get; set; }
    public int ExpectedEmpty { get; set; }
    public int CountedEmpty { get; set; }

    /// <summary>Filled containers lent to customers on this day (ContainerLoan quantity &gt; 0).</summary>
    public int ContainersOut { get; set; }
    /// <summary>Containers returned by customers on this day (ContainerLoan quantity &lt; 0, stored positive).</summary>
    public int ContainersReturned { get; set; }

    public DailySettlement Settlement { get; set; } = null!;
    public Product Product { get; set; } = null!;
}
