using Pos.Api.Data;
using Pos.Api.Data.Enums;
using Pos.Api.DTOs.Dashboard;
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

    // ── ContainerLoans — FR-DSH-014 (store-wide, active customers, net != 0) ─

    private void AddContainerLoan(Guid customerId, Guid productId, int quantity, bool isReversed = false)
    {
        _db.ContainerLoans.Add(new ContainerLoan
        {
            Id = Guid.NewGuid(),
            CustomerId = customerId,
            ProductId = productId,
            Quantity = quantity,
            CreatedBy = OwnerId,
            IsReversed = isReversed,
            CreatedAt = DateTime.UtcNow
        });
        _db.SaveChanges();
    }

    [Fact]
    public async Task GetDashboard_ContainerLoans_AggregatesNetPerCustomerAndProduct()
    {
        AddContainerLoan(CustomerId, ProductId, 10);
        AddContainerLoan(CustomerId, ProductId, -20);

        var result = await _sut.GetDashboardAsync(Today, OwnerId, "owner");
        var entry = Assert.Single(result.ContainerLoans);

        Assert.Equal(CustomerId, entry.CustomerId);
        Assert.Equal("Budi", entry.CustomerName);
        Assert.Equal(ProductId, entry.ProductId);
        Assert.Equal("Galon", entry.ProductName);
        Assert.Equal("galon", entry.ProductUnit);
        Assert.Equal(-10, entry.NetQuantity);
    }

    [Fact]
    public async Task GetDashboard_ContainerLoans_KeepsPositiveAndNegativeNetsSeparately()
    {
        var otherProductId = Guid.NewGuid();
        _db.Products.Add(new Product
        {
            Id = otherProductId, Name = "Gas 3kg", Category = ProductCategory.Refillable,
            ProductionType = ProductionType.Purchased, Type = ProductType.Gas,
            Unit = "tabung 3kg", BasePrice = 20000m, IsActive = true
        });
        _db.SaveChanges();

        AddContainerLoan(CustomerId, ProductId, 7);
        AddContainerLoan(CustomerId, otherProductId, -4);

        var result = await _sut.GetDashboardAsync(Today, OwnerId, "owner");
        var entries = result.ContainerLoans.ToList();

        Assert.Equal(2, entries.Count);
        Assert.Contains(entries, e => e.ProductId == ProductId && e.NetQuantity == 7);
        Assert.Contains(entries, e => e.ProductId == otherProductId && e.NetQuantity == -4);
    }

    [Fact]
    public async Task GetDashboard_ContainerLoans_OmitsZeroNet()
    {
        AddContainerLoan(CustomerId, ProductId, 5);
        AddContainerLoan(CustomerId, ProductId, -5);

        var result = await _sut.GetDashboardAsync(Today, OwnerId, "owner");

        Assert.Empty(result.ContainerLoans);
    }

    [Fact]
    public async Task GetDashboard_ContainerLoans_ExcludesReversedLoans()
    {
        AddContainerLoan(CustomerId, ProductId, 9, isReversed: true);

        var result = await _sut.GetDashboardAsync(Today, OwnerId, "owner");

        Assert.Empty(result.ContainerLoans);
    }

    [Fact]
    public async Task GetDashboard_ContainerLoans_ExcludesInactiveCustomers()
    {
        var deletedCustomerId = Guid.NewGuid();
        _db.Customers.Add(new Customer { Id = deletedCustomerId, Name = "Dihapus", IsActive = false });
        _db.SaveChanges();

        AddContainerLoan(CustomerId, ProductId, 3);
        AddContainerLoan(deletedCustomerId, ProductId, 8);

        var result = await _sut.GetDashboardAsync(Today, OwnerId, "owner");
        var entry = Assert.Single(result.ContainerLoans);

        Assert.Equal(CustomerId, entry.CustomerId);
    }

    [Fact]
    public async Task GetDashboard_ContainerLoans_AlwaysStoreWide()
    {
        AddContainerLoan(CustomerId, ProductId, 6);

        var result = await _sut.GetDashboardAsync(Today, KurirId, "kurir");

        Assert.Contains(result.ContainerLoans, c => c.CustomerId == CustomerId && c.NetQuantity == 6);
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

    // ── Stock summary — FR-DSH-012 ("Pergerakan Stok", period-filtered) ──

    /// <summary>Wednesday, so the containing calendar week is 2026-03-16 .. 2026-03-22.</summary>
    private static readonly DateOnly SummaryAnchor = new(2026, 3, 18);

    private static string Iso(DateOnly date) => date.ToString("yyyy-MM-dd");

    /// <summary>A UTC timestamp that falls at noon WIB on the given date.</summary>
    private static DateTime WibNoon(DateOnly date) =>
        Pos.Api.Services.WibTimeZone.GetUtcDayBounds(date).Start.AddHours(5);

    private void AddSummaryMovement(
        Guid productId, DateOnly date, MovementType type, ContainerStatus status, int quantity,
        bool isReversed = false, bool isReversal = false)
    {
        _db.StockMovements.Add(new StockMovement
        {
            Id = Guid.NewGuid(), ProductId = productId,
            MovementType = type, ContainerStatus = status, Quantity = quantity,
            FromLocationId = type == MovementType.Dispatch ? WarehouseId : null,
            ToLocationId   = type == MovementType.Dispatch ? null : WarehouseId,
            IsReversed = isReversed, IsReversal = isReversal,
            CreatedBy = KurirId, CreatedAt = WibNoon(date)
        });
        _db.SaveChanges();
    }

    /// <summary>Resolves a preset through the production resolver, then loads its summary.</summary>
    private async Task<StockMovementSummaryResponse> GetSummaryAsync(string period, DateOnly anchor)
    {
        var resolved = Pos.Api.Services.StockPeriodRange.TryResolve(
            period, anchor, null, null, out var normalized, out var start, out var end, out var error);
        Assert.True(resolved, error);
        return await _sut.GetStockSummaryAsync(normalized, start, end);
    }

    [Fact]
    public async Task GetStockSummary_SimpleProduct_DispatchCountsAsSold()
    {
        var simpleProductId = Guid.NewGuid();
        _db.Products.Add(new Product
        {
            Id = simpleProductId, Name = "Aqua", Category = ProductCategory.Simple,
            Type = ProductType.Air, Unit = "karton", BasePrice = 30000m, IsActive = true
        });
        _db.SaveChanges();
        AddSummaryMovement(simpleProductId, SummaryAnchor, MovementType.Dispatch, ContainerStatus.Na, 5);

        var result = await GetSummaryAsync("day", SummaryAnchor);
        var entry = result.Items.Single(s => s.ProductId == simpleProductId);

        Assert.Equal(5, entry.TotalSold);
        Assert.Equal(0, entry.TotalReceived);
    }

    [Fact]
    public async Task GetStockSummary_SimpleProduct_InboundCountsAsReceived()
    {
        var simpleProductId = Guid.NewGuid();
        _db.Products.Add(new Product
        {
            Id = simpleProductId, Name = "Aqua", Category = ProductCategory.Simple,
            Type = ProductType.Air, Unit = "karton", BasePrice = 30000m, IsActive = true
        });
        _db.SaveChanges();
        AddSummaryMovement(simpleProductId, SummaryAnchor, MovementType.Receive, ContainerStatus.Na, 10);

        var result = await GetSummaryAsync("day", SummaryAnchor);
        var entry = result.Items.Single(s => s.ProductId == simpleProductId);

        Assert.Equal(10, entry.TotalReceived);
        Assert.Equal(0, entry.TotalSold);
    }

    [Fact]
    public async Task GetStockSummary_RefillableProduct_DispatchFilledCountsAsSold()
    {
        // Dispatch + filled container = sold (filled units delivered to customer)
        AddSummaryMovement(ProductId, SummaryAnchor, MovementType.Dispatch, ContainerStatus.Filled, 3);

        var result = await GetSummaryAsync("day", SummaryAnchor);
        var entry = result.Items.Single(s => s.ProductId == ProductId);

        Assert.Equal(3, entry.TotalSold);
        Assert.Equal(0, entry.TotalReceived);
    }

    [Fact]
    public async Task GetStockSummary_RefillableProduct_DispatchEmptyNotCountedAsSold()
    {
        // Dispatch + empty container does NOT count as sold
        AddSummaryMovement(ProductId, SummaryAnchor, MovementType.Dispatch, ContainerStatus.Empty, 2);

        var result = await GetSummaryAsync("day", SummaryAnchor);

        // No sold/received activity → product is filtered out
        Assert.Empty(result.Items);
    }

    [Fact]
    public async Task GetStockSummary_RefillableProduct_InboundFilledCountsAsReceived()
    {
        AddSummaryMovement(ProductId, SummaryAnchor, MovementType.Receive, ContainerStatus.Filled, 7);

        var result = await GetSummaryAsync("day", SummaryAnchor);
        var entry = result.Items.Single(s => s.ProductId == ProductId);

        Assert.Equal(7, entry.TotalReceived);
        Assert.Equal(0, entry.TotalSold);
    }

    [Fact]
    public async Task GetStockSummary_RefillableProduct_InboundEmptyNotCountedAsReceived()
    {
        // Inbound + empty container does NOT count as received
        AddSummaryMovement(ProductId, SummaryAnchor, MovementType.Receive, ContainerStatus.Empty, 4);

        var result = await GetSummaryAsync("day", SummaryAnchor);

        Assert.Empty(result.Items);
    }

    [Fact]
    public async Task GetStockSummary_ReversedMovement_Excluded()
    {
        AddSummaryMovement(ProductId, SummaryAnchor, MovementType.Dispatch, ContainerStatus.Filled, 5,
            isReversed: true);

        var result = await GetSummaryAsync("day", SummaryAnchor);

        Assert.Empty(result.Items);
    }

    [Fact]
    public async Task GetStockSummary_ReversalEntry_Excluded()
    {
        AddSummaryMovement(ProductId, SummaryAnchor, MovementType.Receive, ContainerStatus.Filled, 5,
            isReversal: true);

        var result = await GetSummaryAsync("day", SummaryAnchor);

        Assert.Empty(result.Items);
    }

    [Fact]
    public async Task GetStockSummary_DayPeriod_IncludesOnlyAnchorDay()
    {
        AddSummaryMovement(ProductId, SummaryAnchor, MovementType.Dispatch, ContainerStatus.Filled, 3);
        AddSummaryMovement(ProductId, SummaryAnchor.AddDays(-1), MovementType.Dispatch, ContainerStatus.Filled, 99);

        var result = await GetSummaryAsync("day", SummaryAnchor);

        Assert.Equal("day", result.Period);
        Assert.Equal(Iso(SummaryAnchor), result.StartDate);
        Assert.Equal(Iso(SummaryAnchor), result.EndDate);
        Assert.Equal(3, Assert.Single(result.Items).TotalSold);
    }

    [Fact]
    public async Task GetStockSummary_WeekPeriod_SpansMondayToSunday()
    {
        AddSummaryMovement(ProductId, new DateOnly(2026, 3, 16), MovementType.Dispatch, ContainerStatus.Filled, 2); // Monday
        AddSummaryMovement(ProductId, new DateOnly(2026, 3, 22), MovementType.Dispatch, ContainerStatus.Filled, 3); // Sunday
        AddSummaryMovement(ProductId, new DateOnly(2026, 3, 15), MovementType.Dispatch, ContainerStatus.Filled, 50); // Sunday before
        AddSummaryMovement(ProductId, new DateOnly(2026, 3, 23), MovementType.Dispatch, ContainerStatus.Filled, 60); // Monday after

        var result = await GetSummaryAsync("week", SummaryAnchor);

        Assert.Equal("2026-03-16", result.StartDate);
        Assert.Equal("2026-03-22", result.EndDate);
        Assert.Equal(5, Assert.Single(result.Items).TotalSold);
    }

    [Fact]
    public async Task GetStockSummary_MonthPeriod_SpansCalendarMonth()
    {
        AddSummaryMovement(ProductId, new DateOnly(2026, 3, 1), MovementType.Dispatch, ContainerStatus.Filled, 4);
        AddSummaryMovement(ProductId, new DateOnly(2026, 3, 31), MovementType.Dispatch, ContainerStatus.Filled, 6);
        AddSummaryMovement(ProductId, new DateOnly(2026, 2, 28), MovementType.Dispatch, ContainerStatus.Filled, 40);
        AddSummaryMovement(ProductId, new DateOnly(2026, 4, 1), MovementType.Dispatch, ContainerStatus.Filled, 60);

        var result = await GetSummaryAsync("month", SummaryAnchor);

        Assert.Equal("2026-03-01", result.StartDate);
        Assert.Equal("2026-03-31", result.EndDate);
        Assert.Equal(10, Assert.Single(result.Items).TotalSold);
    }

    [Fact]
    public async Task GetStockSummary_YearPeriod_SpansCalendarYear()
    {
        AddSummaryMovement(ProductId, new DateOnly(2026, 1, 1), MovementType.Dispatch, ContainerStatus.Filled, 1);
        AddSummaryMovement(ProductId, new DateOnly(2026, 12, 31), MovementType.Dispatch, ContainerStatus.Filled, 2);
        AddSummaryMovement(ProductId, new DateOnly(2025, 12, 31), MovementType.Dispatch, ContainerStatus.Filled, 30);
        AddSummaryMovement(ProductId, new DateOnly(2027, 1, 1), MovementType.Dispatch, ContainerStatus.Filled, 40);

        var result = await GetSummaryAsync("year", SummaryAnchor);

        Assert.Equal("2026-01-01", result.StartDate);
        Assert.Equal("2026-12-31", result.EndDate);
        Assert.Equal(3, Assert.Single(result.Items).TotalSold);
    }

    [Fact]
    public async Task GetStockSummary_CustomRange_UsesSuppliedBoundsInclusive()
    {
        AddSummaryMovement(ProductId, new DateOnly(2026, 3, 10), MovementType.Dispatch, ContainerStatus.Filled, 2);
        AddSummaryMovement(ProductId, new DateOnly(2026, 3, 20), MovementType.Dispatch, ContainerStatus.Filled, 3);
        AddSummaryMovement(ProductId, new DateOnly(2026, 3, 21), MovementType.Dispatch, ContainerStatus.Filled, 70);

        var result = await _sut.GetStockSummaryAsync(
            "custom", new DateOnly(2026, 3, 10), new DateOnly(2026, 3, 20));

        Assert.Equal("custom", result.Period);
        Assert.Equal("2026-03-10", result.StartDate);
        Assert.Equal("2026-03-20", result.EndDate);
        Assert.Equal(5, Assert.Single(result.Items).TotalSold);
    }

    [Fact]
    public async Task GetStockSummary_MultiDayRange_AggregatesSoldAndReceived()
    {
        AddSummaryMovement(ProductId, new DateOnly(2026, 3, 1), MovementType.Dispatch, ContainerStatus.Filled, 4);
        AddSummaryMovement(ProductId, new DateOnly(2026, 3, 31), MovementType.Dispatch, ContainerStatus.Filled, 6);
        AddSummaryMovement(ProductId, new DateOnly(2026, 3, 15), MovementType.Receive, ContainerStatus.Filled, 50);

        var result = await GetSummaryAsync("month", SummaryAnchor);

        var entry = Assert.Single(result.Items);
        Assert.Equal(10, entry.TotalSold);
        Assert.Equal(50, entry.TotalReceived);
    }

    [Fact]
    public async Task GetStockSummary_NoActivityInRange_ReturnsEmptyItems()
    {
        AddSummaryMovement(ProductId, SummaryAnchor, MovementType.Dispatch, ContainerStatus.Filled, 3);

        var result = await _sut.GetStockSummaryAsync(
            "custom", new DateOnly(2025, 1, 1), new DateOnly(2025, 1, 31));

        Assert.Empty(result.Items);
    }

    [Fact]
    public async Task GetStockSummary_SortedByProductName()
    {
        var alphaId = Guid.NewGuid();
        var zebraId = Guid.NewGuid();
        _db.Products.AddRange(
            new Product
            {
                Id = zebraId, Name = "Zebra", Category = ProductCategory.Simple,
                Type = ProductType.Air, Unit = "karton", BasePrice = 1000m, IsActive = true
            },
            new Product
            {
                Id = alphaId, Name = "Alpha", Category = ProductCategory.Simple,
                Type = ProductType.Air, Unit = "karton", BasePrice = 1000m, IsActive = true
            });
        _db.SaveChanges();
        AddSummaryMovement(zebraId, SummaryAnchor, MovementType.Dispatch, ContainerStatus.Na, 1);
        AddSummaryMovement(alphaId, SummaryAnchor, MovementType.Dispatch, ContainerStatus.Na, 1);

        var result = await GetSummaryAsync("day", SummaryAnchor);

        Assert.Equal(new[] { "Alpha", "Zebra" }, result.Items.Select(i => i.ProductName));
    }
}
