using Microsoft.EntityFrameworkCore;
using Pos.Api.Data;
using Pos.Api.Data.Enums;
using Pos.Api.DTOs.Users;
using Pos.Api.Models;
using Pos.Api.Services.Interfaces;

namespace Pos.Api.Services.Implementations;

public class UserService(AppDbContext db) : IUserService
{
    public async Task<IEnumerable<UserResponse>> GetAllAsync() =>
        await db.Users
            .OrderBy(u => u.IsActive ? 0 : 1)
            .ThenBy(u => u.Name)
            .Select(u => MapToResponse(u))
            .ToListAsync();

    public async Task<UserResponse?> GetByIdAsync(Guid id)
    {
        var user = await db.Users.FindAsync(id);
        return user is null ? null : MapToResponse(user);
    }

    public async Task<(UserResponse? User, string? Error)> CreateAsync(CreateUserRequest request)
    {
        if (!Enum.TryParse<UserRole>(request.Role, ignoreCase: true, out var role))
            return (null, "Peran wajib dipilih.");

        if (await db.Users.AnyAsync(u => u.Username == request.Username))
            return (null, "Username ini sudah digunakan.");

        var user = new User
        {
            Name = request.Name.Trim(),
            Username = request.Username.Trim(),
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password, workFactor: 12),
            Role = role
        };

        db.Users.Add(user);
        await db.SaveChangesAsync();
        return (MapToResponse(user), null);
    }

    public async Task<(UserResponse? User, string? Error)> UpdateAsync(Guid id, UpdateUserRequest request, Guid currentUserId)
    {
        var user = await db.Users.FindAsync(id);
        if (user is null) return (null, "User not found.");

        if (!Enum.TryParse<UserRole>(request.Role, ignoreCase: true, out var role))
            return (null, "Peran wajib dipilih.");

        if (await db.Users.AnyAsync(u => u.Username == request.Username && u.Id != id))
            return (null, "Username ini sudah digunakan.");

        user.Name = request.Name.Trim();
        user.Username = request.Username.Trim();
        user.Role = role;
        user.IsActive = request.IsActive;

        if (!string.IsNullOrWhiteSpace(request.Password))
            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password, workFactor: 12);

        await db.SaveChangesAsync();
        return (MapToResponse(user), null);
    }

    public async Task<(bool Success, string? Error)> DeactivateAsync(Guid id, Guid currentUserId)
    {
        if (id == currentUserId)
            return (false, "Tidak bisa menonaktifkan akun sendiri.");

        var user = await db.Users.FindAsync(id);
        if (user is null) return (false, "User not found.");

        user.IsActive = false;
        await db.SaveChangesAsync();
        return (true, null);
    }

    private static UserResponse MapToResponse(User u) =>
        new(u.Id, u.Name, u.Username, u.Role.ToString().ToLower(), u.IsActive, u.CreatedAt);
}
