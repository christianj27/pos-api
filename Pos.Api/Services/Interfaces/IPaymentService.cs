using Pos.Api.DTOs.Payments;

namespace Pos.Api.Services.Interfaces;

public interface IPaymentService
{
    /// <summary>
    /// Records further payment on a transaction. The owner may record on any transaction; kurir/kasir
    /// only on transactions they created. The caller is stored as the collector so the cash is
    /// attributed to the right daily settlement.
    /// </summary>
    Task<(PaymentResponse? Payment, string? Error)> AddPaymentAsync(
        Guid transactionId, CreatePaymentRequest request, Guid userId, string role);
}
