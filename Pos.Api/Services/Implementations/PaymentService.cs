using Microsoft.EntityFrameworkCore;
using Pos.Api.Data;
using Pos.Api.Data.Enums;
using Pos.Api.DTOs.Payments;
using Pos.Api.Models;
using Pos.Api.Services.Interfaces;

namespace Pos.Api.Services.Implementations;

public class PaymentService(AppDbContext db) : IPaymentService
{
    public async Task<(PaymentResponse? Payment, string? Error)> AddPaymentAsync(
        Guid transactionId, CreatePaymentRequest request)
    {
        var transaction = await db.Transactions
            .Include(t => t.Payments)
            .FirstOrDefaultAsync(t => t.Id == transactionId);

        if (transaction is null) return (null, "Transaction not found.");
        if (transaction.Status == TransactionStatus.Cancelled) return (null, "Transaksi sudah dibatalkan.");

        if (!Enum.TryParse<PaymentMethod>(request.Method, ignoreCase: true, out var method))
            return (null, "Metode pembayaran wajib dipilih.");

        var alreadyPaid = transaction.Payments.Sum(p => p.Amount);
        var remaining = transaction.TotalAmount - alreadyPaid;

        if (request.Amount <= 0 || request.Amount > remaining)
            return (null, "Jumlah bayar melebihi sisa tagihan.");

        var payment = new Payment
        {
            TransactionId = transactionId,
            Amount = request.Amount,
            Method = method,
            ReferenceNo = request.ReferenceNo
        };
        db.Payments.Add(payment);

        transaction.PaidAmount += request.Amount;
        transaction.DebtAmount -= request.Amount;

        await db.SaveChangesAsync();

        return (new PaymentResponse(payment.Id, transactionId, payment.Amount,
            payment.Method.ToString().ToLower(), payment.ReferenceNo, payment.PaidAt), null);
    }
}
