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

        // 1. Transactions - cash_in/sale_payment + new_debt/debt_created
        var transactions = await db.Transactions
            .Include(t => t.Customer)
            .Include(t => t.Staff)
            .Where(t => t.Status == TransactionStatus.Completed && t.CreatedAt >= start && t.CreatedAt < end)
            .ToListAsync();

        foreach (var t in transactions)
        {
            if (t.PaidAmount > 0)
            {
                entries.Add(new CashFlowEntryResponse(
                    Guid.NewGuid(), t.Id, "cash_in", "sale_payment", t.PaidAmount,
                    $"Penjualan - {t.Customer?.Name ?? "Tanpa Pelanggan"}",
                    t.Id, t.Staff.Name, t.CreatedAt));
            }
            if (t.DebtAmount > 0)
            {
                entries.Add(new CashFlowEntryResponse(
                    Guid.NewGuid(), t.Id, "new_debt", "debt_created", t.DebtAmount,
                    $"Piutang Baru - {t.Customer?.Name ?? "Tanpa Pelanggan"}",
                    t.Id, t.Staff.Name, t.CreatedAt));
            }
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

        entries.Sort((a, b) => b.CreatedAt.CompareTo(a.CreatedAt));

        var totalCashIn = entries.Where(e => e.FlowType == "cash_in").Sum(e => e.Amount);
        var totalCashOut = entries.Where(e => e.FlowType == "cash_out").Sum(e => e.Amount);
        var totalNewDebt = entries.Where(e => e.FlowType == "new_debt").Sum(e => e.Amount);

        return new CashFlowSummaryResponse(totalCashIn, totalCashOut, totalCashIn - totalCashOut, totalNewDebt, entries);
    }
}
