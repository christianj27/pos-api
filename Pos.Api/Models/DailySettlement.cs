using Pos.Api.Data.Enums;

namespace Pos.Api.Models;

/// <summary>
/// One settlement ("Tutup Kas") per user per business date (WIB). A row is created lazily with
/// <see cref="SettlementStatus.Open"/> the first time the user writes anything on that business date,
/// so idle days never exist and therefore never block anyone (FR-STL-002).
/// Amount fields hold the snapshot taken when the user submitted the close; live figures before that
/// are always recomputed from the underlying ledger.
/// </summary>
public class DailySettlement
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }

    /// <summary>Business date in WIB (hard midnight boundary, FR-STL-003).</summary>
    public DateOnly BusinessDate { get; set; }

    public SettlementStatus Status { get; set; } = SettlementStatus.Open;

    /// <summary>Vehicle location used for the stock/container reconciliation; null for users without an assigned vehicle.</summary>
    public Guid? VehicleLocationId { get; set; }

    // -- Cash ---------------------------------------------------------------
    /// <summary>Cash the user should be holding: cash method totals − cash out + cash adjustments.</summary>
    public decimal ExpectedCash { get; set; }
    public decimal CountedCash { get; set; }
    /// <summary><see cref="CountedCash"/> − <see cref="ExpectedCash"/>. Must be zero to submit (FR-STL-005).</summary>
    public decimal CashVariance { get; set; }
    public decimal CashIn { get; set; }
    public decimal CashOut { get; set; }
    public decimal CashAdjustmentTotal { get; set; }

    // -- Non-cash -----------------------------------------------------------
    public decimal TransferExpected { get; set; }
    public decimal QrisExpected { get; set; }

    // -- Debt ---------------------------------------------------------------
    /// <summary>New customer debt (piutang baru) created on this day by this user.</summary>
    public decimal NewDebtTotal { get; set; }
    /// <summary>Debt payments collected on this day by this user, all methods.</summary>
    public decimal DebtPaymentTotal { get; set; }

    // -- Lifecycle ----------------------------------------------------------
    public string? Note { get; set; }
    public DateTime? SubmittedAt { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public Guid? ReviewedBy { get; set; }
    public string? ReviewNote { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public User User { get; set; } = null!;
    public User? Reviewer { get; set; }
    public Location? VehicleLocation { get; set; }
    public ICollection<DailySettlementMethodLine> MethodLines { get; set; } = [];
    public ICollection<DailySettlementStockLine> StockLines { get; set; } = [];
}
