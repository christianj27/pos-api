using Microsoft.EntityFrameworkCore;
using Pos.Api.Data;
using Pos.Api.Data.Enums;
using Pos.Api.DTOs.CashFlow;
using Pos.Api.Services.Interfaces;

namespace Pos.Api.Services.Implementations;

public class CashFlowService(AppDbContext db) : ICashFlowService
{
    public Task<CashFlowSummaryResponse> GetCashFlowAsync(DateOnly date)
        => GetCashFlowRangeAsync(date, date);

    public async Task<CashFlowSummaryResponse> GetCashFlowRangeAsync(DateOnly startDate, DateOnly endDate)
    {
        var (start, _) = WibTimeZone.GetUtcDayBounds(startDate);
        var (_, end)   = WibTimeZone.GetUtcDayBounds(endDate);
        var entries = new List<CashFlowEntryResponse>();

        // 1a. Payments - cash_in/sale_payment, dated by when the money was actually collected
        //     (Payment.PaidAt) instead of when the invoice was raised. Otherwise an instalment recorded
        //     later would rewrite a past day's cash — the settlement feature freezes those days (Gate B).
        var payments = await db.Payments
            .Include(p => p.Transaction).ThenInclude(t => t.Customer)
            .Include(p => p.Transaction).ThenInclude(t => t.Staff)
            .Include(p => p.Creator)
            .Where(p => p.PaidAt >= start && p.PaidAt < end
                        && p.Transaction.Status != TransactionStatus.Cancelled)
            .ToListAsync();

        foreach (var p in payments)
        {
            entries.Add(new CashFlowEntryResponse(
                Guid.NewGuid(), p.Id, "cash_in", "sale_payment", p.Amount,
                $"Penjualan - {p.Transaction.Customer?.Name ?? "Tanpa Pelanggan"}",
                p.TransactionId, p.Creator?.Name ?? p.Transaction.Staff.Name, p.PaidAt));
        }

        // 1b. Transactions - new_debt/debt_created
        var transactions = await db.Transactions
            .Include(t => t.Customer)
            .Include(t => t.Staff)
            .Where(t => t.Status == TransactionStatus.Completed && t.CreatedAt >= start && t.CreatedAt < end
                        && t.DebtAmount > 0)
            .ToListAsync();

        foreach (var t in transactions)
        {
            entries.Add(new CashFlowEntryResponse(
                Guid.NewGuid(), t.Id, "new_debt", "debt_created", t.DebtAmount,
                $"Piutang Baru - {t.Customer?.Name ?? "Tanpa Pelanggan"}",
                t.Id, t.Staff.Name, t.CreatedAt));
        }

        // 2. DebtPayments - cash_in/debt_payment
        var debtPayments = await db.DebtPayments
            .Include(dp => dp.Transaction)
            .Include(dp => dp.Customer)
            .Include(dp => dp.Creator)
            .Where(dp => dp.CreatedAt >= start && dp.CreatedAt < end && (dp.Transaction == null || dp.Transaction.Status != TransactionStatus.Cancelled))
            .ToListAsync();

        foreach (var dp in debtPayments)
        {
            entries.Add(new CashFlowEntryResponse(
                Guid.NewGuid(), dp.Id, "cash_in", "debt_payment", dp.Amount,
                $"Pembayaran Hutang - {dp.Customer.Name}",
                dp.Id, dp.Creator.Name, dp.CreatedAt));
        }

        // 3. StockMovements with purchase_cost - cash_out/stock_purchase
        var purchases = await db.StockMovements
            .Include(m => m.Product)
            .Include(m => m.Creator)
            .Where(m => m.PurchaseCost != null && m.PurchaseCost > 0 && m.CreatedAt >= start && m.CreatedAt < end && !m.IsReversed)
            .ToListAsync();

        foreach (var m in purchases)
        {
            entries.Add(new CashFlowEntryResponse(
                Guid.NewGuid(), m.Id, "cash_out", "stock_purchase", m.PurchaseCost!.Value,
                $"Pembelian Stok - {m.Product.Name}",
                m.Id, m.Creator.Name, m.CreatedAt));
        }

        // 4. Expenses (operational, owner-recorded) - cash_out/operational_expense
        //    Filtered by the business date (expense_date) rather than created_at so a back-dated
        //    expense lands on the day it belongs to.
        var expenses = await db.Expenses
            .Include(x => x.Creator)
            .Where(x => x.ExpenseDate >= startDate && x.ExpenseDate <= endDate)
            .ToListAsync();

        foreach (var x in expenses)
        {
            // Keep the entry on its business date while preserving the recording time-of-day (WIB).
            var (dayStartUtc, _) = WibTimeZone.GetUtcDayBounds(x.ExpenseDate);
            var occurredAt = dayStartUtc.Add(WibTimeZone.ToWib(x.CreatedAt).TimeOfDay);

            entries.Add(new CashFlowEntryResponse(
                Guid.NewGuid(), x.Id, "cash_out", "operational_expense", x.Amount,
                $"{x.Category.ToLabel()} - {x.Description}",
                x.Id, x.Creator.Name, occurredAt));
        }

        // 5. Cash adjustments ("Selisih Kas", FR-STL-009) - owner-booked correction for money that is
        //    genuinely short (negative → cash_out) or over (positive → cash_in).
        var adjustments = await db.CashAdjustments
            .Include(a => a.User)
            .Include(a => a.Creator)
            .Where(a => a.BusinessDate >= startDate && a.BusinessDate <= endDate)
            .ToListAsync();

        foreach (var a in adjustments)
        {
            var (dayStartUtc, _) = WibTimeZone.GetUtcDayBounds(a.BusinessDate);
            var occurredAt = dayStartUtc.Add(WibTimeZone.ToWib(a.CreatedAt).TimeOfDay);

            entries.Add(new CashFlowEntryResponse(
                Guid.NewGuid(), a.Id,
                a.Amount >= 0 ? "cash_in" : "cash_out", "cash_variance", Math.Abs(a.Amount),
                $"Selisih Kas - {a.User.Name}: {a.Reason}",
                a.Id, a.Creator.Name, occurredAt));
        }

        entries.Sort((a, b) => b.CreatedAt.CompareTo(a.CreatedAt));

        var totalCashIn = entries.Where(e => e.FlowType == "cash_in").Sum(e => e.Amount);
        var totalCashOut = entries.Where(e => e.FlowType == "cash_out").Sum(e => e.Amount);
        var totalNewDebt = entries.Where(e => e.FlowType == "new_debt").Sum(e => e.Amount);

        return new CashFlowSummaryResponse(totalCashIn, totalCashOut, totalCashIn - totalCashOut, totalNewDebt, entries);
    }
}
