namespace Pos.Api.Data.Enums;

/// <summary>
/// Kategori pengeluaran operasional (FR-CSH-006).
/// Stored as a string; API values are snake_case (see <see cref="ExpenseCategoryExtensions"/>).
/// </summary>
public enum ExpenseCategory
{
    Fuel,
    Dues,
    Electricity,
    GallonCap,
    Cleaning,
    Salary,
    Other
}
