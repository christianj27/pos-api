using Pos.Api.DTOs.Transactions;

namespace Pos.Api.Services.Interfaces;

public interface ITransactionService
{
    Task<IEnumerable<TransactionResponse>> GetAllAsync(Guid userId, string role, DateOnly? date);
    Task<TransactionDetailResponse?> GetByIdAsync(Guid id, Guid userId, string role);
    Task<(TransactionDetailResponse? Transaction, string? Error)> CreateAsync(CreateTransactionRequest request, Guid staffId, string role);
    Task<(bool Success, string? Error)> UpdateStatusAsync(Guid id, UpdateTransactionStatusRequest request, Guid userId, string role);
}
