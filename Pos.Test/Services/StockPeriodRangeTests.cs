using Pos.Api.Services;

namespace Pos.Test.Services;

/// <summary>FR-DSH-012 — period selector resolution for the "Pergerakan Stok" section.</summary>
public class StockPeriodRangeTests
{
    /// <summary>Wednesday, so its calendar week is Monday 2026-03-16 .. Sunday 2026-03-22.</summary>
    private static readonly DateOnly Wednesday = new(2026, 3, 18);

    private static bool TryResolve(
        string? period, DateOnly anchor, DateOnly? start, DateOnly? end,
        out string normalized, out DateOnly rangeStart, out DateOnly rangeEnd, out string? error) =>
        StockPeriodRange.TryResolve(period, anchor, start, end, out normalized, out rangeStart, out rangeEnd, out error);

    [Fact]
    public void TryResolve_OmittedPeriod_DefaultsToDay()
    {
        Assert.True(TryResolve(null, Wednesday, null, null, out var period, out var start, out var end, out _));
        Assert.Equal("day", period);
        Assert.Equal(Wednesday, start);
        Assert.Equal(Wednesday, end);
    }

    [Fact]
    public void TryResolve_Day_ReturnsAnchorDay()
    {
        Assert.True(TryResolve("day", Wednesday, null, null, out _, out var start, out var end, out _));
        Assert.Equal(Wednesday, start);
        Assert.Equal(Wednesday, end);
    }

    [Theory]
    [InlineData(2026, 3, 16)] // Monday
    [InlineData(2026, 3, 18)] // Wednesday
    [InlineData(2026, 3, 22)] // Sunday
    public void TryResolve_Week_ReturnsMondayToSunday(int year, int month, int day)
    {
        Assert.True(TryResolve("week", new DateOnly(year, month, day), null, null,
            out _, out var start, out var end, out _));
        Assert.Equal(new DateOnly(2026, 3, 16), start);
        Assert.Equal(new DateOnly(2026, 3, 22), end);
    }

    [Fact]
    public void TryResolve_Week_CrossingMonthBoundary_SpansBothMonths()
    {
        // 2026-04-01 is a Wednesday → week runs Mon 2026-03-30 .. Sun 2026-04-05
        Assert.True(TryResolve("week", new DateOnly(2026, 4, 1), null, null,
            out _, out var start, out var end, out _));
        Assert.Equal(new DateOnly(2026, 3, 30), start);
        Assert.Equal(new DateOnly(2026, 4, 5), end);
    }

    [Theory]
    [InlineData(2026, 3, 1, 2026, 3, 31)]   // March
    [InlineData(2026, 2, 14, 2026, 2, 28)]  // February, non-leap
    [InlineData(2024, 2, 14, 2024, 2, 29)]  // February, leap year
    public void TryResolve_Month_ReturnsFirstToLastDay(int ay, int am, int ad, int ey, int em, int ed)
    {
        Assert.True(TryResolve("month", new DateOnly(ay, am, ad), null, null,
            out _, out var start, out var end, out _));
        Assert.Equal(new DateOnly(ay, am, 1), start);
        Assert.Equal(new DateOnly(ey, em, ed), end);
    }

    [Fact]
    public void TryResolve_Year_ReturnsJanuaryToDecember()
    {
        Assert.True(TryResolve("year", Wednesday, null, null, out _, out var start, out var end, out _));
        Assert.Equal(new DateOnly(2026, 1, 1), start);
        Assert.Equal(new DateOnly(2026, 12, 31), end);
    }

    [Fact]
    public void TryResolve_Custom_UsesSuppliedBounds()
    {
        Assert.True(TryResolve("custom", Wednesday, new DateOnly(2026, 1, 5), new DateOnly(2026, 1, 9),
            out var period, out var start, out var end, out _));
        Assert.Equal("custom", period);
        Assert.Equal(new DateOnly(2026, 1, 5), start);
        Assert.Equal(new DateOnly(2026, 1, 9), end);
    }

    [Fact]
    public void TryResolve_OmittedPeriodWithBothBounds_BecomesCustom()
    {
        Assert.True(TryResolve(null, Wednesday, new DateOnly(2026, 1, 5), new DateOnly(2026, 1, 9),
            out var period, out var start, out var end, out _));
        Assert.Equal("custom", period);
        Assert.Equal(new DateOnly(2026, 1, 5), start);
        Assert.Equal(new DateOnly(2026, 1, 9), end);
    }

    [Fact]
    public void TryResolve_PeriodIsTrimmedAndCaseInsensitive()
    {
        Assert.True(TryResolve("  WEEK ", Wednesday, null, null, out var period, out _, out _, out _));
        Assert.Equal("week", period);
    }

    [Fact]
    public void TryResolve_UnknownPeriod_Fails()
    {
        Assert.False(TryResolve("fortnight", Wednesday, null, null,
            out _, out _, out _, out var error));
        Assert.Equal("Periode tidak valid.", error);
    }

    [Theory]
    [InlineData(null, null)]                  // no bounds
    [InlineData("2026-03-01", null)]          // start only
    [InlineData(null, "2026-03-10")]          // end only
    public void TryResolve_Custom_MissingBounds_Fails(string? start, string? end)
    {
        var startDate = start is null ? (DateOnly?)null : DateOnly.Parse(start);
        var endDate = end is null ? (DateOnly?)null : DateOnly.Parse(end);

        Assert.False(TryResolve("custom", Wednesday, startDate, endDate,
            out var period, out _, out _, out var error));
        Assert.Equal("custom", period);
        Assert.Equal("Tanggal mulai dan tanggal selesai wajib diisi.", error);
    }

    [Fact]
    public void TryResolve_Custom_StartAfterEnd_Fails()
    {
        Assert.False(TryResolve("custom", Wednesday, new DateOnly(2026, 3, 10), new DateOnly(2026, 3, 1),
            out _, out _, out _, out var error));
        Assert.Equal("Tanggal mulai harus sebelum atau sama dengan tanggal selesai.", error);
    }

    [Fact]
    public void TryResolve_Custom_SingleDayRange_Succeeds()
    {
        Assert.True(TryResolve("custom", Wednesday, new DateOnly(2026, 3, 10), new DateOnly(2026, 3, 10),
            out _, out var start, out var end, out _));
        Assert.Equal(start, end);
    }

    [Fact]
    public void TryResolve_Custom_EndInFuture_Fails()
    {
        var today = WibTimeZone.TodayWib();

        Assert.False(TryResolve("custom", today, today, today.AddDays(1),
            out _, out _, out _, out var error));
        Assert.Equal("Tanggal tidak boleh di masa depan.", error);
    }

    [Fact]
    public void TryResolve_Custom_EndingToday_Succeeds()
    {
        var today = WibTimeZone.TodayWib();

        Assert.True(TryResolve("custom", today, today.AddDays(-3), today,
            out _, out var start, out var end, out _));
        Assert.Equal(today.AddDays(-3), start);
        Assert.Equal(today, end);
    }
}
