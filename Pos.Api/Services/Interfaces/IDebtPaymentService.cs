using Pos.Api.DTOs.DebtPayments;

namespace Pos.Api.Services.Interfaces;

public interface IDebtPaymentService
{
    Task<IEnumerable<DebtPaymentResponse>> GetAllAsync(DateOnly? date);
    Task<(DebtPaymentResponse? Payment, string? Error)> CreateAsync(CreateDebtPaymentRequest request, Guid createdBy);
}
