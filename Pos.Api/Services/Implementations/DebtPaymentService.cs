using Microsoft.EntityFrameworkCore;
using Pos.Api.Data;
using Pos.Api.Data.Enums;
using Pos.Api.DTOs.DebtPayments;
using Pos.Api.Models;
using Pos.Api.Services.Interfaces;

namespace Pos.Api.Services.Implementations;

public class DebtPaymentService(AppDbContext db) : IDebtPaymentService
{
    public async Task<IEnumerable<DebtPaymentResponse>> GetAllAsync(DateOnly? date)
    {
        var filter = date ?? WibTimeZone.TodayWib();
        var (start, end) = WibTimeZone.GetUtcDayBounds(filter);

        return await db.DebtPayments
            .Include(dp => dp.Customer)
            .Include(dp => dp.Creator)
            .Where(dp => dp.CreatedAt >= start && dp.CreatedAt < end)
            .OrderByDescending(dp => dp.CreatedAt)
            .Select(dp => new DebtPaymentResponse(
                dp.Id, dp.CustomerId, dp.Customer.Name, dp.Amount,
                dp.Method.ToString().ToLower(), dp.ReferenceNo, dp.Note,
                dp.Creator.Name, dp.CreatedAt))
            .ToListAsync();
    }

    public async Task<(DebtPaymentResponse? Payment, string? Error)> CreateAsync(
        CreateDebtPaymentRequest request, Guid createdBy)
    {
        var customer = await db.Customers.FindAsync(request.CustomerId);
        if (customer is null || !customer.IsActive)
            return (null, "Pelanggan wajib dipilih.");

        if (!Enum.TryParse<PaymentMethod>(request.Method, ignoreCase: true, out var method))
            return (null, "Metode pembayaran wajib dipilih.");

        if (request.Amount <= 0)
            return (null, "Jumlah pembayaran wajib diisi dan harus positif.");

        var payment = new DebtPayment
        {
            CustomerId = request.CustomerId,
            Amount = request.Amount,
            Method = method,
            ReferenceNo = request.ReferenceNo,
            Note = request.Note,
            CreatedBy = createdBy
        };
        db.DebtPayments.Add(payment);
        await db.SaveChangesAsync();

        var creator = await db.Users.FindAsync(createdBy);
        return (new DebtPaymentResponse(
            payment.Id, customer.Id, customer.Name, payment.Amount,
            payment.Method.ToString().ToLower(), payment.ReferenceNo, payment.Note,
            creator!.Name, payment.CreatedAt), null);
    }
}
