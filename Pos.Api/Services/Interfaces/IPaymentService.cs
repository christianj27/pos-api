using Pos.Api.DTOs.Payments;

namespace Pos.Api.Services.Interfaces;

public interface IPaymentService
{
    Task<(PaymentResponse? Payment, string? Error)> AddPaymentAsync(Guid transactionId, CreatePaymentRequest request);
}
