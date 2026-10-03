using Pos.Api.Data;
using Pos.Api.Data.Enums;
using Pos.Api.DTOs.Customers;
using Pos.Api.Models;
using Pos.Api.Services.Implementations;
using Pos.Test.Helpers;

namespace Pos.Test.Services;

public class CustomerServiceTests
{
    private readonly AppDbContext _db;
    private readonly CustomerService _sut;

    private static readonly Guid StaffId = Guid.NewGuid();
    private static readonly Guid LocationId = Guid.NewGuid();
    private static readonly Guid ProductId = Guid.NewGuid();

    public CustomerServiceTests()
    {
        _db = DbContextFactory.Create(Guid.NewGuid().ToString());
        _sut = new CustomerService(_db);
        Seed();
    }

    private void Seed()
    {
        _db.Users.Add(new User
        {
            Id = StaffId, Name = "Staff", Username = "staff",
            PasswordHash = "x", Role = UserRole.Kasir, IsActive = true
        });
        _db.Locations.Add(new Location
        {
            Id = LocationId, Name = "Gudang", Type = LocationType.Warehouse, IsActive = true
        });
        _db.Products.Add(new Product
        {
            Id = ProductId, Name = "Galon", Category = ProductCategory.Refillable,
            ProductionType = ProductionType.SelfProduced, Type = ProductType.Air,
            Unit = "galon", BasePrice = 5000m, IsActive = true
        });
        _db.SaveChanges();
    }

    // ── Create ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task Create_ValidRequest_ReturnsCustomer()
    {
        var req = new CreateCustomerRequest("Budi", "08123456789", "Jl. Merdeka 1");
        var (result, err) = await _sut.CreateAsync(req);

        Assert.Null(err);
        Assert.NotNull(result);
        Assert.Equal("Budi", result!.Name);
        Assert.Equal("08123456789", result.Phone);
        Assert.True(result.IsActive);
    }

    [Fact]
    public async Task Create_TrimsWhitespace()
    {
        var req = new CreateCustomerRequest("  Sari  ", " 081 ", "  Jl. X  ");
        var (result, _) = await _sut.CreateAsync(req);

        Assert.Equal("Sari", result!.Name);
        Assert.Equal("081", result.Phone);
    }

    // ── GetAll ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetAll_Default_ExcludesInactiveCustomers()
    {
        _db.Customers.AddRange(
            new Customer { Name = "Andi", IsActive = true },
            new Customer { Name = "Bejo", IsActive = false }
        );
        _db.SaveChanges();

        var all = (await _sut.GetAllAsync()).ToList();

        Assert.Contains(all, c => c.Name == "Andi");
        Assert.DoesNotContain(all, c => c.Name == "Bejo");
    }

    [Fact]
    public async Task GetAll_ActiveOnlyFalse_IncludesInactiveCustomers()
    {
        _db.Customers.AddRange(
            new Customer { Name = "Andi", IsActive = true },
            new Customer { Name = "Bejo", IsActive = false }
        );
        _db.SaveChanges();

        var all = (await _sut.GetAllAsync(activeOnly: false)).ToList();

        Assert.Contains(all, c => c.Name == "Andi");
        Assert.Contains(all, c => c.Name == "Bejo");
    }

    [Fact]
    public async Task GetAll_ActiveOnly_ExcludesInactive()
    {
        _db.Customers.AddRange(
            new Customer { Name = "ActiveOne", IsActive = true },
            new Customer { Name = "InactiveOne", IsActive = false }
        );
        _db.SaveChanges();

        var active = (await _sut.GetAllAsync(activeOnly: true)).ToList();

        Assert.DoesNotContain(active, c => c.Name == "InactiveOne");
        Assert.Contains(active, c => c.Name == "ActiveOne");
    }

    // ── Delete (soft) ──────────────────────────────────────────────────────

