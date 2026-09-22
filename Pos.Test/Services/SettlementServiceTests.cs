using Pos.Api.Data;
using Pos.Api.Data.Enums;
using Pos.Api.DTOs.Settlements;
using Pos.Api.Models;
using Pos.Api.Services;
using Pos.Api.Services.Implementations;
using Pos.Api.Services.Interfaces;
using Pos.Test.Helpers;

namespace Pos.Test.Services;

/// <summary>
/// Settlement lifecycle: preview maths, the zero-cash-variance rule, owner verification, reopening,
/// and the "Selisih Kas" adjustment that makes a genuinely short day closable (FR-STL-004..010).
/// </summary>
public class SettlementServiceTests
{
    private readonly AppDbContext _db;
    private readonly SettlementService _sut;

    private static readonly Guid OwnerId = Guid.NewGuid();
    private static readonly Guid KurirId = Guid.NewGuid();
    private static readonly Guid CustomerId = Guid.NewGuid();
    private static readonly Guid LocationId = Guid.NewGuid();
    private static readonly Guid ProductId = Guid.NewGuid();

    private static readonly DateOnly Today = WibTimeZone.TodayWib();

    public SettlementServiceTests()
    {
        _db = DbContextFactory.Create(Guid.NewGuid().ToString());
        var guard = DbContextFactory.CreateGuard(_db, Today.AddDays(-30));
        _sut = new SettlementService(_db, new DayTotalsCalculator(_db), guard);
        Seed();
    }

    private void Seed()
    {
        _db.Users.AddRange(
            new User { Id = OwnerId, Name = "Owner", Username = "owner", PasswordHash = "x", Role = UserRole.Owner, IsActive = true },
            new User { Id = KurirId, Name = "Kurir", Username = "kurir", PasswordHash = "x", Role = UserRole.Kurir, IsActive = true });
        _db.Customers.Add(new Customer { Id = CustomerId, Name = "Budi", IsActive = true });
        _db.Locations.Add(new Location { Id = LocationId, Name = "Gudang", Type = LocationType.Warehouse, IsActive = true });
        _db.Products.Add(new Product
        {
            Id = ProductId,
            Name = "Galon",
            Category = ProductCategory.Refillable,
            ProductionType = ProductionType.SelfProduced,
            Type = ProductType.Air,
            Unit = "galon",
            BasePrice = 5000m,
            IsActive = true
        });
        _db.SaveChanges();
    }

    /// <summary>Records cash collected by the kurir on the given business date.</summary>
    private void AddCashSale(decimal amount, Guid? collectorId = null, DateOnly? businessDate = null)
    {
        var collectedAt = DateTime.UtcNow;

        if (businessDate.HasValue)
        {
            var (dayStartUtc, _) = WibTimeZone.GetUtcDayBounds(businessDate.Value);
            collectedAt = dayStartUtc.AddHours(9); // mid-morning on that WIB day
        }

        var transaction = new Transaction
        {
            Id = Guid.NewGuid(),
            TransactionType = TransactionType.Delivery,
            CustomerId = CustomerId,
            StaffId = collectorId ?? KurirId,
            LocationId = LocationId,
            Status = TransactionStatus.Completed,
            PaymentMethod = PaymentMethod.Cash,
            TotalAmount = amount,
            PaidAmount = amount,
            DebtAmount = 0m,
            CreatedAt = collectedAt
        };
        _db.Transactions.Add(transaction);

        _db.Payments.Add(new Payment
        {
            Id = Guid.NewGuid(),
            TransactionId = transaction.Id,
            Amount = amount,
            Method = PaymentMethod.Cash,
            PaidAt = collectedAt,
            CreatedBy = collectorId ?? KurirId
        });

        _db.SaveChanges();
    }

    private Task<(SettlementResponse? Settlement, string? Error, string? Code)> SubmitAsync(
        decimal countedCash, DateOnly? businessDate = null, string? note = null) =>
        _sut.SubmitAsync(
            new SubmitSettlementRequest(businessDate ?? Today, countedCash, null, null, note),
            KurirId);

