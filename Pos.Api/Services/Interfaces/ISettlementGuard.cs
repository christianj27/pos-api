namespace Pos.Api.Services.Interfaces;

/// <summary>A blocked write, with the machine-readable reason the frontend keys off.</summary>
public sealed record SettlementBlock(
    string Code,
    string Message,
    DateOnly BusinessDate,
    Guid SettlementId,
    string Status
);

/// <summary>
/// Enforces the two settlement gates (FR-STL-002):
/// <list type="bullet">
///   <item><b>Gate A — pendency:</b> a kurir/kasir with an unapproved earlier business date cannot start new work.</item>
///   <item><b>Gate B — immutability:</b> an approved business date is frozen for that user.</item>
/// </list>
/// The owner is exempt from Gate A but never from Gate B.
/// </summary>
public interface ISettlementGuard
{
    /// <summary>
    /// Returns the pending day that blocks new work, or null when the user may work.
    /// The owner is resolved from the user record and is never blocked by this gate.
    /// </summary>
    Task<SettlementBlock?> GetBlockAsync(Guid userId);

    /// <summary>Gate A: message describing why new work is blocked, or null when allowed.</summary>
    Task<string?> EnsureNewWorkAllowedAsync(Guid userId);

    /// <summary>Gate B: message describing why the day is frozen, or null when it is still writable.</summary>
    Task<string?> EnsureDayWritableAsync(Guid userId, DateOnly businessDate);

    /// <summary>
    /// Lazily opens the settlement row for a user's business date. The row is only added to the change
    /// tracker — the caller's own SaveChanges persists it, keeping it inside the caller's transaction.
    /// </summary>
    Task TouchDayAsync(Guid userId, DateOnly businessDate);
}
