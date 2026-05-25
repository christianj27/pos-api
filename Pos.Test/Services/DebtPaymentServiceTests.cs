using Pos.Api.Data;
using Pos.Api.Data.Enums;
using Pos.Api.DTOs.DebtPayments;
using Pos.Api.Models;
using Pos.Api.Services.Implementations;
using Pos.Test.Helpers;

namespace Pos.Test.Services;

public class DebtPaymentServiceTests
{
    private readonly AppDbContext _db;
    private readonly DebtPaymentService _sut;

    private static readonly Guid StaffId = Guid.NewGuid();
    private static readonly Guid CustomerId = Guid.NewGuid();
    private static readonly Guid InactiveCustomerId = Guid.NewGuid();

    public DebtPaymentServiceTests()
    {
        _db = DbContextFactory.Create(Guid.NewGuid().ToString());
        _sut = new DebtPaymentService(_db);
        Seed();
    }

    private void Seed()
    {
        _db.Users.Add(new User
        {
            Id = StaffId, Name = "Kasir", Username = "kasir",
            PasswordHash = "x", Role = UserRole.Kasir, IsActive = true
        });
        _db.Customers.AddRange(
            new Customer { Id = CustomerId, Name = "Budi", IsActive = true },
            new Customer { Id = InactiveCustomerId, Name = "Inactive", IsActive = false }
        );
        _db.SaveChanges();
    }

    // ── Create ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task Create_ValidRequest_ReturnsPayment()
    {
        var req = new CreateDebtPaymentRequest(CustomerId, 20000m, "cash", null, null);
        var (result, err) = await _sut.CreateAsync(req, StaffId);

        Assert.Null(err);
        Assert.NotNull(result);
        Assert.Equal(CustomerId, result!.CustomerId);
        Assert.Equal(20000m, result.Amount);
        Assert.Equal("cash", result.Method);
    }

    [Fact]
    public async Task Create_ValidRequest_PersistsToDatabase()
    {
        var req = new CreateDebtPaymentRequest(CustomerId, 15000m, "transfer", "REF-001", "note");
        var (result, _) = await _sut.CreateAsync(req, StaffId);

        Assert.True(_db.DebtPayments.Any(dp => dp.Id == result!.Id && dp.ReferenceNo == "REF-001"));
    }

    [Fact]
    public async Task Create_InactiveCustomer_ReturnsError()
    {
        var req = new CreateDebtPaymentRequest(InactiveCustomerId, 10000m, "cash", null, null);
        var (result, err) = await _sut.CreateAsync(req, StaffId);

        Assert.Null(result);
        Assert.NotNull(err);
    }

    [Fact]
    public async Task Create_NonExistentCustomer_ReturnsError()
    {
        var req = new CreateDebtPaymentRequest(Guid.NewGuid(), 10000m, "cash", null, null);
        var (result, err) = await _sut.CreateAsync(req, StaffId);

        Assert.Null(result);
        Assert.NotNull(err);
    }

    [Fact]
    public async Task Create_InvalidPaymentMethod_ReturnsError()
    {
        var req = new CreateDebtPaymentRequest(CustomerId, 10000m, "barter", null, null);
        var (result, err) = await _sut.CreateAsync(req, StaffId);

        Assert.Null(result);
        Assert.NotNull(err);
    }

    [Fact]
    public async Task Create_ZeroAmount_ReturnsError()
    {
        var req = new CreateDebtPaymentRequest(CustomerId, 0m, "cash", null, null);
        var (result, err) = await _sut.CreateAsync(req, StaffId);

        Assert.Null(result);
        Assert.NotNull(err);
    }

    [Fact]
    public async Task Create_NegativeAmount_ReturnsError()
    {
        var req = new CreateDebtPaymentRequest(CustomerId, -500m, "cash", null, null);
        var (result, err) = await _sut.CreateAsync(req, StaffId);

        Assert.Null(result);
        Assert.NotNull(err);
    }

    // ── GetAll ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetAll_TodayFilter_ReturnsPaymentsCreatedToday()
    {
        var req = new CreateDebtPaymentRequest(CustomerId, 5000m, "cash", null, null);
        await _sut.CreateAsync(req, StaffId);

        var today = Pos.Api.Services.WibTimeZone.TodayWib();
        var results = (await _sut.GetAllAsync(today)).ToList();

        Assert.NotEmpty(results);
        Assert.Contains(results, dp => dp.CustomerId == CustomerId && dp.Amount == 5000m);
    }

    [Fact]
    public async Task GetAll_FutureDate_ReturnsEmpty()
    {
        var req = new CreateDebtPaymentRequest(CustomerId, 5000m, "cash", null, null);
        await _sut.CreateAsync(req, StaffId);

        var future = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(2));
        var results = (await _sut.GetAllAsync(future)).ToList();

        Assert.Empty(results);
    }
}
