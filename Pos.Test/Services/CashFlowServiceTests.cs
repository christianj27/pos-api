using Pos.Api.Data;
using Pos.Api.Data.Enums;
using Pos.Api.Models;
using Pos.Api.Services.Implementations;
using Pos.Test.Helpers;

namespace Pos.Test.Services;

public class CashFlowServiceTests
{
    private readonly AppDbContext _db;
    private readonly CashFlowService _sut;

    private static readonly Guid StaffId = Guid.NewGuid();
    private static readonly Guid CustomerId = Guid.NewGuid();
    private static readonly Guid LocationId = Guid.NewGuid();
    private static readonly Guid ProductId = Guid.NewGuid();

    private static readonly DateOnly Today = Pos.Api.Services.WibTimeZone.TodayWib();

    public CashFlowServiceTests()
    {
        _db = DbContextFactory.Create(Guid.NewGuid().ToString());
        _sut = new CashFlowService(_db);
        Seed();
    }

    private void Seed()
    {
        _db.Users.Add(new User
        {
            Id = StaffId, Name = "Kasir", Username = "kasir",
            PasswordHash = "x", Role = UserRole.Kasir, IsActive = true
        });
        _db.Customers.Add(new Customer { Id = CustomerId, Name = "Budi", IsActive = true });
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

    private Transaction AddTransaction(decimal paid, decimal debt = 0m)
    {
        var tx = new Transaction
        {
            Id = Guid.NewGuid(),
            TransactionType = TransactionType.Counter,
            CustomerId = CustomerId,
            StaffId = StaffId,
            LocationId = LocationId,
            Status = TransactionStatus.Completed,
            PaymentMethod = PaymentMethod.Cash,
            TotalAmount = paid + debt,
            PaidAmount = paid,
            DebtAmount = debt,
            CreatedAt = DateTime.UtcNow
        };
        _db.Transactions.Add(tx);
        _db.SaveChanges();
        return tx;
    }

    // ── GetCashFlow (single day) ───────────────────────────────────────────

    [Fact]
    public async Task GetCashFlow_CompletedTransaction_GeneratesCashInEntry()
    {
        AddTransaction(paid: 30000m);

        var result = await _sut.GetCashFlowAsync(Today);

        Assert.Contains(result.Entries, e => e.FlowType == "cash_in" && e.Category == "sale_payment" && e.Amount == 30000m);
    }

    [Fact]
    public async Task GetCashFlow_TransactionWithDebt_GeneratesNewDebtEntry()
    {
        AddTransaction(paid: 10000m, debt: 20000m);

        var result = await _sut.GetCashFlowAsync(Today);

        Assert.Contains(result.Entries, e => e.FlowType == "new_debt" && e.Category == "debt_created" && e.Amount == 20000m);
    }

    [Fact]
    public async Task GetCashFlow_TransactionZeroPaid_NoCashInEntry()
    {
        AddTransaction(paid: 0m, debt: 15000m);

        var result = await _sut.GetCashFlowAsync(Today);

        Assert.DoesNotContain(result.Entries, e => e.FlowType == "cash_in" && e.Category == "sale_payment");
    }

    [Fact]
    public async Task GetCashFlow_DebtPayment_GeneratesCashInDebtPaymentEntry()
    {
        _db.DebtPayments.Add(new DebtPayment
        {
            Id = Guid.NewGuid(), CustomerId = CustomerId,
            Amount = 12000m, Method = PaymentMethod.Cash,
            CreatedBy = StaffId, CreatedAt = DateTime.UtcNow
        });
        _db.SaveChanges();

        var result = await _sut.GetCashFlowAsync(Today);

        Assert.Contains(result.Entries, e => e.FlowType == "cash_in" && e.Category == "debt_payment" && e.Amount == 12000m);
    }

    [Fact]
    public async Task GetCashFlow_StockMovementWithCost_GeneratesCashOutEntry()
    {
        _db.StockMovements.Add(new StockMovement
        {
            Id = Guid.NewGuid(), ProductId = ProductId,
            MovementType = MovementType.Receive, ContainerStatus = ContainerStatus.Filled,
            Quantity = 10, ToLocationId = LocationId,
            PurchaseCost = 50000m, CreatedBy = StaffId, CreatedAt = DateTime.UtcNow
        });
        _db.SaveChanges();

        var result = await _sut.GetCashFlowAsync(Today);

        Assert.Contains(result.Entries, e => e.FlowType == "cash_out" && e.Category == "stock_purchase" && e.Amount == 50000m);
    }

    // ── Summary totals ─────────────────────────────────────────────────────

    [Fact]
    public async Task GetCashFlow_Totals_AreComputedCorrectly()
    {
        AddTransaction(paid: 40000m, debt: 5000m);
        _db.StockMovements.Add(new StockMovement
        {
            Id = Guid.NewGuid(), ProductId = ProductId,
            MovementType = MovementType.Receive, ContainerStatus = ContainerStatus.Filled,
            Quantity = 5, ToLocationId = LocationId,
            PurchaseCost = 20000m, CreatedBy = StaffId, CreatedAt = DateTime.UtcNow
        });
        _db.SaveChanges();

        var result = await _sut.GetCashFlowAsync(Today);

        Assert.Equal(40000m, result.TotalCashIn);
        Assert.Equal(20000m, result.TotalCashOut);
        Assert.Equal(20000m, result.NetCash);
        Assert.Equal(5000m, result.TotalNewDebt);
    }

    // ── Date filter ────────────────────────────────────────────────────────

    [Fact]
    public async Task GetCashFlow_DateFilter_ExcludesFutureDayEntries()
    {
        AddTransaction(paid: 25000m);

        var yesterday = Today.AddDays(-1);
        var result = await _sut.GetCashFlowAsync(yesterday);

        Assert.DoesNotContain(result.Entries, e => e.FlowType == "cash_in" && e.Amount == 25000m);
    }

    // ── GetCashFlowRange ───────────────────────────────────────────────────

    [Fact]
    public async Task GetCashFlowRange_MultiDay_IncludesAllDays()
    {
        AddTransaction(paid: 10000m);

        var start = Today.AddDays(-1);
        var end = Today;
        var result = await _sut.GetCashFlowRangeAsync(start, end);

        Assert.Contains(result.Entries, e => e.FlowType == "cash_in" && e.Amount == 10000m);
    }

    [Fact]
    public async Task GetCashFlow_NoEntries_ReturnsZeroTotals()
    {
        var result = await _sut.GetCashFlowAsync(Today);

        Assert.Equal(0m, result.TotalCashIn);
        Assert.Equal(0m, result.TotalCashOut);
        Assert.Equal(0m, result.NetCash);
        Assert.Equal(0m, result.TotalNewDebt);
    }
}
