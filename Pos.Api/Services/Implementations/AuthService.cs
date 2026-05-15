using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Pos.Api.Data;
using Pos.Api.DTOs.Auth;
using Pos.Api.Models;
using Pos.Api.Services.Interfaces;

namespace Pos.Api.Services.Implementations;

public class AuthService(AppDbContext db, IConfiguration config) : IAuthService
{
    public async Task<(LoginResponse Response, string RawRefreshToken)?> LoginAsync(string username, string password)
    {
        var user = await db.Users
            .FirstOrDefaultAsync(u => u.Username == username && u.IsActive);

        if (user is null || !BCrypt.Net.BCrypt.Verify(password, user.PasswordHash))
            return null;

        var accessToken = GenerateAccessToken(user);
        var rawRefresh = GenerateRawToken();
        await PersistRefreshTokenAsync(user.Id, rawRefresh);

        return (new LoginResponse(accessToken, user.Role.ToString().ToLower(), user.Id, user.Name, user.Username), rawRefresh);
    }

    public async Task<(RefreshResponse Response, string RawRefreshToken)?> RefreshAsync(string rawToken)
    {
        var hash = HashToken(rawToken);
        var stored = await db.RefreshTokens
            .Include(rt => rt.User)
            .FirstOrDefaultAsync(rt => rt.TokenHash == hash
                                       && rt.RevokedAt == null
                                       && rt.ExpiresAt > DateTime.UtcNow);

        if (stored is null || !stored.User.IsActive)
            return null;

        stored.RevokedAt = DateTime.UtcNow;

        var newRaw = GenerateRawToken();
        await PersistRefreshTokenAsync(stored.User.Id, newRaw);

        return (new RefreshResponse(GenerateAccessToken(stored.User)), newRaw);
    }

    public async Task LogoutAsync(string rawToken)
    {
        var hash = HashToken(rawToken);
        var stored = await db.RefreshTokens
            .FirstOrDefaultAsync(rt => rt.TokenHash == hash && rt.RevokedAt == null);

        if (stored is not null)
        {
            stored.RevokedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
        }
    }

    // -- Helpers ---------------------------------------------------------------

    private string GenerateAccessToken(User user)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(config["Jwt:SecretKey"]!));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var expiry = int.Parse(config["Jwt:AccessTokenExpiryMinutes"] ?? "60");

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.UniqueName, user.Username),
            new Claim(ClaimTypes.Role, user.Role.ToString().ToLower()),
            new Claim("name", user.Name),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        var token = new JwtSecurityToken(
            issuer: config["Jwt:Issuer"],
            audience: config["Jwt:Audience"],
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(expiry),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private static string GenerateRawToken() =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));

    private async Task PersistRefreshTokenAsync(Guid userId, string raw)
    {
        var expiryDays = int.Parse(config["Jwt:RefreshTokenExpiryDays"] ?? "7");
        db.RefreshTokens.Add(new RefreshToken
        {
            UserId = userId,
            TokenHash = HashToken(raw),
            ExpiresAt = DateTime.UtcNow.AddDays(expiryDays)
        });
        await db.SaveChangesAsync();
    }

    public static string HashToken(string raw)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(raw));
        return Convert.ToHexString(bytes).ToLower();
    }
}
