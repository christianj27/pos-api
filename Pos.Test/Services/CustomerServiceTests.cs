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
    public async Task GetAll_NoFilter_ReturnsAllCustomers()
    {
        _db.Customers.AddRange(
            new Customer { Name = "Andi", IsActive = true },
            new Customer { Name = "Bejo", IsActive = false }
        );
        _db.SaveChanges();

        var all = (await _sut.GetAllAsync(activeOnly: false)).ToList();

        Assert.True(all.Count >= 2);
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

    }
