using Pos.Api.Data;
using Pos.Api.Data.Enums;
using Pos.Api.DTOs.Expenses;
using Pos.Api.Models;
using Pos.Api.Services.Implementations;
using Pos.Test.Helpers;

namespace Pos.Test.Services;

public class ExpenseServiceTests
{
    private readonly AppDbContext _db;
    private readonly ExpenseService _sut;

    private static readonly Guid OwnerId = Guid.NewGuid();
    private static readonly DateOnly Today = Pos.Api.Services.WibTimeZone.TodayWib();

    public ExpenseServiceTests()
    {
        _db = DbContextFactory.Create(Guid.NewGuid().ToString());
        _sut = new ExpenseService(_db, DbContextFactory.CreateGuard(_db));

        _db.Users.Add(new User
        {
            Id = OwnerId, Name = "Budi", Username = "owner",
            PasswordHash = "x", Role = UserRole.Owner, IsActive = true
        });
        _db.SaveChanges();
    }

    private static CreateExpenseRequest Request(
        string category = "fuel",
        string description = "Isi bensin truk",
        decimal amount = 75000m,
        DateOnly? expenseDate = null) =>
        new(category, description, amount, expenseDate ?? Today);

    // ── Create ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task CreateAsync_ValidRequest_ReturnsExpenseAndPersists()
    {
        var (expense, error) = await _sut.CreateAsync(Request(), OwnerId);

        Assert.Null(error);
        Assert.NotNull(expense);
        Assert.Equal("fuel", expense!.Category);
        Assert.Equal("Isi bensin truk", expense.Description);
        Assert.Equal(75000m, expense.Amount);
        Assert.Equal(Today, expense.ExpenseDate);
        Assert.Equal("Budi", expense.CreatedByName);
        Assert.Single(_db.Expenses);
    }

    [Theory]
    [InlineData("fuel", ExpenseCategory.Fuel)]
    [InlineData("dues", ExpenseCategory.Dues)]
    [InlineData("electricity", ExpenseCategory.Electricity)]
    [InlineData("gallon_cap", ExpenseCategory.GallonCap)]
    [InlineData("cleaning", ExpenseCategory.Cleaning)]
    [InlineData("salary", ExpenseCategory.Salary)]
    [InlineData("other", ExpenseCategory.Other)]
    public async Task CreateAsync_ParsesAllApiCategoryValues(string apiValue, ExpenseCategory expected)
    {
        var (expense, error) = await _sut.CreateAsync(Request(category: apiValue), OwnerId);

        Assert.Null(error);
        Assert.Equal(expected.ToApiValue(), expense!.Category);
    }

    [Fact]
    public async Task CreateAsync_TrimsDescription()
    {
        var (expense, _) = await _sut.CreateAsync(Request(description: "  Tutup galon  "), OwnerId);

        Assert.Equal("Tutup galon", expense!.Description);
    }

    [Theory]
    [InlineData("", "Kategori pengeluaran wajib dipilih.")]
    [InlineData("   ", "Kategori pengeluaran wajib dipilih.")]
    [InlineData("nope", "Kategori pengeluaran tidak valid.")]
    public async Task CreateAsync_InvalidCategory_ReturnsError(string category, string expectedError)
    {
        var (expense, error) = await _sut.CreateAsync(Request(category: category), OwnerId);

        Assert.Null(expense);
        Assert.Equal(expectedError, error);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task CreateAsync_EmptyDescription_ReturnsError(string description)
    {
        var (expense, error) = await _sut.CreateAsync(Request(description: description), OwnerId);

        Assert.Null(expense);
        Assert.Equal("Deskripsi pengeluaran wajib diisi.", error);
    }

    [Fact]
    public async Task CreateAsync_DescriptionTooLong_ReturnsError()
    {
        var (expense, error) = await _sut.CreateAsync(Request(description: new string('a', 256)), OwnerId);

        Assert.Null(expense);
        Assert.Equal("Deskripsi pengeluaran tidak boleh lebih dari 255 karakter.", error);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1000)]
    public async Task CreateAsync_NonPositiveAmount_ReturnsError(decimal amount)
    {
        var (expense, error) = await _sut.CreateAsync(Request(amount: amount), OwnerId);

        Assert.Null(expense);
        Assert.Equal("Jumlah pengeluaran harus berupa angka positif.", error);
    }

