using Pos.Api.Data;
using Pos.Api.Data.Enums;
using Pos.Api.Models;
using Pos.Api.Services.Implementations;
using Pos.Test.Helpers;

namespace Pos.Test.Services;

public class DashboardServiceTests
{
    private readonly AppDbContext _db;
    private readonly DashboardService _sut;

    private static readonly Guid OwnerId = Guid.NewGuid();
    private static readonly Guid KasirId = Guid.NewGuid();
    private static readonly Guid KurirId = Guid.NewGuid();
    private static readonly Guid CustomerId = Guid.NewGuid();
    private static readonly Guid WarehouseId = Guid.NewGuid();
    private static readonly Guid VehicleId = Guid.NewGuid();
    private static readonly Guid ProductId = Guid.NewGuid();

    private static readonly DateOnly Today = Pos.Api.Services.WibTimeZone.TodayWib();

    public DashboardServiceTests()
    {
        _db = DbContextFactory.Create(Guid.NewGuid().ToString());
        _sut = new DashboardService(_db);
        Seed();
    }

    private void Seed()
    {
        _db.Users.AddRange(
            new User { Id = OwnerId, Name = "Owner", Username = "owner", PasswordHash = "x", Role = UserRole.Owner, IsActive = true },
            new User { Id = KasirId, Name = "Kasir", Username = "kasir", PasswordHash = "x", Role = UserRole.Kasir, IsActive = true },
            new User { Id = KurirId, Name = "Kurir", Username = "kurir", PasswordHash = "x", Role = UserRole.Kurir, IsActive = true }
        );
        _db.Customers.Add(new Customer { Id = CustomerId, Name = "Budi", IsActive = true });
        _db.Locations.AddRange(
            new Location { Id = WarehouseId, Name = "Gudang", Type = LocationType.Warehouse, IsActive = true },
            new Location { Id = VehicleId, Name = "Motor 1", Type = LocationType.Vehicle, AssignedTo = KurirId, IsActive = true }
        );
        _db.Products.Add(new Product
        {
            Id = ProductId, Name = "Galon", Category = ProductCategory.Refillable,
            ProductionType = ProductionType.SelfProduced, Type = ProductType.Air,
            Unit = "galon", BasePrice = 5000m, IsActive = true
        });
        _db.SaveChanges();
    }

    private void AddCompletedTransaction(Guid staffId, decimal paid, decimal debt = 0m)
    {
        _db.Transactions.Add(new Transaction
        {
            Id = Guid.NewGuid(),
            TransactionType = TransactionType.Counter,
            CustomerId = CustomerId,
            StaffId = staffId,
            LocationId = WarehouseId,
            Status = TransactionStatus.Completed,
            PaymentMethod = PaymentMethod.Cash,
            TotalAmount = paid + debt,
            PaidAmount = paid,
            DebtAmount = debt,
            CreatedAt = DateTime.UtcNow
        });
        _db.SaveChanges();
    }

    // ── TodayRevenue / TodayTransactions ───────────────────────────────────

    [Fact]
    public async Task GetDashboard_Owner_IncludesAllStaffRevenue()
    {
        AddCompletedTransaction(KasirId, 25000m);
        AddCompletedTransaction(KurirId, 10000m);

        var result = await _sut.GetDashboardAsync(Today, OwnerId, "owner");

        Assert.Equal(35000m, result.TodayRevenue);
        Assert.Equal(2, result.TodayTransactions);
    }

    [Fact]
    public async Task GetDashboard_Kasir_OnlySeesOwnRevenue()
    {
        AddCompletedTransaction(KasirId, 25000m);
        AddCompletedTransaction(KurirId, 10000m);

        var result = await _sut.GetDashboardAsync(Today, KasirId, "kasir");

        Assert.Equal(25000m, result.TodayRevenue);
        Assert.Equal(1, result.TodayTransactions);
    }

    [Fact]
    public async Task GetDashboard_Kurir_OnlySeesOwnRevenue()
    {
        AddCompletedTransaction(KasirId, 25000m);
        AddCompletedTransaction(KurirId, 10000m);

        var result = await _sut.GetDashboardAsync(Today, KurirId, "kurir");

        Assert.Equal(10000m, result.TodayRevenue);
        Assert.Equal(1, result.TodayTransactions);
    }

    // ── StaffRevenue ───────────────────────────────────────────────────────

    [Fact]
    public async Task GetDashboard_Owner_StaffRevenuePopulated()
    {
        AddCompletedTransaction(KasirId, 50000m);

        var result = await _sut.GetDashboardAsync(Today, OwnerId, "owner");

        Assert.Contains(result.StaffRevenue, s => s.StaffId == KasirId && s.Revenue == 50000m);
    }

