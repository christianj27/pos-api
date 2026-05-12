using Microsoft.EntityFrameworkCore;
using Pos.Api.Data;
using Pos.Api.DTOs.Profile;
using Pos.Api.Services.Interfaces;

namespace Pos.Api.Services.Implementations;

public class UserProfileService(AppDbContext db) : IUserProfileService
{
    public async Task<ProfileResponse?> GetProfileAsync(Guid userId)
    {
        var user = await db.Users.FindAsync(userId);
        return user is null
            ? null
            : new ProfileResponse(user.Id, user.Name, user.Username, user.Role.ToString().ToLower());
    }

    public async Task<(bool Success, string? Error)> UpdateProfileAsync(Guid userId, UpdateProfileRequest request)
    {
        var user = await db.Users.FindAsync(userId);
        if (user is null) return (false, "User not found.");

        if (!string.IsNullOrWhiteSpace(request.Name))
            user.Name = request.Name.Trim();

        if (!string.IsNullOrWhiteSpace(request.NewPassword))
        {
            if (string.IsNullOrWhiteSpace(request.CurrentPassword))
                return (false, "Kata sandi saat ini wajib diisi.");

            if (!BCrypt.Net.BCrypt.Verify(request.CurrentPassword, user.PasswordHash))
                return (false, "Kata sandi saat ini tidak sesuai.");

            if (request.NewPassword.Length < 8)
                return (false, "Kata sandi baru minimal 8 karakter.");

            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword, workFactor: 12);
        }

        await db.SaveChangesAsync();
        return (true, null);
    }
}