    [Fact]
    public async Task CreateAsync_FutureExpenseDate_ReturnsError()
    {
        var (expense, error) = await _sut.CreateAsync(Request(expenseDate: Today.AddDays(1)), OwnerId);

        Assert.Null(expense);
        Assert.Equal("Tanggal pengeluaran tidak boleh di masa depan.", error);
    }

    // ── Get ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetAllAsync_ReturnsOnlyExpensesOnGivenBusinessDate()
    {
        await _sut.CreateAsync(Request(), OwnerId);
        await _sut.CreateAsync(
            Request(description: "Iuran sampah", category: "dues", expenseDate: Today.AddDays(-1)), OwnerId);

        var result = (await _sut.GetAllAsync(Today)).ToList();

        Assert.Single(result);
        Assert.Equal("Isi bensin truk", result[0].Description);
    }

    [Fact]
    public async Task GetAllAsync_NoDate_DefaultsToTodayWib()
    {
        await _sut.CreateAsync(Request(), OwnerId);

        var result = await _sut.GetAllAsync(null);

        Assert.Single(result);
    }

    // ── Update ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task UpdateAsync_ValidRequest_UpdatesFields()
    {
        var (created, _) = await _sut.CreateAsync(Request(), OwnerId);

        var (updated, notFound, error) = await _sut.UpdateAsync(created!.Id,
            new UpdateExpenseRequest("salary", "Gaji karyawan mingguan", 250000m, Today.AddDays(-1)));

        Assert.False(notFound);
        Assert.Null(error);
        Assert.Equal("salary", updated!.Category);
        Assert.Equal("Gaji karyawan mingguan", updated.Description);
        Assert.Equal(250000m, updated.Amount);
        Assert.Equal(Today.AddDays(-1), updated.ExpenseDate);
    }

    [Fact]
    public async Task UpdateAsync_UnknownId_ReturnsNotFound()
    {
        var (updated, notFound, error) = await _sut.UpdateAsync(
            Guid.NewGuid(), new UpdateExpenseRequest("fuel", "Isi bensin", 1m, Today));

        Assert.Null(updated);
        Assert.True(notFound);
        Assert.Equal("Pengeluaran tidak ditemukan.", error);
    }

    [Fact]
    public async Task UpdateAsync_InvalidPayload_ReturnsValidationError()
    {
        var (created, _) = await _sut.CreateAsync(Request(), OwnerId);

        var (updated, notFound, error) = await _sut.UpdateAsync(
            created!.Id, new UpdateExpenseRequest("fuel", "  ", 0m, Today));

        Assert.Null(updated);
        Assert.False(notFound);
        Assert.Equal("Deskripsi pengeluaran wajib diisi.", error);
    }

    // ── Delete ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task DeleteAsync_ExistingExpense_RemovesRow()
    {
        var (created, _) = await _sut.CreateAsync(Request(), OwnerId);

        var (success, notFound, error) = await _sut.DeleteAsync(created!.Id);

        Assert.True(success);
        Assert.False(notFound);
        Assert.Null(error);
        Assert.Empty(_db.Expenses);
    }

    [Fact]
    public async Task DeleteAsync_UnknownId_ReturnsNotFound()
    {
        var (success, notFound, error) = await _sut.DeleteAsync(Guid.NewGuid());

        Assert.False(success);
        Assert.True(notFound);
        Assert.Equal("Pengeluaran tidak ditemukan.", error);
    }
}
