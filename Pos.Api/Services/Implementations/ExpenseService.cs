using Microsoft.EntityFrameworkCore;
using Pos.Api.Data;
using Pos.Api.Data.Enums;
using Pos.Api.DTOs.Expenses;
using Pos.Api.Models;
using Pos.Api.Services.Interfaces;

namespace Pos.Api.Services.Implementations;

public class ExpenseService(AppDbContext db) : IExpenseService
{
    private const string NotFoundError = "Pengeluaran tidak ditemukan.";

    public async Task<IEnumerable<ExpenseResponse>> GetAllAsync(DateOnly? date)
    {
        var filter = date ?? WibTimeZone.TodayWib();

        var expenses = await db.Expenses
            .Include(e => e.Creator)
            .Where(e => e.ExpenseDate == filter)
            .OrderByDescending(e => e.CreatedAt)
            .ToListAsync();

        return expenses.Select(e => Map(e, e.Creator.Name)).ToList();
    }

    public async Task<(ExpenseResponse? Expense, string? Error)> CreateAsync(
        CreateExpenseRequest request, Guid createdBy)
    {
        var error = Validate(request.Category, request.Description, request.Amount, request.ExpenseDate, out var category);
        if (error is not null) return (null, error);

        var expense = new Expense
        {
            Category = category,
            Description = request.Description.Trim(),
            Amount = request.Amount,
            ExpenseDate = request.ExpenseDate,
            CreatedBy = createdBy
        };
        db.Expenses.Add(expense);
        await db.SaveChangesAsync();

        var creator = await db.Users.FindAsync(createdBy);
        return (Map(expense, creator?.Name ?? string.Empty), null);
    }

    public async Task<(ExpenseResponse? Expense, bool NotFound, string? Error)> UpdateAsync(
        Guid id, UpdateExpenseRequest request)
    {
        var expense = await db.Expenses.Include(e => e.Creator).FirstOrDefaultAsync(e => e.Id == id);
        if (expense is null) return (null, true, NotFoundError);

        var error = Validate(request.Category, request.Description, request.Amount, request.ExpenseDate, out var category);
        if (error is not null) return (null, false, error);

        expense.Category = category;
        expense.Description = request.Description.Trim();
        expense.Amount = request.Amount;
        expense.ExpenseDate = request.ExpenseDate;
        await db.SaveChangesAsync();

        return (Map(expense, expense.Creator.Name), false, null);
    }

    public async Task<(bool Success, bool NotFound, string? Error)> DeleteAsync(Guid id)
    {
        var expense = await db.Expenses.FindAsync(id);
        if (expense is null) return (false, true, NotFoundError);

        db.Expenses.Remove(expense);
        await db.SaveChangesAsync();
        return (true, false, null);
    }

    private static string? Validate(
        string? category, string? description, decimal amount, DateOnly expenseDate,
        out ExpenseCategory parsedCategory)
    {
        parsedCategory = ExpenseCategory.Other;

        if (string.IsNullOrWhiteSpace(category))
            return "Kategori pengeluaran wajib dipilih.";

        if (!ExpenseCategoryExtensions.TryParseApiValue(category, out parsedCategory))
            return "Kategori pengeluaran tidak valid.";

        if (string.IsNullOrWhiteSpace(description))
            return "Deskripsi pengeluaran wajib diisi.";

        if (description.Trim().Length > 255)
            return "Deskripsi pengeluaran tidak boleh lebih dari 255 karakter.";

        if (amount <= 0)
            return "Jumlah pengeluaran harus berupa angka positif.";

        if (expenseDate > WibTimeZone.TodayWib())
            return "Tanggal pengeluaran tidak boleh di masa depan.";

        return null;
    }

    private static ExpenseResponse Map(Expense expense, string createdByName) =>
        new(expense.Id, expense.Category.ToApiValue(), expense.Description, expense.Amount,
            expense.ExpenseDate, createdByName, expense.CreatedAt);
}
