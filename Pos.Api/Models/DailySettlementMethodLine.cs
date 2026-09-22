using Pos.Api.Data.Enums;

namespace Pos.Api.Models;

/// <summary>
/// Snapshot of one payment method on a settlement. Only <see cref="PaymentMethod.Cash"/> is physically
/// counted by the user and hard-gated; Transfer/QRIS are confirmed against the ledger (FR-STL-004).
/// </summary>
public class DailySettlementMethodLine
{
    public Guid Id { get; set; }
    public Guid SettlementId { get; set; }
    public PaymentMethod Method { get; set; }
    public decimal ExpectedAmount { get; set; }
    public decimal CountedAmount { get; set; }
    public decimal Variance { get; set; }

    public DailySettlement Settlement { get; set; } = null!;
}
