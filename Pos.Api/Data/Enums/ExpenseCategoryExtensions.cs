namespace Pos.Api.Data.Enums;

/// <summary>
/// Maps <see cref="ExpenseCategory"/> to/from API values (snake_case) and Indonesian display labels.
/// </summary>
public static class ExpenseCategoryExtensions
{
    private static readonly Dictionary<string, ExpenseCategory> ByApiValue = new(StringComparer.OrdinalIgnoreCase)
    {
        ["fuel"] = ExpenseCategory.Fuel,
        ["dues"] = ExpenseCategory.Dues,
        ["electricity"] = ExpenseCategory.Electricity,
        ["gallon_cap"] = ExpenseCategory.GallonCap,
        ["cleaning"] = ExpenseCategory.Cleaning,
        ["salary"] = ExpenseCategory.Salary,
        ["other"] = ExpenseCategory.Other,
    };

    private static readonly Dictionary<ExpenseCategory, string> ApiValueByCategory =
        ByApiValue.ToDictionary(kv => kv.Value, kv => kv.Key);

    private static readonly Dictionary<ExpenseCategory, string> Labels = new()
    {
        [ExpenseCategory.Fuel] = "Bensin",
        [ExpenseCategory.Dues] = "Iuran",
        [ExpenseCategory.Electricity] = "Listrik",
        [ExpenseCategory.GallonCap] = "Tutup Galon",
        [ExpenseCategory.Cleaning] = "Kebersihan",
        [ExpenseCategory.Salary] = "Gaji Karyawan",
        [ExpenseCategory.Other] = "Lainnya",
    };

    /// <summary>Parses an API value (e.g. "gallon_cap"); returns false when the value is unknown.</summary>
    public static bool TryParseApiValue(string? apiValue, out ExpenseCategory category)
    {
        if (!string.IsNullOrWhiteSpace(apiValue) && ByApiValue.TryGetValue(apiValue.Trim(), out category))
            return true;

        category = ExpenseCategory.Other;
        return false;
    }

    public static string ToApiValue(this ExpenseCategory category) => ApiValueByCategory[category];

    public static string ToLabel(this ExpenseCategory category) => Labels[category];
}