    // ── Preview ────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetPreviewAsync_ComputesExpectedCashFromTheLedger()
    {
        AddCashSale(120000m);

        var preview = await _sut.GetPreviewAsync(KurirId, Today);

        Assert.NotNull(preview);
        Assert.Equal(120000m, preview!.ExpectedCash);
        Assert.Equal(120000m, preview.CashIn);
        Assert.Equal(0m, preview.CashOut);
        Assert.Equal("cash", preview.Methods.First(m => m.Method == "cash").Method);
    }

    [Fact]
    public async Task GetPreviewAsync_ExcludesOtherUsersCash()
    {
        AddCashSale(120000m, collectorId: OwnerId);

        var preview = await _sut.GetPreviewAsync(KurirId, Today);

        Assert.Equal(0m, preview!.ExpectedCash);
    }

    [Fact]
    public async Task GetPreviewAsync_FutureDate_ReturnsNull()
    {
        Assert.Null(await _sut.GetPreviewAsync(KurirId, Today.AddDays(1)));
    }

    // ── Submit ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task SubmitAsync_CashMismatch_IsRejectedWithCashVarianceCode()
    {
        AddCashSale(100000m);

        var (settlement, error, code) = await SubmitAsync(countedCash: 90000m);

        Assert.Null(settlement);
        Assert.Equal("CASH_VARIANCE", code);
        Assert.Contains("10,000", error!);
        Assert.Empty(_db.DailySettlements.Where(s => s.UserId == KurirId));
    }

    [Fact]
    public async Task SubmitAsync_MatchingCash_SubmitsAndSnapshotsTheDay()
    {
        AddCashSale(100000m);

        var (settlement, error, code) = await SubmitAsync(countedCash: 100000m, note: "Tutup kas sore");

        Assert.Null(error);
        Assert.Null(code);
        Assert.NotNull(settlement);
        Assert.Equal("submitted", settlement!.Status);
        Assert.Equal(100000m, settlement.ExpectedCash);
        Assert.Equal(0m, settlement.CashVariance);

        var stored = _db.DailySettlements.Single(s => s.UserId == KurirId && s.BusinessDate == Today);
        Assert.Equal(SettlementStatus.Submitted, stored.Status);
        Assert.Equal("Tutup kas sore", stored.Note);
        Assert.NotNull(stored.SubmittedAt);
    }

    [Fact]
    public async Task SubmitAsync_WithoutActivity_IsRejected()
    {
        var (settlement, _, code) = await SubmitAsync(countedCash: 0m);

        Assert.Null(settlement);
        Assert.Equal("NO_ACTIVITY", code);
    }

    [Fact]
    public async Task SubmitAsync_UnsettledDay_CanBeClosedOnALaterDay()
    {
        AddCashSale(50000m, businessDate: Today.AddDays(-1));

        var (settlement, error, _) = await SubmitAsync(countedCash: 50000m, businessDate: Today.AddDays(-1));

        Assert.Null(error);
        Assert.Equal("submitted", settlement!.Status);
        Assert.Equal(Today.AddDays(-1), DateOnly.Parse(settlement.BusinessDate));
    }

    // ── Owner verification ─────────────────────────────────────────────────

    [Fact]
    public async Task ApproveAsync_SubmittedSettlement_SetsApprovedAndWritesAudit()
    {
        AddCashSale(100000m);
        var submitted = (await SubmitAsync(100000m)).Settlement!;

        var (approved, notFound, error) = await _sut.ApproveAsync(submitted.Id, null, OwnerId);

        Assert.False(notFound);
        Assert.Null(error);
        Assert.Equal("approved", approved!.Status);

        var audit = _db.AuditLogs.Single(a => a.EntityType == "settlement" && a.EntityId == submitted.Id);
        Assert.Equal("approve", audit.Action);
        Assert.Equal(OwnerId, audit.ActorId);
    }

    [Fact]
    public async Task ApproveAsync_StillOpenSettlement_IsRejected()
    {
        AddCashSale(100000m);
        _db.DailySettlements.Add(new DailySettlement
        {
            Id = Guid.NewGuid(), UserId = KurirId, BusinessDate = Today, Status = SettlementStatus.Open
        });
        _db.SaveChanges();

        var (approved, _, error) = await _sut.ApproveAsync(
            _db.DailySettlements.Single(s => s.UserId == KurirId).Id, null, OwnerId);

        Assert.Null(approved);
        Assert.NotNull(error);
    }

