using Pos.Api.Data;
using Pos.Api.Data.Enums;
using Pos.Api.Models;
using Pos.Api.Services;
using Pos.Api.Services.Implementations;
using Pos.Api.Services.Interfaces;
using Pos.Test.Helpers;

namespace Pos.Test.Services;

/// <summary>
/// Gate A (a pending earlier day blocks new work) and Gate B (an approved day is frozen) — FR-STL-002/003.
/// </summary>
public class SettlementGuardTests
{
    private readonly AppDbContext _db;

    private static readonly Guid OwnerId = Guid.NewGuid();
    private static readonly Guid KurirId = Guid.NewGuid();
    private static readonly DateOnly Today = WibTimeZone.TodayWib();

    public SettlementGuardTests()
    {
        _db = DbContextFactory.Create(Guid.NewGuid().ToString());
        Seed();
    }

    private void Seed()
    {
        _db.Users.AddRange(
            new User { Id = OwnerId, Name = "Owner", Username = "owner", PasswordHash = "x", Role = UserRole.Owner, IsActive = true },
            new User { Id = KurirId, Name = "Kurir", Username = "kurir", PasswordHash = "x", Role = UserRole.Kurir, IsActive = true });
        _db.SaveChanges();
    }

    private ISettlementGuard CreateGuard(DateOnly? enforcementStart = null) =>
        DbContextFactory.CreateGuard(_db, enforcementStart ?? Today.AddDays(-30));

    private void AddSettlement(Guid userId, DateOnly date, SettlementStatus status)
    {
        _db.DailySettlements.Add(new DailySettlement
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            BusinessDate = date,
            Status = status
        });
        _db.SaveChanges();
    }

    [Fact]
    public async Task GetBlockAsync_NoPendingDay_ReturnsNull()
    {
        var guard = CreateGuard();

        Assert.Null(await guard.GetBlockAsync(KurirId));
    }

    [Theory]
    [InlineData(SettlementStatus.Open)]
    [InlineData(SettlementStatus.Submitted)]
    [InlineData(SettlementStatus.Rejected)]
    public async Task GetBlockAsync_UnapprovedPreviousDay_Blocks(SettlementStatus status)
    {
        AddSettlement(KurirId, Today.AddDays(-1), status);

        var block = await CreateGuard().GetBlockAsync(KurirId);

        Assert.NotNull(block);
        Assert.Equal(SettlementGuard.UnsettledPreviousDay, block!.Code);
        Assert.Equal(Today.AddDays(-1), block.BusinessDate);
    }

    [Fact]
    public async Task GetBlockAsync_OwnerIsNeverBlocked()
    {
        AddSettlement(OwnerId, Today.AddDays(-1), SettlementStatus.Open);

        Assert.Null(await CreateGuard().GetBlockAsync(OwnerId));
    }

    [Fact]
    public async Task GetBlockAsync_ApprovedPreviousDay_DoesNotBlock()
    {
        AddSettlement(KurirId, Today.AddDays(-1), SettlementStatus.Approved);

        Assert.Null(await CreateGuard().GetBlockAsync(KurirId));
    }

    [Fact]
    public async Task GetBlockAsync_PendingToday_DoesNotBlock()
    {
        AddSettlement(KurirId, Today, SettlementStatus.Open);

        Assert.Null(await CreateGuard().GetBlockAsync(KurirId));
    }

    [Fact]
    public async Task GetBlockAsync_DayBeforeEnforcementStart_IsIgnored()
    {
        AddSettlement(KurirId, Today.AddDays(-3), SettlementStatus.Open);

        // Enforcement starts today, so the older pending day must not a block a user over existing history.
        Assert.Null(await CreateGuard(Today).GetBlockAsync(KurirId));
    }

    [Fact]
    public async Task GetBlockAsync_ReportsTheOldestPendingDay()
    {
        AddSettlement(KurirId, Today.AddDays(-1), SettlementStatus.Rejected);
        AddSettlement(KurirId, Today.AddDays(-2), SettlementStatus.Open);

        var block = await CreateGuard().GetBlockAsync(KurirId);

        Assert.NotNull(block);
        Assert.Equal(Today.AddDays(-2), block!.BusinessDate);
        Assert.Equal("open", block.Status);
    }

    [Fact]
    public async Task EnsureNewWorkAllowedAsync_ReturnsTheBlockMessage()
    {
        AddSettlement(KurirId, Today.AddDays(-1), SettlementStatus.Open);

        var message = await CreateGuard().EnsureNewWorkAllowedAsync(KurirId);

        Assert.NotNull(message);
        Assert.Contains("settlement", message!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task EnsureDayWritableAsync_ApprovedDay_ReturnsMessage()
    {
        AddSettlement(KurirId, Today.AddDays(-1), SettlementStatus.Approved);

        Assert.NotNull(await CreateGuard().EnsureDayWritableAsync(KurirId, Today.AddDays(-1)));
    }

    [Fact]
    public async Task EnsureDayWritableAsync_OpenDay_ReturnsNull()
    {
        AddSettlement(KurirId, Today.AddDays(-1), SettlementStatus.Open);

        Assert.Null(await CreateGuard().EnsureDayWritableAsync(KurirId, Today.AddDays(-1)));
    }

    [Fact]
    public async Task TouchDayAsync_CreatesOneOpenRowPerUserAndDay()
    {
        var guard = CreateGuard();

        await guard.TouchDayAsync(KurirId, Today);
        _db.SaveChanges();
        await guard.TouchDayAsync(KurirId, Today);
        _db.SaveChanges();

        var rows = _db.DailySettlements.Where(s => s.UserId == KurirId).ToList();
        Assert.Single(rows);
        Assert.Equal(SettlementStatus.Open, rows[0].Status);
        Assert.Equal(Today, rows[0].BusinessDate);
    }
}
