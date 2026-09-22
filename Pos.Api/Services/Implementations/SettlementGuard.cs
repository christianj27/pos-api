using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Pos.Api.Data;
using Pos.Api.Data.Enums;
using Pos.Api.Models;
using Pos.Api.Services.Interfaces;

namespace Pos.Api.Services.Implementations;

/// <summary>
/// Implements the two settlement gates (FR-STL-002).
/// All business dates are WIB calendar dates (hard midnight boundary, FR-STL-003).
/// </summary>
public class SettlementGuard(AppDbContext db, IConfiguration config) : ISettlementGuard
{
    public const string UnsettledPreviousDay = "UNSETTLED_PREVIOUS_DAY";
    public const string DayLocked = "DAY_LOCKED";

    private static readonly string[] MonthNames =
    [
        "Januari", "Februari", "Maret", "April", "Mei", "Juni",
        "Juli", "Agustus", "September", "Oktober", "November", "Desember"
    ];

    /// <summary>
    /// Days before this date are ignored, so switching settlement on never blocks a user over their
    /// existing history. An unset/invalid value disables enforcement entirely.
    /// </summary>
    private DateOnly EnforcementStartDate
    {
        get
        {
            var raw = config["Settlement:EnforcementStartDate"];
            return DateOnly.TryParse(raw, out var parsed) ? parsed : DateOnly.MaxValue;
        }
    }

    public static string FormatDateId(DateOnly date) =>
        $"{date.Day} {MonthNames[date.Month - 1]} {date.Year}";

    public async Task<SettlementBlock?> GetBlockAsync(Guid userId)
    {
        // The owner must be able to verify and to work, so Gate A never applies to them.
        if (await IsOwnerAsync(userId)) return null;

        var today = WibTimeZone.TodayWib();
        var start = EnforcementStartDate;

        var blocking = await db.DailySettlements
            .Where(s => s.UserId == userId
                        && s.Status != SettlementStatus.Approved
                        && s.BusinessDate < today
                        && s.BusinessDate >= start)
            .OrderBy(s => s.BusinessDate)
            .Select(s => new { s.Id, s.BusinessDate, s.Status })
            .FirstOrDefaultAsync();

        if (blocking is null) return null;

        var status = blocking.Status.ToString().ToLower();
        var message = status switch
        {
            "submitted" => $"Settlement {FormatDateId(blocking.BusinessDate)} sedang menunggu persetujuan owner.",
            "rejected" => $"Settlement {FormatDateId(blocking.BusinessDate)} ditolak owner. Perbaiki datanya lalu ajukan ulang.",
            _ => $"Selesaikan settlement {FormatDateId(blocking.BusinessDate)} dulu sebelum mencatat data baru."
        };

        return new SettlementBlock(UnsettledPreviousDay, message, blocking.BusinessDate, blocking.Id, status);
    }

    public async Task<string?> EnsureNewWorkAllowedAsync(Guid userId) =>
        (await GetBlockAsync(userId))?.Message;

    public async Task<string?> EnsureDayWritableAsync(Guid userId, DateOnly businessDate)
    {
        var approved = await db.DailySettlements
            .Where(s => s.UserId == userId
                        && s.BusinessDate == businessDate
                        && s.Status == SettlementStatus.Approved)
            .Select(s => (Guid?)s.Id)
            .FirstOrDefaultAsync();

        if (approved is null) return null;

        return $"Tanggal {FormatDateId(businessDate)} sudah settlement dan tidak bisa diubah lagi. " +
               "Minta owner membuka kembali settlement tersebut.";
    }

    public async Task TouchDayAsync(Guid userId, DateOnly businessDate)
    {
        // Check the change tracker first: a row added earlier in the same unit of work has not been
        // saved yet, and adding it twice would violate the unique (UserId, BusinessDate) index.
        if (db.DailySettlements.Local.Any(s => s.UserId == userId && s.BusinessDate == businessDate))
            return;

        var exists = await db.DailySettlements
            .AnyAsync(s => s.UserId == userId && s.BusinessDate == businessDate);

        if (exists) return;

        db.DailySettlements.Add(new DailySettlement
        {
            UserId = userId,
            BusinessDate = businessDate,
            Status = SettlementStatus.Open
        });
    }

    private async Task<bool> IsOwnerAsync(Guid userId)
    {
        var role = await db.Users
            .Where(u => u.Id == userId)
            .Select(u => (UserRole?)u.Role)
            .FirstOrDefaultAsync();

        return role == UserRole.Owner;
    }
}
