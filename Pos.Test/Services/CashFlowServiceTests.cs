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

        // FR-PAY-001: the app writes a Payments row for the paid amount, and Arus Kas dates cash by when
        // it was collected (Payment.PaidAt) rather than by the invoice date.
        if (paid > 0m)
        {
            _db.Payments.Add(new Payment
            {
                Id = Guid.NewGuid(),
                TransactionId = tx.Id,
                Amount = paid,
                Method = PaymentMethod.Cash,
                PaidAt = DateTime.UtcNow,
                CreatedBy = StaffId
            });
        }

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

    // ── Expenses (FR-CSH-006) ──────────────────────────────────────────────

    private Expense AddExpense(decimal amount, DateOnly expenseDate, string description = "Isi bensin truk",
        ExpenseCategory category = ExpenseCategory.Fuel)
    {
        var expense = new Expense
        {
            Id = Guid.NewGuid(),
            Category = category,
            Description = description,
            Amount = amount,
            ExpenseDate = expenseDate,
            CreatedBy = StaffId,
            CreatedAt = DateTime.UtcNow
        };
        _db.Expenses.Add(expense);
        _db.SaveChanges();
        return expense;
    }

    [Fact]
    public async Task GetCashFlow_Expense_GeneratesCashOutOperationalExpenseEntry()
    {
        AddExpense(75000m, Today);

        var result = await _sut.GetCashFlowAsync(Today);

        Assert.Contains(result.Entries, e =>
            e.FlowType == "cash_out" &&
            e.Category == "operational_expense" &&
            e.Amount == 75000m &&
            e.Description == "Bensin - Isi bensin truk");
    }

    [Fact]
    public async Task GetCashFlow_Expense_IsIncludedInCashOutTotal()
    {
        AddExpense(20000m, Today, "Iuran kebersihan", ExpenseCategory.Cleaning);

        var result = await _sut.GetCashFlowAsync(Today);

        Assert.Equal(20000m, result.TotalCashOut);
        Assert.Equal(-20000m, result.NetCash);
    }

    [Fact]
    public async Task GetCashFlow_Expense_UsesBusinessDateForEntryTimestamp()
    {
        // Recorded 3 days ago, but business-dated today → must land on today.
        var expense = AddExpense(15000m, Today);
        expense.CreatedAt = DateTime.UtcNow.AddDays(-3);
        _db.SaveChanges();

        var result = await _sut.GetCashFlowAsync(Today);

        var entry = Assert.Single(result.Entries, e => e.Category == "operational_expense");
        Assert.Equal(Today, DateOnly.FromDateTime(Pos.Api.Services.WibTimeZone.ToWib(entry.CreatedAt)));
    }

    [Fact]
    public async Task GetCashFlow_Expense_OnAnotherBusinessDate_IsExcluded()
    {
        AddExpense(40000m, Today.AddDays(-2));

        var result = await _sut.GetCashFlowAsync(Today);

        Assert.DoesNotContain(result.Entries, e => e.Category == "operational_expense");
    }

    [Fact]
    public async Task GetCashFlowRange_Expenses_IncludesBusinessDatesWithinRange()
    {
        AddExpense(30000m, Today.AddDays(-1), "Gaji karyawan", ExpenseCategory.Salary);
        AddExpense(10000m, Today.AddDays(-5));

        var result = await _sut.GetCashFlowRangeAsync(Today.AddDays(-2), Today);

        Assert.Contains(result.Entries, e => e.Category == "operational_expense" && e.Amount == 30000m);
        Assert.DoesNotContain(result.Entries, e => e.Category == "operational_expense" && e.Amount == 10000m);
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

    // ── Settlement-driven entries (FR-STL) ────────────────────────────────

    [Fact]
    public async Task GetCashFlow_LateInstalment_LandsOnCollectionDayNotInvoiceDay()
    {
        // Invoice raised three days ago, money collected today: the cash belongs to today, and the
        // invoice's own day must not be rewritten (that day may already be settled).
        var tx = AddTransaction(paid: 0m, debt: 30000m);
        tx.CreatedAt = DateTime.UtcNow.AddDays(-3);
        _db.Payments.Add(new Payment
        {
            Id = Guid.NewGuid(), TransactionId = tx.Id, Amount = 30000m,
            Method = PaymentMethod.Cash, PaidAt = DateTime.UtcNow, CreatedBy = StaffId
        });
        _db.SaveChanges();

        var collectionDay = await _sut.GetCashFlowAsync(Today);
        var invoiceDay = await _sut.GetCashFlowAsync(Today.AddDays(-3));

        Assert.Contains(collectionDay.Entries, e => e.Category == "sale_payment" && e.Amount == 30000m);
        Assert.DoesNotContain(invoiceDay.Entries, e => e.Category == "sale_payment");
    }

    [Fact]
    public async Task GetCashFlow_CashAdjustment_BooksOnItsBusinessDate()
    {
        _db.CashAdjustments.Add(new CashAdjustment
        {
            Id = Guid.NewGuid(), UserId = StaffId, BusinessDate = Today,
            Amount = -5000m, Reason = "Kas kurang", CreatedBy = StaffId, CreatedAt = DateTime.UtcNow
        });
        _db.SaveChanges();

        var result = await _sut.GetCashFlowAsync(Today);

        Assert.Contains(result.Entries, e =>
            e.Category == "cash_variance" && e.FlowType == "cash_out" && e.Amount == 5000m);
        Assert.Equal(-5000m, result.NetCash);
    }
}
