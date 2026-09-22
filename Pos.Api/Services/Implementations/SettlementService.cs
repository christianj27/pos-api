using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Pos.Api.Data;
using Pos.Api.Data.Enums;
using Pos.Api.DTOs.Settlements;
using Pos.Api.Models;
using Pos.Api.Services.Interfaces;

namespace Pos.Api.Services.Implementations;

public class SettlementService(
    AppDbContext db,
    DayTotalsCalculator calculator,
    ISettlementGuard guard) : ISettlementService
{
    public async Task<SettlementPreviewResponse?> GetPreviewAsync(Guid userId, DateOnly? date)
    {
        var today = WibTimeZone.TodayWib();
        var businessDate = date ?? today;
        if (businessDate > today) return null;

        var settlement = await db.DailySettlements
            .Include(s => s.MethodLines)
            .Include(s => s.StockLines)
            .FirstOrDefaultAsync(s => s.UserId == userId && s.BusinessDate == businessDate);

        var totals = await calculator.GetDayTotalsAsync(userId, businessDate);
        var vehicle = await GetVehicleAsync(userId);
        var stocks = vehicle is null
            ? []
            : await calculator.GetStockTotalsAsync(userId, vehicle.Id, businessDate);

        var productIds = stocks.Select(s => s.ProductId).ToList();
        var products = await db.Products
            .Where(p => productIds.Contains(p.Id))
            .Select(p => new { p.Id, p.Name, p.Unit })
            .ToDictionaryAsync(p => p.Id, p => new { p.Name, p.Unit });

        var methods = totals.Methods
            .Select(m =>
            {
                var stored = settlement?.MethodLines.FirstOrDefault(l => l.Method == m.Method);
                var counted = m.Method == PaymentMethod.Cash
                    ? settlement?.CountedCash ?? m.ExpectedAmount
                    : stored?.CountedAmount ?? m.ExpectedAmount;

                return new SettlementMethodLineResponse(
                    stored?.Id ?? Guid.Empty,
                    m.Method.ToString().ToLower(),
                    m.ExpectedAmount,
                    counted,
                    counted - m.ExpectedAmount);
            })
            .ToList();

        var stockLines = stocks.Select(s =>
        {
            var stored = settlement?.StockLines.FirstOrDefault(l => l.ProductId == s.ProductId);
            var meta = products.GetValueOrDefault(s.ProductId);

            return new SettlementStockLineResponse(
                stored?.Id ?? Guid.Empty,
                s.ProductId,
                meta?.Name ?? string.Empty,
                meta?.Unit ?? string.Empty,
                s.IsRefillable,
                s.ExpectedFilled,
                stored?.CountedFilled ?? s.ExpectedFilled,
                s.ExpectedEmpty,
                stored?.CountedEmpty ?? s.ExpectedEmpty,
                s.ContainersOut,
                s.ContainersReturned);
        }).ToList();

        return new SettlementPreviewResponse(
            businessDate.ToString("yyyy-MM-dd"),
            (settlement?.Status ?? SettlementStatus.Open).ToString().ToLower(),
            settlement?.Id,
            totals.ExpectedCash,
            settlement?.CountedCash ?? totals.ExpectedCash,
            (settlement?.CountedCash ?? totals.ExpectedCash) - totals.ExpectedCash,
            totals.CashIn,
            totals.CashOut,
            totals.CashAdjustments,
            totals.TransferExpected,
            totals.QrisExpected,
            totals.NewDebtTotal,
            totals.DebtPaymentTotal,
            vehicle?.Id,
            vehicle?.Name,
            methods,
            stockLines);
    }

    public async Task<SettlementStatusResponse> GetStatusAsync(Guid userId)
    {
        var block = await guard.GetBlockAsync(userId);

        return block is null
            ? new SettlementStatusResponse(false, null, null, null, null)
            : new SettlementStatusResponse(
                true,
                block.BusinessDate.ToString("yyyy-MM-dd"),
                block.Status,
                block.SettlementId,
                block.Message);
    }

    public async Task<(SettlementResponse? Settlement, string? Error, string? Code)> SubmitAsync(
        SubmitSettlementRequest request, Guid userId)
    {
        var today = WibTimeZone.TodayWib();
        var businessDate = request.BusinessDate ?? today;

        if (businessDate > today)
            return (null, "Tanggal settlement tidak boleh di masa depan.", "INVALID_DATE");

        var totals = await calculator.GetDayTotalsAsync(userId, businessDate);
        var vehicle = await GetVehicleAsync(userId);
        var stocks = vehicle is null
            ? []
            : await calculator.GetStockTotalsAsync(userId, vehicle.Id, businessDate);

        var hasActivity = totals.Methods.Any(m => m.ExpectedAmount != 0)
                          || totals.CashIn != 0
                          || totals.CashOut != 0
                          || totals.CashAdjustments != 0
                          || totals.NewDebtTotal != 0
                          || totals.DebtPaymentTotal != 0
                          || stocks.Count > 0;

        var settlement = await db.DailySettlements
            .Include(s => s.MethodLines)
            .Include(s => s.StockLines)
            .FirstOrDefaultAsync(s => s.UserId == userId && s.BusinessDate == businessDate);

        if (settlement is null)
        {
            if (!hasActivity)
                return (null, "Tidak ada aktivitas pada tanggal tersebut, jadi tidak perlu settlement.", "NO_ACTIVITY");

            settlement = new DailySettlement { UserId = userId, BusinessDate = businessDate };
            db.DailySettlements.Add(settlement);
        }
        else if (settlement.Status == SettlementStatus.Approved)
        {
            return (null,
                $"Settlement {SettlementGuard.FormatDateId(businessDate)} sudah disetujui owner dan tidak bisa diajukan ulang.",
                SettlementGuard.DayLocked);
        }

        // Cash variance must be exactly zero (FR-STL-005). A genuinely short/over amount is booked by the
        // owner as a "Selisih Kas" cash adjustment instead of being hidden inside the transactions.
        var variance = request.CountedCash - totals.ExpectedCash;
        if (variance != 0)
        {
            var direction = variance > 0 ? "lebih" : "kurang";
            return (null,
                $"Kas fisik {Money(request.CountedCash)} {direction} {Money(Math.Abs(variance))} dari perhitungan sistem " +
                $"({Money(totals.ExpectedCash)}). Perbaiki transaksinya, atau minta owner mencatat Selisih Kas.",
                "CASH_VARIANCE");
        }

        settlement.VehicleLocationId = vehicle?.Id;
        settlement.ExpectedCash = totals.ExpectedCash;
        settlement.CountedCash = request.CountedCash;
        settlement.CashVariance = 0m;
        settlement.CashIn = totals.CashIn;
        settlement.CashOut = totals.CashOut;
        settlement.CashAdjustmentTotal = totals.CashAdjustments;
        settlement.TransferExpected = totals.TransferExpected;
        settlement.QrisExpected = totals.QrisExpected;
        settlement.NewDebtTotal = totals.NewDebtTotal;
        settlement.DebtPaymentTotal = totals.DebtPaymentTotal;
        settlement.Note = request.Note;
        settlement.Status = SettlementStatus.Submitted;
        settlement.SubmittedAt = DateTime.UtcNow;
        settlement.ReviewedAt = null;
        settlement.ReviewedBy = null;
        settlement.ReviewNote = null;

        var methodRequests = (request.MethodLines ?? []).ToList();

        db.DailySettlementMethodLines.RemoveRange(settlement.MethodLines);
        settlement.MethodLines.Clear();
        foreach (var method in totals.Methods)
        {
            var counted = method.Method == PaymentMethod.Cash
                ? request.CountedCash
                : TryGetMethodCount(methodRequests, method.Method) ?? method.ExpectedAmount;

            settlement.MethodLines.Add(new DailySettlementMethodLine
            {
                Method = method.Method,
                ExpectedAmount = method.ExpectedAmount,
                CountedAmount = counted,
                Variance = counted - method.ExpectedAmount
            });
        }

        var stockRequests = (request.StockLines ?? []).ToList();

        db.DailySettlementStockLines.RemoveRange(settlement.StockLines);
        settlement.StockLines.Clear();
        foreach (var stock in stocks)
        {
            var counted = stockRequests.FirstOrDefault(r => r.ProductId == stock.ProductId);

            settlement.StockLines.Add(new DailySettlementStockLine
            {
                ProductId = stock.ProductId,
                ExpectedFilled = stock.ExpectedFilled,
                CountedFilled = counted?.CountedFilled ?? stock.ExpectedFilled,
                ExpectedEmpty = stock.ExpectedEmpty,
                CountedEmpty = counted?.CountedEmpty ?? stock.ExpectedEmpty,
                ContainersOut = stock.ContainersOut,
                ContainersReturned = stock.ContainersReturned
            });
        }

        await db.SaveChangesAsync();

        return (Map(settlement, await GetUserNameAsync(userId)), null, null);
    }

    public async Task<(SettlementResponse? Settlement, bool NotFound, string? Error)> ApproveAsync(
        Guid id, string? note, Guid reviewedBy)
    {
        var settlement = await LoadAsync(id);
        if (settlement is null) return (null, true, null);

        if (settlement.Status != SettlementStatus.Submitted)
            return (null, false, "Hanya settlement yang sudah diajukan yang dapat disetujui.");

        settlement.Status = SettlementStatus.Approved;
        settlement.ReviewedAt = DateTime.UtcNow;
        settlement.ReviewedBy = reviewedBy;
        settlement.ReviewNote = note;

        db.AuditLogs.Add(new AuditLog
        {
            EntityType = "settlement",
            EntityId = settlement.Id,
            Action = "approve",
            Reason = string.IsNullOrWhiteSpace(note) ? "Disetujui owner." : note.Trim(),
            ActorId = reviewedBy
        });

        await db.SaveChangesAsync();

        return (Map(settlement, settlement.User.Name), false, null);
    }

    public async Task<(SettlementResponse? Settlement, bool NotFound, string? Error)> RejectAsync(
        Guid id, RejectSettlementRequest request, Guid reviewedBy)
    {
        if (string.IsNullOrWhiteSpace(request.Reason))
            return (null, false, "Alasan penolakan wajib diisi.");

        var settlement = await LoadAsync(id);
        if (settlement is null) return (null, true, null);

        if (settlement.Status != SettlementStatus.Submitted)
            return (null, false, "Hanya settlement yang sudah diajukan yang dapat ditolak.");

        settlement.Status = SettlementStatus.Rejected;
        settlement.ReviewedAt = DateTime.UtcNow;
        settlement.ReviewedBy = reviewedBy;
        settlement.ReviewNote = request.Reason.Trim();

        db.AuditLogs.Add(new AuditLog
        {
            EntityType = "settlement",
            EntityId = settlement.Id,
            Action = "reject",
            Reason = request.Reason.Trim(),
            ActorId = reviewedBy
        });

        await db.SaveChangesAsync();

        return (Map(settlement, settlement.User.Name), false, null);
    }

    public async Task<(SettlementResponse? Settlement, bool NotFound, string? Error)> ReopenAsync(
        Guid id, ReopenSettlementRequest request, Guid reviewedBy)
    {
        if (string.IsNullOrWhiteSpace(request.Reason))
            return (null, false, "Alasan membuka kembali wajib diisi.");

        var settlement = await LoadAsync(id);
        if (settlement is null) return (null, true, null);

        if (settlement.Status != SettlementStatus.Approved)
            return (null, false, "Hanya settlement yang sudah disetujui yang dapat dibuka kembali.");

        settlement.Status = SettlementStatus.Open;
        settlement.ReviewedAt = null;
        settlement.ReviewedBy = null;
        settlement.ReviewNote = request.Reason.Trim();
        settlement.SubmittedAt = null;

        db.AuditLogs.Add(new AuditLog
        {
            EntityType = "settlement",
            EntityId = settlement.Id,
            Action = "reopen",
            Reason = request.Reason.Trim(),
            ActorId = reviewedBy
        });

        await db.SaveChangesAsync();

        return (Map(settlement, settlement.User.Name), false, null);
    }

    public async Task<IEnumerable<SettlementResponse>> ListAsync(
        DateOnly? from, DateOnly? to, Guid? userId, string? status, Guid callerId, string role)
    {
        var q = db.DailySettlements
            .Include(s => s.User)
            .Include(s => s.Reviewer)
            .AsQueryable();

        if (role != "owner")
            q = q.Where(s => s.UserId == callerId);
        else if (userId.HasValue)
            q = q.Where(s => s.UserId == userId.Value);

        if (from.HasValue) q = q.Where(s => s.BusinessDate >= from.Value);
        if (to.HasValue) q = q.Where(s => s.BusinessDate <= to.Value);

        if (!string.IsNullOrWhiteSpace(status) &&
            Enum.TryParse<SettlementStatus>(status, ignoreCase: true, out var parsed))
            q = q.Where(s => s.Status == parsed);

        var rows = await q
            .OrderByDescending(s => s.BusinessDate)
            .ThenBy(s => s.User.Name)
            .ToListAsync();

        return rows.Select(s => Map(s, s.User.Name)).ToList();
    }

    public async Task<SettlementDetailResponse?> GetByIdAsync(Guid id, Guid callerId, string role)
    {
        var settlement = await db.DailySettlements
            .Include(s => s.User)
            .Include(s => s.Reviewer)
            .Include(s => s.MethodLines)
            .Include(s => s.StockLines).ThenInclude(l => l.Product)
            .FirstOrDefaultAsync(s => s.Id == id);

        if (settlement is null) return null;
        if (role != "owner" && settlement.UserId != callerId) return null;

        var audit = await db.AuditLogs
            .Include(a => a.Actor)
            .Where(a => a.EntityType == "settlement" && a.EntityId == settlement.Id)
            .OrderByDescending(a => a.CreatedAt)
            .Select(a => new AuditLogResponse(a.Id, a.Action, a.Reason, a.ChangesJson, a.Actor.Name, a.CreatedAt))
            .ToListAsync();

        var methods = settlement.MethodLines
            .OrderBy(l => l.Method)
            .Select(l => new SettlementMethodLineResponse(
                l.Id, l.Method.ToString().ToLower(), l.ExpectedAmount, l.CountedAmount, l.Variance))
            .ToList();

        var stocks = settlement.StockLines
            .Select(l => new SettlementStockLineResponse(
                l.Id, l.ProductId, l.Product.Name, l.Product.Unit,
                l.Product.Category == ProductCategory.Refillable,
                l.ExpectedFilled, l.CountedFilled, l.ExpectedEmpty, l.CountedEmpty,
                l.ContainersOut, l.ContainersReturned))
            .ToList();

        return new SettlementDetailResponse(Map(settlement, settlement.User.Name), methods, stocks, audit);
    }

    public async Task<(bool Success, string? Error)> CreateAdjustmentAsync(
        CashAdjustmentRequest request, Guid createdBy)
    {
        if (request.Amount == 0m)
            return (false, "Jumlah selisih kas tidak boleh nol.");

        if (string.IsNullOrWhiteSpace(request.Reason))
            return (false, "Alasan selisih kas wajib diisi.");

        if (request.BusinessDate > WibTimeZone.TodayWib())
            return (false, "Tanggal selisih kas tidak boleh di masa depan.");

        var user = await db.Users.FindAsync(request.UserId);
        if (user is null) return (false, "Pengguna tidak ditemukan.");

        var locked = await db.DailySettlements.AnyAsync(s =>
            s.UserId == request.UserId &&
            s.BusinessDate == request.BusinessDate &&
            s.Status == SettlementStatus.Approved);

        if (locked)
            return (false, "Tanggal tersebut sudah disetujui owner. Buka kembali settlement sebelum mencatat selisih kas.");

        var adjustment = new CashAdjustment
        {
            UserId = request.UserId,
            BusinessDate = request.BusinessDate,
            Amount = request.Amount,
            Reason = request.Reason.Trim(),
            CreatedBy = createdBy
        };
        db.CashAdjustments.Add(adjustment);

        db.AuditLogs.Add(new AuditLog
        {
            EntityType = "cash_adjustment",
            EntityId = adjustment.Id,
            Action = "cash_adjustment",
            Reason = adjustment.Reason,
            ChangesJson = $$"""{"userId":"{{user.Id}}","businessDate":"{{adjustment.BusinessDate:yyyy-MM-dd}}","amount":{{adjustment.Amount.ToString(CultureInfo.InvariantCulture)}}}""",
            ActorId = createdBy
        });

        await db.SaveChangesAsync();
        return (true, null);
    }

    // ── helpers ────────────────────────────────────────────────────────────

    private async Task<DailySettlement?> LoadAsync(Guid id) =>
        await db.DailySettlements
            .Include(s => s.User)
            .Include(s => s.Reviewer)
            .FirstOrDefaultAsync(s => s.Id == id);

    private async Task<Location?> GetVehicleAsync(Guid userId) =>
        await db.Locations
            .Where(l => l.AssignedTo == userId && l.Type == LocationType.Vehicle && l.IsActive)
            .Select(l => new Location { Id = l.Id, Name = l.Name, Type = l.Type })
            .FirstOrDefaultAsync();

    private async Task<string> GetUserNameAsync(Guid userId) =>
        await db.Users.Where(u => u.Id == userId).Select(u => u.Name).FirstOrDefaultAsync() ?? string.Empty;

    private static decimal? TryGetMethodCount(
        IEnumerable<SettlementMethodLineRequest> requests, PaymentMethod method) =>
        requests
            .Where(r => Enum.TryParse<PaymentMethod>(r.Method, ignoreCase: true, out var parsed) && parsed == method)
            .Select(r => (decimal?)r.CountedAmount)
            .FirstOrDefault();

    private static string Money(decimal value) =>
        "Rp " + value.ToString("N0", CultureInfo.InvariantCulture);

    private static SettlementResponse Map(DailySettlement s, string userName) =>
        new(
            s.Id,
            s.UserId,
            userName,
            s.BusinessDate.ToString("yyyy-MM-dd"),
            s.Status.ToString().ToLower(),
            s.ExpectedCash,
            s.CountedCash,
            s.CashVariance,
            s.TransferExpected,
            s.QrisExpected,
            s.NewDebtTotal,
            s.DebtPaymentTotal,
            s.Note,
            s.SubmittedAt,
            s.ReviewedAt,
            s.Reviewer?.Name,
            s.ReviewNote,
            s.CreatedAt);
}