    [Fact]
    public async Task Delete_ExistingCustomer_SetsIsActiveFalseAndRetainsRecord()
    {
        var (created, _) = await _sut.CreateAsync(new CreateCustomerRequest("Hapus Aku", null, null));

        var (success, err) = await _sut.DeleteAsync(created!.Id);

        Assert.True(success);
        Assert.Null(err);

        // Record is retained (soft delete), just flagged inactive.
        var retained = _db.Customers.Where(c => c.Id == created.Id).ToList();
        Assert.Single(retained);
        Assert.False(retained[0].IsActive);

        var reloaded = await _sut.GetByIdAsync(created.Id);
        Assert.NotNull(reloaded);
        Assert.False(reloaded!.IsActive);
    }

    [Fact]
    public async Task Delete_NonExistentId_ReturnsError()
    {
        var (success, err) = await _sut.DeleteAsync(Guid.NewGuid());

        Assert.False(success);
        Assert.NotNull(err);
    }

    [Fact]
    public async Task Delete_ExcludesCustomerFromDefaultListButKeepsHistory()
    {
        var (created, _) = await _sut.CreateAsync(new CreateCustomerRequest("Hilang", null, null));
        await _sut.DeleteAsync(created!.Id);

        var defaultList = (await _sut.GetAllAsync()).ToList();
        var withDeleted = (await _sut.GetAllAsync(activeOnly: false)).ToList();

        Assert.DoesNotContain(defaultList, c => c.Id == created.Id);
        Assert.Contains(withDeleted, c => c.Id == created.Id);
    }

    // ── GetById ────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetById_ExistingId_ReturnsCustomer()
    {
        var (created, _) = await _sut.CreateAsync(new CreateCustomerRequest("TestGet", null, null));

        var result = await _sut.GetByIdAsync(created!.Id);

        Assert.NotNull(result);
        Assert.Equal("TestGet", result!.Name);
    }

    [Fact]
    public async Task GetById_NonExistentId_ReturnsNull()
    {
        var result = await _sut.GetByIdAsync(Guid.NewGuid());
        Assert.Null(result);
    }

    // ── Update ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task Update_ExistingCustomer_UpdatesFields()
    {
        var (created, _) = await _sut.CreateAsync(new CreateCustomerRequest("Old Name", null, null));

        var (updated, err) = await _sut.UpdateAsync(created!.Id,
            new UpdateCustomerRequest("New Name", "08111", "Jl. Baru", true));

        Assert.Null(err);
        Assert.Equal("New Name", updated!.Name);
        Assert.Equal("08111", updated.Phone);
    }

    [Fact]
    public async Task Update_NonExistentId_ReturnsError()
    {
        var (result, err) = await _sut.UpdateAsync(Guid.NewGuid(),
            new UpdateCustomerRequest("X", null, null, true));

        Assert.Null(result);
        Assert.NotNull(err);
    }

    [Fact]
    public async Task Update_SetInactive_PersistsFlag()
    {
        var (created, _) = await _sut.CreateAsync(new CreateCustomerRequest("Aktif", null, null));
        var (updated, _) = await _sut.UpdateAsync(created!.Id,
            new UpdateCustomerRequest("Aktif", null, null, false));

        Assert.False(updated!.IsActive);
    }

    // ── Pricing ────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetPricing_NonExistentCustomer_ReturnsNull()
    {
        var result = await _sut.GetPricingAsync(Guid.NewGuid());
        Assert.Null(result);
    }

    [Fact]
    public async Task UpdatePricing_SetsCustomPrice()
    {
        var (customer, _) = await _sut.CreateAsync(new CreateCustomerRequest("Pricing Test", null, null));

        await _sut.UpdatePricingAsync(customer!.Id,
            new UpdateCustomerPricingRequest(
                new[] { new CustomerPricingItemRequest(ProductId, 4000m) }));

        var pricing = await _sut.GetPricingAsync(customer.Id);
        var item = pricing!.Items.First(i => i.ProductId == ProductId);
        Assert.Equal(4000m, item.CustomPrice);
    }

