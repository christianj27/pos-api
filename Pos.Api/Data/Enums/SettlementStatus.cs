namespace Pos.Api.Data.Enums;

/// <summary>
/// Lifecycle of a user's daily kas settlement (FR-STL).
/// <list type="bullet">
///   <item><see cref="Open"/> — the day has activity and must be closed by the user (the default for a lazily created row).</item>
///   <item><see cref="Submitted"/> — the user has closed the day and is waiting for owner verification.</item>
///   <item><see cref="Rejected"/> — the owner rejected the close; the user is blocked again and must fix the data.</item>
///   <item><see cref="Approved"/> — the owner verified the batch; the day is frozen (Gate B).</item>
/// </list>
/// </summary>
public enum SettlementStatus
{
    Open,
    Submitted,
    Rejected,
    Approved
}
