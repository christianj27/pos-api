using Microsoft.EntityFrameworkCore;
using Pos.Api.Data;
using Pos.Api.Data.Enums;
using Pos.Api.DTOs.Payments;
using Pos.Api.Models;
using Pos.Api.Services.Interfaces;

namespace Pos.Api.Services.Implementations;

public class PaymentService(AppDbContext db, ISettlementGuard guard) : IPaymentService
{
    public async Task<(PaymentResponse? Payment, string? Error)> AddPaymentAsync(
        Guid transactionId, CreatePaymentRequest request, Guid userId, string role)
    {
        var transaction = await db.Transactions
            .Include(t => t.Payments)
            .FirstOrDefaultAsync(t => t.Id == transactionId);

        if (transaction is null) return (null, "Transaction not found.");
        if (transaction.Status == TransactionStatus.Cancelled) return (null, "Transaksi sudah dibatalkan.");

        // Kurir/kasir may only record money on their own transactions; the owner may record on any.
        if (role is not "owner" && transaction.StaffId != userId)
            return (null, "Anda hanya dapat mencatat pembayaran untuk transaksi Anda sendiri.");

        // Collecting on an old invoice is a correction, not new work, so Gate A deliberately does not
        // apply — otherwise a blocked user could never account for the cash they collected (FR-STL-007).
        // Gate B still freezes a day the owner has approved.
        var transactionDayLock = await guard.EnsureDayWritableAsync(
            transaction.StaffId, WibTimeZone.ToWibDate(transaction.CreatedAt));
        if (transactionDayLock is not null) return (null, transactionDayLock);

        var businessDate = WibTimeZone.TodayWib();
        var dayLock = await guard.EnsureDayWritableAsync(userId, businessDate);
        if (dayLock is not null) return (null, dayLock);

        await guard.TouchDayAsync(userId, businessDate);

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
            ReferenceNo = request.ReferenceNo,
            CreatedBy = userId
        };
        db.Payments.Add(payment);

        transaction.PaidAmount += request.Amount;
        transaction.DebtAmount -= request.Amount;

        await db.SaveChangesAsync();

        return (new PaymentResponse(payment.Id, transactionId, payment.Amount,
            payment.Method.ToString().ToLower(), payment.ReferenceNo, payment.PaidAt), null);
    }
}