    [Fact]
    public async Task UpdatePricing_NullOrZeroPrice_RemovesEntry()
    {
        var (customer, _) = await _sut.CreateAsync(new CreateCustomerRequest("Pricing Remove", null, null));

        // First set a price
        await _sut.UpdatePricingAsync(customer!.Id,
            new UpdateCustomerPricingRequest(
                new[] { new CustomerPricingItemRequest(ProductId, 4000m) }));

        // Then remove it by passing null
        await _sut.UpdatePricingAsync(customer.Id,
            new UpdateCustomerPricingRequest(
                new[] { new CustomerPricingItemRequest(ProductId, null) }));

        var pricing = await _sut.GetPricingAsync(customer.Id);
        var item = pricing!.Items.First(i => i.ProductId == ProductId);
        Assert.Null(item.CustomPrice);
    }

    [Fact]
    public async Task UpdatePricing_NonExistentCustomer_ReturnsError()
    {
        var (success, err) = await _sut.UpdatePricingAsync(Guid.NewGuid(),
            new UpdateCustomerPricingRequest(
                new[] { new CustomerPricingItemRequest(ProductId, 4000m) }));

        Assert.False(success);
        Assert.NotNull(err);
    }

    // ── Debt ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetDebt_NonExistentCustomer_ReturnsNull()
    {
        var result = await _sut.GetDebtAsync(Guid.NewGuid());
        Assert.Null(result);
    }

    [Fact]
    public async Task GetDebt_CustomerWithNoTransactions_ReturnsZeroDebt()
    {
        var (customer, _) = await _sut.CreateAsync(new CreateCustomerRequest("No Debt", null, null));

        var debt = await _sut.GetDebtAsync(customer!.Id);

        Assert.NotNull(debt);
        Assert.Equal(0m, debt!.OutstandingDebt);
    }

    [Fact]
    public async Task GetDebt_AfterDebtTransaction_ReturnsPositiveDebt()
    {
        var (customer, _) = await _sut.CreateAsync(new CreateCustomerRequest("Debtor", null, null));

        // Manually create a transaction with debt
        _db.Transactions.Add(new Transaction
        {
            Id = Guid.NewGuid(),
            TransactionType = TransactionType.Counter,
            CustomerId = customer!.Id,
            StaffId = StaffId,
            LocationId = LocationId,
            Status = TransactionStatus.Completed,
            PaymentMethod = PaymentMethod.Cash,
            TotalAmount = 100000m,
            PaidAmount = 60000m,
            DebtAmount = 40000m
        });
        _db.SaveChanges();

        var debt = await _sut.GetDebtAsync(customer.Id);

        Assert.Equal(40000m, debt!.OutstandingDebt);
    }

    [Fact]
    public async Task GetDebt_AfterDebtPayment_ReducesDebt()
    {
        var (customer, _) = await _sut.CreateAsync(new CreateCustomerRequest("PayDebt", null, null));

        _db.Transactions.Add(new Transaction
        {
            Id = Guid.NewGuid(),
            TransactionType = TransactionType.Counter,
            CustomerId = customer!.Id,
            StaffId = StaffId,
            LocationId = LocationId,
            Status = TransactionStatus.Completed,
            PaymentMethod = PaymentMethod.Cash,
            TotalAmount = 100000m,
            PaidAmount = 60000m,
            DebtAmount = 40000m
        });
        _db.DebtPayments.Add(new DebtPayment
        {
            Id = Guid.NewGuid(),
            CustomerId = customer.Id,
            Amount = 20000m,
            Method = PaymentMethod.Cash,
            CreatedBy = StaffId
        });
        _db.SaveChanges();

        var debt = await _sut.GetDebtAsync(customer.Id);

        Assert.Equal(20000m, debt!.OutstandingDebt);
    }

    // ── ContainerLoans ─────────────────────────────────────────────────────