    [Fact]
    public async Task ApproveAsync_UnknownSettlement_ReportsNotFound()
    {
        var (settlement, notFound, _) = await _sut.ApproveAsync(Guid.NewGuid(), null, OwnerId);

        Assert.Null(settlement);
        Assert.True(notFound);
    }

    [Fact]
    public async Task RejectAsync_RequiresAReason()
    {
        AddCashSale(100000m);
        var submitted = (await SubmitAsync(100000m)).Settlement!;

        var (rejected, _, error) = await _sut.RejectAsync(submitted.Id, new RejectSettlementRequest("  "), OwnerId);

        Assert.Null(rejected);
        Assert.NotNull(error);
    }

    [Fact]
    public async Task RejectAsync_SubmittedSettlement_ReopensTheDayForTheUser()
    {
        // Gate A only ever fires for an earlier day, so the rejected day has to be yesterday's.
        var yesterday = Today.AddDays(-1);
        AddCashSale(100000m, businessDate: yesterday);
        var submitted = (await SubmitAsync(100000m, yesterday)).Settlement!;

        var (rejected, _, error) = await _sut.RejectAsync(
            submitted.Id, new RejectSettlementRequest("Kas kurang"), OwnerId);

        Assert.Null(error);
        Assert.Equal("rejected", rejected!.Status);
        Assert.Equal("Kas kurang", rejected.ReviewNote);

        // Gate A applies again: the rejected day blocks new work until it is fixed and approved.
        var guard = DbContextFactory.CreateGuard(_db, Today.AddDays(-30));
        Assert.NotNull(await guard.EnsureNewWorkAllowedAsync(KurirId));
    }

    [Fact]
    public async Task ReopenAsync_ApprovedSettlement_MakesTheDayWritableAgain()
    {
        AddCashSale(100000m);
        var submitted = (await SubmitAsync(100000m)).Settlement!;
        await _sut.ApproveAsync(submitted.Id, null, OwnerId);

        var guard = DbContextFactory.CreateGuard(_db, Today.AddDays(-30));
        Assert.NotNull(await guard.EnsureDayWritableAsync(KurirId, Today));

        var (reopened, _, error) = await _sut.ReopenAsync(
            submitted.Id, new ReopenSettlementRequest("Salah hitung"), OwnerId);

        Assert.Null(error);
        Assert.Equal("open", reopened!.Status);
        Assert.Null(await guard.EnsureDayWritableAsync(KurirId, Today));

        Assert.Contains(_db.AuditLogs, a => a.Action == "reopen" && a.Reason == "Salah hitung");
    }

    [Fact]
    public async Task SubmitAsync_AfterApproval_IsBlockedByGateB()
    {
        AddCashSale(100000m);
        var submitted = (await SubmitAsync(100000m)).Settlement!;
        await _sut.ApproveAsync(submitted.Id, null, OwnerId);

        var (settlement, _, code) = await SubmitAsync(100000m);

        Assert.Null(settlement);
        Assert.Equal(SettlementGuard.DayLocked, code);
    }

    // ── Selisih Kas adjustment (FR-STL-009) ────────────────────────────────

    [Fact]
    public async Task CreateAdjustmentAsync_ZeroVarianceCorrection_UnlocksTheClose()
    {
        AddCashSale(100000m);

        // The kurir physically holds 5,000 less than the system expects, so the close is refused.
        var (blocked, _, code) = await SubmitAsync(countedCash: 95000m);
        Assert.Null(blocked);
        Assert.Equal("CASH_VARIANCE", code);

        // The owner books the shortfall, which is the only way the day can legitimately reach zero.
        var (ok, error) = await _sut.CreateAdjustmentAsync(
            new CashAdjustmentRequest(KurirId, Today, -5000m, "Kas kurang, sudah ditelusuri"), OwnerId);

        Assert.True(ok);
        Assert.Null(error);

        var (settlement, submitError, _) = await SubmitAsync(countedCash: 95000m);

        Assert.Null(submitError);
        Assert.Equal("submitted", settlement!.Status);
        Assert.Equal(95000m, settlement.ExpectedCash);
    }

