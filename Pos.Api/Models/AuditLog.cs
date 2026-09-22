namespace Pos.Api.Models;

/// <summary>
/// Generic audit trail for money-affecting changes (FR-STL-008): transaction edits before settlement,
/// and settlement approve/reject/reopen/cash-adjustment actions. Every row carries a mandatory reason.
/// </summary>
public class AuditLog
{
    public Guid Id { get; set; }

    /// <summary>Entity kind, e.g. "transaction" or "settlement".</summary>
    public string EntityType { get; set; } = string.Empty;
    public Guid EntityId { get; set; }

    /// <summary>Action kind, e.g. "edit", "approve", "reject", "reopen", "cash_adjustment".</summary>
    public string Action { get; set; } = string.Empty;

    public string? Reason { get; set; }

    /// <summary>JSON payload of the changed fields (old → new).</summary>
    public string? ChangesJson { get; set; }

    public Guid ActorId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public User Actor { get; set; } = null!;
}