    [Fact]
    public async Task GetContainerLoans_NonExistentCustomer_ReturnsNull()
    {
        var result = await _sut.GetContainerLoansAsync(Guid.NewGuid());
        Assert.Null(result);
    }

    // ── InitialDebt ────────────────────────────────────────────────────────

    [Fact]
    public async Task Create_WithInitialDebt_PersistsInitialDebt()
    {
        var req = new CreateCustomerRequest("Debtor Init", null, null, 50000m);
        var (result, err) = await _sut.CreateAsync(req);

        Assert.Null(err);
        Assert.NotNull(result);
        var stored = _db.Customers.First(c => c.Id == result!.Id);
        Assert.Equal(50000m, stored.InitialDebt);
    }

    [Fact]
    public async Task GetDebt_InitialDebt_IsIncludedInOutstandingDebt()
    {
        var req = new CreateCustomerRequest("Init Debt User", null, null, 30000m);
        var (result, _) = await _sut.CreateAsync(req);

        var debt = await _sut.GetDebtAsync(result!.Id);

        Assert.Equal(30000m, debt!.OutstandingDebt);
    }

    // ── Stock summary — FR-CST-011 ("Pergerakan Stok" per pelanggan) ───────

    /// <summary>Wednesday, so the containing calendar week is 2026-03-16 .. 2026-03-22.</summary>
    private static readonly DateOnly SummaryAnchor = new(2026, 3, 18);

    private static string Iso(DateOnly date) => date.ToString("yyyy-MM-dd");

    /// <summary>A UTC timestamp that falls at noon WIB on the given date.</summary>
    private static DateTime WibNoon(DateOnly date) =>
        Pos.Api.Services.WibTimeZone.GetUtcDayBounds(date).Start.AddHours(5);

    private Guid AddCustomer(string name)
    {
        var id = Guid.NewGuid();
        _db.Customers.Add(new Customer { Id = id, Name = name, IsActive = true });
        _db.SaveChanges();
        return id;
    }

    private Guid AddKurir(string name)
    {
        var id = Guid.NewGuid();
        _db.Users.Add(new User
        {
            Id = id, Name = name, Username = name.Replace(" ", "").ToLowerInvariant(),
            PasswordHash = "x", Role = UserRole.Kurir, IsActive = true
        });
        _db.SaveChanges();
        return id;
    }

    private Guid AddSimpleProduct(string name)
    {
        var id = Guid.NewGuid();
        _db.Products.Add(new Product
        {
            Id = id, Name = name, Category = ProductCategory.Simple,
            Type = ProductType.Air, Unit = "karton", BasePrice = 30000m, IsActive = true
        });
        _db.SaveChanges();
        return id;
    }

    /// <summary>Adds a movement belonging to a transaction of <paramref name="customerId"/>.</summary>
    private void AddCustomerMovement(
        Guid customerId, Guid productId, DateOnly date, MovementType type, ContainerStatus status,
        int quantity, Guid? staffId = null, bool isReversed = false, bool isReversal = false)
    {
        var transactionId = Guid.NewGuid();
        _db.Transactions.Add(new Transaction
        {
            Id = transactionId, TransactionType = TransactionType.Delivery,
            CustomerId = customerId, StaffId = staffId ?? StaffId, LocationId = LocationId,
            Status = TransactionStatus.Completed, PaymentMethod = PaymentMethod.Cash,
            CreatedAt = WibNoon(date)
        });
        _db.StockMovements.Add(new StockMovement
        {
            Id = Guid.NewGuid(), ProductId = productId,
            MovementType = type, ContainerStatus = status, Quantity = quantity,
            FromLocationId = type == MovementType.Dispatch ? LocationId : null,
            ToLocationId = type == MovementType.Dispatch ? null : LocationId,
            TransactionId = transactionId,
            IsReversed = isReversed, IsReversal = isReversal,
            CreatedBy = staffId ?? StaffId, CreatedAt = WibNoon(date)
        });
        _db.SaveChanges();
    }