    [Fact]
    public async Task CreateAdjustmentAsync_ZeroAmount_IsRejected()
    {
        var (ok, error) = await _sut.CreateAdjustmentAsync(
            new CashAdjustmentRequest(KurirId, Today, 0m, "Tidak ada"), OwnerId);

        Assert.False(ok);
        Assert.NotNull(error);
    }

    [Fact]
    public async Task CreateAdjustmentAsync_RequiresAReason()
    {
        var (ok, error) = await _sut.CreateAdjustmentAsync(
            new CashAdjustmentRequest(KurirId, Today, -1000m, "  "), OwnerId);

        Assert.False(ok);
        Assert.NotNull(error);
    }

    [Fact]
    public async Task CreateAdjustmentAsync_OnAnApprovedDay_IsRejected()
    {
        AddCashSale(100000m);
        var submitted = (await SubmitAsync(100000m)).Settlement!;
        await _sut.ApproveAsync(submitted.Id, null, OwnerId);

        var (ok, error) = await _sut.CreateAdjustmentAsync(
            new CashAdjustmentRequest(KurirId, Today, -5000m, "Terlambat"), OwnerId);

        Assert.False(ok);
        Assert.NotNull(error);
    }

    [Fact]
    public async Task CreateAdjustmentAsync_WritesAnAuditEntry()
    {
        await _sut.CreateAdjustmentAsync(
            new CashAdjustmentRequest(KurirId, Today, 2500m, "Kas lebih"), OwnerId);

        var audit = _db.AuditLogs.Single(a => a.EntityType == "cash_adjustment");
        Assert.Equal("cash_adjustment", audit.Action);
        Assert.Equal("Kas lebih", audit.Reason);
        Assert.Equal(OwnerId, audit.ActorId);
    }

    // ── Listing / status ───────────────────────────────────────────────────

    [Fact]
    public async Task GetStatusAsync_ReportsThePendingDayForTheBanner()
    {
        _db.DailySettlements.Add(new DailySettlement
        {
            Id = Guid.NewGuid(), UserId = KurirId, BusinessDate = Today.AddDays(-1), Status = SettlementStatus.Submitted
        });
        _db.SaveChanges();

        var status = await _sut.GetStatusAsync(KurirId);

        Assert.True(status.Blocked);
        Assert.Equal(Today.AddDays(-1).ToString("yyyy-MM-dd"), status.BlockingBusinessDate);
        Assert.Equal("submitted", status.BlockingStatus);
    }

    [Fact]
    public async Task ListAsync_NonOwnerSeesOnlyTheirOwnRows()
    {
        _db.DailySettlements.AddRange(
            new DailySettlement { Id = Guid.NewGuid(), UserId = KurirId, BusinessDate = Today, Status = SettlementStatus.Open },
            new DailySettlement { Id = Guid.NewGuid(), UserId = OwnerId, BusinessDate = Today, Status = SettlementStatus.Open });
        _db.SaveChanges();

        var rows = (await _sut.ListAsync(null, null, null, null, KurirId, "kurir")).ToList();

        Assert.Single(rows);
        Assert.Equal(KurirId, rows[0].UserId);
    }

    [Fact]
    public async Task ListAsync_OwnerCanFilterByUserAndStatus()
    {
        _db.DailySettlements.AddRange(
            new DailySettlement { Id = Guid.NewGuid(), UserId = KurirId, BusinessDate = Today, Status = SettlementStatus.Submitted },
            new DailySettlement { Id = Guid.NewGuid(), UserId = OwnerId, BusinessDate = Today, Status = SettlementStatus.Open });
        _db.SaveChanges();

        var rows = (await _sut.ListAsync(null, null, KurirId, "submitted", OwnerId, "owner")).ToList();

        Assert.Single(rows);
        Assert.Equal("submitted", rows[0].Status);
    }

    [Fact]
    public async Task GetByIdAsync_NonOwnerCannotReadSomeoneElsesSettlement()
    {
        var id = Guid.NewGuid();
        _db.DailySettlements.Add(new DailySettlement
        {
            Id = id, UserId = OwnerId, BusinessDate = Today, Status = SettlementStatus.Open
        });
        _db.SaveChanges();

        Assert.Null(await _sut.GetByIdAsync(id, KurirId, "kurir"));
        Assert.NotNull(await _sut.GetByIdAsync(id, OwnerId, "owner"));
    }
}