    [Fact]
    public async Task GetDashboard_NonOwner_StaffRevenueEmpty()
    {
        AddCompletedTransaction(KasirId, 50000m);

        var result = await _sut.GetDashboardAsync(Today, KasirId, "kasir");

        Assert.Empty(result.StaffRevenue);
    }

    // ── CustomerDebts — always store-wide ────────────────────────────────

    [Fact]
    public async Task GetDashboard_CustomerDebts_AlwaysStoreWide()
    {
        // Kasir creates a transaction that results in debt
        AddCompletedTransaction(KasirId, 0m, debt: 40000m);

        // Kurir should still see the store-wide customer debt
        var result = await _sut.GetDashboardAsync(Today, KurirId, "kurir");

        Assert.Contains(result.CustomerDebts, d => d.CustomerId == CustomerId && d.OutstandingDebt == 40000m);
        Assert.Equal(40000m, result.TotalOutstandingDebt);
    }

    [Fact]
    public async Task GetDashboard_CustomerWithInitialDebt_IncludedInCustomerDebts()
    {
        var debtorId = Guid.NewGuid();
        _db.Customers.Add(new Customer { Id = debtorId, Name = "InitDebtor", InitialDebt = 20000m, IsActive = true });
        _db.SaveChanges();

        var result = await _sut.GetDashboardAsync(Today, OwnerId, "owner");

        Assert.Contains(result.CustomerDebts, d => d.CustomerId == debtorId && d.OutstandingDebt == 20000m);
    }

    // ── WeeklyChart ────────────────────────────────────────────────────────

    [Fact]
    public async Task GetDashboard_WeeklyChart_Returns7Entries()
    {
        var result = await _sut.GetDashboardAsync(Today, OwnerId, "owner");

        Assert.Equal(7, result.WeeklyChart.Count());
    }

    [Fact]
    public async Task GetDashboard_WeeklyChart_LastEntryMatchesToday()
    {
        var result = await _sut.GetDashboardAsync(Today, OwnerId, "owner");

        Assert.Equal(Today.ToString("yyyy-MM-dd"), result.WeeklyChart.Last().Date);
    }

    // ── TodayPurchaseCost ─────────────────────────────────────────────────

    [Fact]
    public async Task GetDashboard_TodayPurchaseCost_SumsStockMovementsWithCost()
    {
        _db.StockMovements.Add(new StockMovement
        {
            Id = Guid.NewGuid(), ProductId = ProductId,
            MovementType = MovementType.Receive, ContainerStatus = ContainerStatus.Filled,
            Quantity = 10, ToLocationId = WarehouseId,
            PurchaseCost = 50000m, CreatedBy = OwnerId, CreatedAt = DateTime.UtcNow
        });
        _db.SaveChanges();

        var result = await _sut.GetDashboardAsync(Today, OwnerId, "owner");

        Assert.Equal(50000m, result.TodayPurchaseCost);
    }

    // ── TodayDebtCollected ─────────────────────────────────────────────────

    [Fact]
    public async Task GetDashboard_TodayDebtCollected_SumsDebtPayments()
    {
        _db.DebtPayments.Add(new DebtPayment
        {
            Id = Guid.NewGuid(), CustomerId = CustomerId,
            Amount = 15000m, Method = PaymentMethod.Cash,
            CreatedBy = KasirId, CreatedAt = DateTime.UtcNow
        });
        _db.SaveChanges();

        var result = await _sut.GetDashboardAsync(Today, OwnerId, "owner");

        Assert.Equal(15000m, result.TodayDebtCollected);
    }

    // ── LowStockCount ─────────────────────────────────────────────────────

    [Fact]
    public async Task GetDashboard_LowStockCount_CountsProductsAtOrBelow5()
    {
        // 3 filled in — below threshold of 5
        _db.StockMovements.Add(new StockMovement
        {
            Id = Guid.NewGuid(), ProductId = ProductId,
            MovementType = MovementType.Receive, ContainerStatus = ContainerStatus.Filled,
            Quantity = 3, ToLocationId = WarehouseId,
            CreatedBy = OwnerId, CreatedAt = DateTime.UtcNow
        });
        _db.SaveChanges();

        var result = await _sut.GetDashboardAsync(Today, OwnerId, "owner");

        Assert.Equal(1, result.LowStockCount);
    }

    // ── RecentTransactions ────────────────────────────────────────────────

    [Fact]
    public async Task GetDashboard_RecentTransactions_OrderedByDescendingDate()
    {
        AddCompletedTransaction(KasirId, 10000m);
        AddCompletedTransaction(KasirId, 20000m);

        var result = await _sut.GetDashboardAsync(Today, OwnerId, "owner");
        var recent = result.RecentTransactions.ToList();

        for (int i = 0; i < recent.Count - 1; i++)
            Assert.True(recent[i].CreatedAt >= recent[i + 1].CreatedAt);
    }
}