    /// <summary>Adds a movement with no transaction (e.g. a vendor receive) — never customer-linked.</summary>
    private void AddVendorMovement(
        Guid productId, DateOnly date, MovementType type, ContainerStatus status, int quantity)
    {
        _db.StockMovements.Add(new StockMovement
        {
            Id = Guid.NewGuid(), ProductId = productId,
            MovementType = type, ContainerStatus = status, Quantity = quantity,
            ToLocationId = LocationId, CreatedBy = StaffId, CreatedAt = WibNoon(date)
        });
        _db.SaveChanges();
    }

    /// <summary>Resolves a preset through the production resolver, then loads its summary.</summary>
    private async Task<CustomerStockSummaryResponse> GetSummaryAsync(Guid customerId, string period, DateOnly anchor)
    {
        var resolved = Pos.Api.Services.StockPeriodRange.TryResolve(
            period, anchor, null, null, out var normalized, out var start, out var end, out var error);
        Assert.True(resolved, error);
        return (await _sut.GetStockSummaryAsync(customerId, normalized, start, end))!;
    }

    [Fact]
    public async Task GetStockSummary_NonExistentCustomer_ReturnsNull()
    {
        var result = await _sut.GetStockSummaryAsync(Guid.NewGuid(), "day", SummaryAnchor, SummaryAnchor);
        Assert.Null(result);
    }

    [Fact]
    public async Task GetStockSummary_DispatchFilledRefillable_CountsAsSold()
    {
        var customerId = AddCustomer("Bu Ani");
        AddCustomerMovement(customerId, ProductId, SummaryAnchor, MovementType.Dispatch, ContainerStatus.Filled, 3);

        var result = await GetSummaryAsync(customerId, "day", SummaryAnchor);
        var entry = Assert.Single(result.Items);

        Assert.Equal(3, entry.TotalSold);
        Assert.Equal(0, entry.TotalReturned);
    }

    [Fact]
    public async Task GetStockSummary_DispatchEmptyRefillable_NotCountedAsSold()
    {
        var customerId = AddCustomer("Bu Ani");
        AddCustomerMovement(customerId, ProductId, SummaryAnchor, MovementType.Dispatch, ContainerStatus.Empty, 2);

        var result = await GetSummaryAsync(customerId, "day", SummaryAnchor);

        Assert.Empty(result.Items);
    }

    [Fact]
    public async Task GetStockSummary_SimpleProductDispatch_CountsAllAsSold()
    {
        var customerId = AddCustomer("Toko Sedap");
        var simpleId = AddSimpleProduct("Aqua Karton");
        AddCustomerMovement(customerId, simpleId, SummaryAnchor, MovementType.Dispatch, ContainerStatus.Na, 5);

        var result = await GetSummaryAsync(customerId, "day", SummaryAnchor);
        var entry = Assert.Single(result.Items);

        Assert.Equal(5, entry.TotalSold);
        Assert.Equal("simple", entry.ProductCategory);
    }

    [Fact]
    public async Task GetStockSummary_EmptyContainerReturn_CountsAsReturned()
    {
        var customerId = AddCustomer("Bu Ani");
        AddCustomerMovement(customerId, ProductId, SummaryAnchor, MovementType.Receive, ContainerStatus.Empty, 4);

        var result = await GetSummaryAsync(customerId, "day", SummaryAnchor);
        var entry = Assert.Single(result.Items);

        Assert.Equal(4, entry.TotalReturned);
        Assert.Equal(0, entry.TotalSold);
    }

    [Fact]
    public async Task GetStockSummary_FilledInboundOnCustomerTransaction_NotCountedAsReturned()
    {
        var customerId = AddCustomer("Bu Ani");
        AddCustomerMovement(customerId, ProductId, SummaryAnchor, MovementType.Receive, ContainerStatus.Filled, 9);

        var result = await GetSummaryAsync(customerId, "day", SummaryAnchor);

        // "Dikembalikan" only counts empty containers handed back by the customer
        Assert.Empty(result.Items);
    }

