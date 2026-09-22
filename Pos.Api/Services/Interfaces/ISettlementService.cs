using Pos.Api.DTOs.Settlements;

namespace Pos.Api.Services.Interfaces;

public interface ISettlementService
{
    /// <summary>Live figures for a user's business date, recomputed from the ledger.</summary>
    Task<SettlementPreviewResponse?> GetPreviewAsync(Guid userId, DateOnly? date);

    /// <summary>Blocking status for the signed-in user (drives the frontend banner).</summary>
    Task<SettlementStatusResponse> GetStatusAsync(Guid userId);

    /// <summary>Closes the day. Cash variance must be exactly zero (FR-STL-005).</summary>
    Task<(SettlementResponse? Settlement, string? Error, string? Code)> SubmitAsync(
        SubmitSettlementRequest request, Guid userId);

    Task<(SettlementResponse? Settlement, bool NotFound, string? Error)> ApproveAsync(
        Guid id, string? note, Guid reviewedBy);

    Task<(SettlementResponse? Settlement, bool NotFound, string? Error)> RejectAsync(
        Guid id, RejectSettlementRequest request, Guid reviewedBy);

    /// <summary>Reopens an approved day; the only way past Gate B.</summary>
    Task<(SettlementResponse? Settlement, bool NotFound, string? Error)> ReopenAsync(
        Guid id, ReopenSettlementRequest request, Guid reviewedBy);

    Task<IEnumerable<SettlementResponse>> ListAsync(
        DateOnly? from, DateOnly? to, Guid? userId, string? status, Guid callerId, string role);

    Task<SettlementDetailResponse?> GetByIdAsync(Guid id, Guid callerId, string role);

    /// <summary>Owner-only "Selisih Kas" booking (FR-STL-009).</summary>
    Task<(bool Success, string? Error)> CreateAdjustmentAsync(CashAdjustmentRequest request, Guid createdBy);
}
