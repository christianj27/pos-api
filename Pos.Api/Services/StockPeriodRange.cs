namespace Pos.Api.Services;

/// <summary>
/// Resolves the FR-DSH-012 "Pergerakan Stok" period selector into an inclusive WIB date range.
/// Mirrors <c>utils/stockPeriod.ts</c> on the frontend (mock mode needs the same resolution client-side).
/// </summary>
public static class StockPeriodRange
{
    public const string Day = "day";
    public const string Week = "week";
    public const string Month = "month";
    public const string Year = "year";
    public const string Custom = "custom";

    /// <summary>
    /// Normalises the requested period. An omitted period means <see cref="Custom"/> when both range bounds
    /// are supplied (mirrors the cash-flow <c>start_date</c>/<c>end_date</c> convention), otherwise <see cref="Day"/>.
    /// </summary>
    public static string NormalizePeriod(string? period, DateOnly? startDate, DateOnly? endDate) =>
        string.IsNullOrWhiteSpace(period)
            ? startDate.HasValue && endDate.HasValue ? Custom : Day
            : period.Trim().ToLowerInvariant();

    /// <summary>Resolves the period into an inclusive WIB date range, or returns an Indonesian error message.</summary>
    public static bool TryResolve(
        string? period,
        DateOnly anchor,
        DateOnly? startDate,
        DateOnly? endDate,
        out string normalizedPeriod,
        out DateOnly rangeStart,
        out DateOnly rangeEnd,
        out string? error)
    {
        normalizedPeriod = NormalizePeriod(period, startDate, endDate);
        error = null;
        rangeStart = anchor;
        rangeEnd = anchor;

        switch (normalizedPeriod)
        {
            case Day:
                return true;

            case Week:
                // Calendar week, Monday–Sunday, containing the anchor date.
                var mondayOffset = ((int)anchor.DayOfWeek + 6) % 7;
                rangeStart = anchor.AddDays(-mondayOffset);
                rangeEnd = rangeStart.AddDays(6);
                return true;

            case Month:
                rangeStart = new DateOnly(anchor.Year, anchor.Month, 1);
                rangeEnd = rangeStart.AddMonths(1).AddDays(-1);
                return true;

            case Year:
                rangeStart = new DateOnly(anchor.Year, 1, 1);
                rangeEnd = new DateOnly(anchor.Year, 12, 31);
                return true;

            case Custom:
                if (startDate is null || endDate is null)
                {
                    error = "Tanggal mulai dan tanggal selesai wajib diisi.";
                    return false;
                }
                if (startDate > endDate)
                {
                    error = "Tanggal mulai harus sebelum atau sama dengan tanggal selesai.";
                    return false;
                }
                if (endDate > WibTimeZone.TodayWib())
                {
                    error = "Tanggal tidak boleh di masa depan.";
                    return false;
                }
                rangeStart = startDate.Value;
                rangeEnd = endDate.Value;
                return true;

            default:
                error = "Periode tidak valid.";
                return false;
        }
    }
}