    [Fact]
    public async Task GetStockSummary_OtherCustomerMovements_Excluded()
    {
        var customerId = AddCustomer("Bu Ani");
        var otherCustomerId = AddCustomer("Pak Joko");
        AddCustomerMovement(customerId, ProductId, SummaryAnchor, MovementType.Dispatch, ContainerStatus.Filled, 3);
        AddCustomerMovement(otherCustomerId, ProductId, SummaryAnchor, MovementType.Dispatch, ContainerStatus.Filled, 99);

        var result = await GetSummaryAsync(customerId, "day", SummaryAnchor);

        Assert.Equal(3, Assert.Single(result.Items).TotalSold);
    }

    [Fact]
    public async Task GetStockSummary_MovementWithoutTransaction_Excluded()
    {
        var customerId = AddCustomer("Bu Ani");
        AddVendorMovement(ProductId, SummaryAnchor, MovementType.Receive, ContainerStatus.Filled, 50);

        var result = await GetSummaryAsync(customerId, "day", SummaryAnchor);

        Assert.Empty(result.Items);
    }

    [Fact]
    public async Task GetStockSummary_ReversedMovements_Excluded()
    {
        var customerId = AddCustomer("Bu Ani");
        AddCustomerMovement(customerId, ProductId, SummaryAnchor, MovementType.Dispatch, ContainerStatus.Filled, 5,
            isReversed: true);
        AddCustomerMovement(customerId, ProductId, SummaryAnchor, MovementType.Receive, ContainerStatus.Empty, 5,
            isReversal: true);

        var result = await GetSummaryAsync(customerId, "day", SummaryAnchor);

        Assert.Empty(result.Items);
    }

    [Fact]
    public async Task GetStockSummary_DayPeriod_IncludesOnlyAnchorDay()
    {
        var customerId = AddCustomer("Bu Ani");
        AddCustomerMovement(customerId, ProductId, SummaryAnchor, MovementType.Dispatch, ContainerStatus.Filled, 3);
        AddCustomerMovement(customerId, ProductId, SummaryAnchor.AddDays(-1), MovementType.Dispatch, ContainerStatus.Filled, 99);

        var result = await GetSummaryAsync(customerId, "day", SummaryAnchor);

        Assert.Equal("day", result.Period);
        Assert.Equal(Iso(SummaryAnchor), result.StartDate);
        Assert.Equal(Iso(SummaryAnchor), result.EndDate);
        Assert.Equal(3, Assert.Single(result.Items).TotalSold);
    }

    [Fact]
    public async Task GetStockSummary_WeekPeriod_SpansMondayToSunday()
    {
        var customerId = AddCustomer("Bu Ani");
        AddCustomerMovement(customerId, ProductId, new DateOnly(2026, 3, 16), MovementType.Dispatch, ContainerStatus.Filled, 2); // Monday
        AddCustomerMovement(customerId, ProductId, new DateOnly(2026, 3, 22), MovementType.Dispatch, ContainerStatus.Filled, 3); // Sunday
        AddCustomerMovement(customerId, ProductId, new DateOnly(2026, 3, 15), MovementType.Dispatch, ContainerStatus.Filled, 50); // Sunday before
        AddCustomerMovement(customerId, ProductId, new DateOnly(2026, 3, 23), MovementType.Dispatch, ContainerStatus.Filled, 60); // Monday after

        var result = await GetSummaryAsync(customerId, "week", SummaryAnchor);

        Assert.Equal("2026-03-16", result.StartDate);
        Assert.Equal("2026-03-22", result.EndDate);
        Assert.Equal(5, Assert.Single(result.Items).TotalSold);
    }

