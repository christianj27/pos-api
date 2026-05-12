using Microsoft.EntityFrameworkCore;
using Pos.Api.Data;
using Pos.Api.Data.Enums;
using Pos.Api.Models;

namespace Pos.Api.Data;

public static class DbSeeder
{
    public static async Task SeedAsync(AppDbContext db)
    {
        await db.Database.MigrateAsync();

        // Seed default owner if no users exist
        if (!await db.Users.AnyAsync())
        {
            db.Users.Add(new User
            {
                Name = "Owner",
                Username = "owner",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("owner1234", workFactor: 12),
                Role = UserRole.Owner,
                IsActive = true
            });

            // Seed default warehouse location
            db.Locations.Add(new Location
            {
                Name = "Gudang Utama",
                Type = LocationType.Warehouse,
                IsActive = true
            });

            await db.SaveChangesAsync();
        }
    }
}
