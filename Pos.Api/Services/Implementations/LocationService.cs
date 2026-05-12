using Microsoft.EntityFrameworkCore;
using Pos.Api.Data;
using Pos.Api.Data.Enums;
using Pos.Api.DTOs.Locations;
using Pos.Api.Models;
using Pos.Api.Services.Interfaces;

namespace Pos.Api.Services.Implementations;

public class LocationService(AppDbContext db) : ILocationService
{
    public async Task<IEnumerable<LocationResponse>> GetAllAsync() =>
        await db.Locations
            .Include(l => l.AssignedUser)
            .OrderBy(l => l.Name)
            .Select(l => MapToResponse(l))
            .ToListAsync();

    public async Task<LocationResponse?> GetByIdAsync(Guid id)
    {
        var loc = await db.Locations.Include(l => l.AssignedUser).FirstOrDefaultAsync(l => l.Id == id);
        return loc is null ? null : MapToResponse(loc);
    }

    public async Task<(LocationResponse? Location, string? Error)> CreateAsync(CreateLocationRequest request)
    {
        if (!Enum.TryParse<LocationType>(request.Type, ignoreCase: true, out var type))
            return (null, "Tipe lokasi wajib dipilih.");

        if (type == LocationType.Vehicle && request.AssignedTo is null)
            return (null, "Pengguna wajib dipilih untuk kendaraan.");

        if (request.AssignedTo.HasValue)
        {
            var assignee = await db.Users.FindAsync(request.AssignedTo.Value);
            if (assignee is null || !assignee.IsActive)
                return (null, "Pengguna tidak ditemukan atau tidak aktif.");
        }

        var location = new Location
        {
            Name = request.Name.Trim(),
            Type = type,
            AssignedTo = request.AssignedTo
        };

        db.Locations.Add(location);
        await db.SaveChangesAsync();

        var created = await db.Locations.Include(l => l.AssignedUser).FirstAsync(l => l.Id == location.Id);
        return (MapToResponse(created), null);
    }

    public async Task<(LocationResponse? Location, string? Error)> UpdateAsync(Guid id, UpdateLocationRequest request)
    {
        var location = await db.Locations.Include(l => l.AssignedUser).FirstOrDefaultAsync(l => l.Id == id);
        if (location is null) return (null, "Location not found.");

        if (location.Type == LocationType.Vehicle && request.AssignedTo is null)
            return (null, "Pengguna wajib dipilih untuk kendaraan.");

        if (request.AssignedTo.HasValue)
        {
            var assignee = await db.Users.FindAsync(request.AssignedTo.Value);
            if (assignee is null || !assignee.IsActive)
                return (null, "Pengguna tidak ditemukan atau tidak aktif.");
        }

        location.Name = request.Name.Trim();
        location.AssignedTo = request.AssignedTo;
        location.IsActive = request.IsActive;

        await db.SaveChangesAsync();
        await db.Entry(location).Reference(l => l.AssignedUser).LoadAsync();
        return (MapToResponse(location), null);
    }

    private static LocationResponse MapToResponse(Location l) =>
        new(l.Id, l.Name, l.Type.ToString().ToLower(), l.AssignedTo, l.AssignedUser?.Name, l.IsActive, l.CreatedAt);
}