    [Fact]
    public async Task GetStockSummary_CustomRange_UsesSuppliedBoundsInclusive()
    {
        var customerId = AddCustomer("Bu Ani");
        AddCustomerMovement(customerId, ProductId, new DateOnly(2026, 3, 10), MovementType.Dispatch, ContainerStatus.Filled, 2);
        AddCustomerMovement(customerId, ProductId, new DateOnly(2026, 3, 20), MovementType.Dispatch, ContainerStatus.Filled, 3);
        AddCustomerMovement(customerId, ProductId, new DateOnly(2026, 3, 21), MovementType.Dispatch, ContainerStatus.Filled, 70);

        var result = await _sut.GetStockSummaryAsync(
            customerId, "custom", new DateOnly(2026, 3, 10), new DateOnly(2026, 3, 20));

        Assert.Equal("custom", result!.Period);
        Assert.Equal("2026-03-10", result.StartDate);
        Assert.Equal("2026-03-20", result.EndDate);
        Assert.Equal(5, Assert.Single(result.Items).TotalSold);
    }

    [Fact]
    public async Task GetStockSummary_NoActivityInRange_ReturnsEmptyItems()
    {
        var customerId = AddCustomer("Bu Ani");
        AddCustomerMovement(customerId, ProductId, SummaryAnchor, MovementType.Dispatch, ContainerStatus.Filled, 3);

        var result = await _sut.GetStockSummaryAsync(
            customerId, "custom", new DateOnly(2025, 1, 1), new DateOnly(2025, 1, 31));

        Assert.Empty(result!.Items);
    }

    [Fact]
    public async Task GetStockSummary_SortedByProductName()
    {
        var customerId = AddCustomer("Bu Ani");
        var zebraId = AddSimpleProduct("Zebra");
        var alphaId = AddSimpleProduct("Alpha");
        AddCustomerMovement(customerId, zebraId, SummaryAnchor, MovementType.Dispatch, ContainerStatus.Na, 1);
        AddCustomerMovement(customerId, alphaId, SummaryAnchor, MovementType.Dispatch, ContainerStatus.Na, 1);

        var result = await GetSummaryAsync(customerId, "day", SummaryAnchor);

        Assert.Equal(new[] { "Alpha", "Zebra" }, result.Items.Select(i => i.ProductName));
    }

    [Fact]
    public async Task GetStockSummary_StaffBreakdown_SortedBySoldThenName()
    {
        var customerId = AddCustomer("Bu Ani");
        var otherStaffId = AddKurir("Andi Kurir");
        AddCustomerMovement(customerId, ProductId, SummaryAnchor, MovementType.Dispatch, ContainerStatus.Filled, 8,
            staffId: otherStaffId);
        AddCustomerMovement(customerId, ProductId, SummaryAnchor, MovementType.Receive, ContainerStatus.Empty, 3,
            staffId: otherStaffId);
        AddCustomerMovement(customerId, ProductId, SummaryAnchor, MovementType.Dispatch, ContainerStatus.Filled, 2);

        var result = await GetSummaryAsync(customerId, "day", SummaryAnchor);
        var entry = Assert.Single(result.Items);
        var staff = entry.Staff.ToList();

        Assert.Equal(10, entry.TotalSold);
        Assert.Equal(3, entry.TotalReturned);
        Assert.Equal(2, staff.Count);
        Assert.Equal("Andi Kurir", staff[0].StaffName);
        Assert.Equal(8, staff[0].Sold);
        Assert.Equal(3, staff[0].Returned);
        Assert.Equal(2, staff[1].Sold);
    }

    [Fact]
    public async Task GetStockSummary_EchoesCustomerNameAndResolvedRange()
    {
        var customerId = AddCustomer("Bu Ani");
        AddCustomerMovement(customerId, ProductId, SummaryAnchor, MovementType.Dispatch, ContainerStatus.Filled, 1);

        var result = await GetSummaryAsync(customerId, "month", SummaryAnchor);

        Assert.Equal(customerId, result.CustomerId);
        Assert.Equal("Bu Ani", result.CustomerName);
        Assert.Equal("2026-03-01", result.StartDate);
        Assert.Equal("2026-03-31", result.EndDate);
    }

    }

