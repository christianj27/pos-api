using System.Runtime.InteropServices;

namespace Pos.Api.Services;

public static class WibTimeZone
{
    private static readonly TimeZoneInfo Zone = TimeZoneInfo.FindSystemTimeZoneById(
        RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? "SE Asia Standard Time"
            : "Asia/Jakarta");

    public static (DateTime Start, DateTime End) GetUtcDayBounds(DateOnly date)
    {
        var start = TimeZoneInfo.ConvertTimeToUtc(date.ToDateTime(TimeOnly.MinValue), Zone);
        return (start, start.AddDays(1));
    }

    public static DateOnly TodayWib() =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, Zone));

    /// <summary>Converts a UTC timestamp to wall-clock time in WIB.</summary>
    public static DateTime ToWib(DateTime utc) => TimeZoneInfo.ConvertTimeFromUtc(utc, Zone);
}
