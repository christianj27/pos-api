using Microsoft.Extensions.Configuration;
using Pos.Api.Data;
using Pos.Api.Data.Enums;
using Pos.Api.Models;
using Pos.Api.Services.Implementations;
using Pos.Test.Helpers;

namespace Pos.Test.Services;

public class AuthServiceTests
{
    private readonly AppDbContext _db;
    private readonly AuthService _sut;

    private static readonly Guid UserId = Guid.NewGuid();
    private const string RawPassword = "Test1234!";

    public AuthServiceTests()
    {
        _db = DbContextFactory.Create(Guid.NewGuid().ToString());

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:SecretKey"] = "super-secret-key-for-unit-tests-32chars!!",
                ["Jwt:Issuer"] = "pos-test",
                ["Jwt:Audience"] = "pos-test-audience",
                ["Jwt:AccessTokenExpiryMinutes"] = "60",
                ["Jwt:RefreshTokenExpiryDays"] = "7"
            })
            .Build();

        _sut = new AuthService(_db, config);

        SeedUser();
    }

    private void SeedUser()
    {
        _db.Users.Add(new User
        {
            Id = UserId,
            Name = "Test Owner",
            Username = "owner",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(RawPassword, workFactor: 12),
            Role = UserRole.Owner,
            IsActive = true
        });
        _db.SaveChanges();
    }

    // ── Login ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task Login_ValidCredentials_ReturnsTokenAndRole()
    {
        var result = await _sut.LoginAsync("owner", RawPassword);

        Assert.NotNull(result);
        Assert.Equal("owner", result!.Value.Response.Role);
        Assert.Equal(UserId, result.Value.Response.UserId);
        Assert.False(string.IsNullOrWhiteSpace(result.Value.Response.AccessToken));
        Assert.False(string.IsNullOrWhiteSpace(result.Value.RawRefreshToken));
    }

    [Fact]
    public async Task Login_ValidCredentials_PersistsRefreshToken()
    {
        await _sut.LoginAsync("owner", RawPassword);

        Assert.True(_db.RefreshTokens.Any(rt => rt.UserId == UserId && rt.RevokedAt == null));
    }

    [Fact]
    public async Task Login_WrongPassword_ReturnsNull()
    {
        var result = await _sut.LoginAsync("owner", "wrongpassword");
        Assert.Null(result);
    }

    [Fact]
    public async Task Login_UnknownUsername_ReturnsNull()
    {
        var result = await _sut.LoginAsync("nobody", RawPassword);
        Assert.Null(result);
    }

    [Fact]
    public async Task Login_InactiveUser_ReturnsNull()
    {
        var user = _db.Users.First(u => u.Id == UserId);
        user.IsActive = false;
        _db.SaveChanges();

        var result = await _sut.LoginAsync("owner", RawPassword);
        Assert.Null(result);

        // restore
        user.IsActive = true;
        _db.SaveChanges();
    }

    // ── Refresh ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Refresh_ValidToken_ReturnsNewAccessToken()
    {
        var login = await _sut.LoginAsync("owner", RawPassword);
        var rawToken = login!.Value.RawRefreshToken;

        var refreshResult = await _sut.RefreshAsync(rawToken);

        Assert.NotNull(refreshResult);
        Assert.False(string.IsNullOrWhiteSpace(refreshResult!.Value.Response.AccessToken));
    }

    [Fact]
    public async Task Refresh_ValidToken_RotatesRefreshToken()
    {
        var login = await _sut.LoginAsync("owner", RawPassword);
        var oldRaw = login!.Value.RawRefreshToken;

        var refreshResult = await _sut.RefreshAsync(oldRaw);

        // Old token should now be revoked
        var oldHash = AuthService.HashToken(oldRaw);
        var storedOld = _db.RefreshTokens.First(rt => rt.TokenHash == oldHash);
        Assert.NotNull(storedOld.RevokedAt);

        // A new valid token should exist
        var newHash = AuthService.HashToken(refreshResult!.Value.RawRefreshToken);
        Assert.True(_db.RefreshTokens.Any(rt => rt.TokenHash == newHash && rt.RevokedAt == null));
    }

    [Fact]
    public async Task Refresh_RevokedToken_ReturnsNull()
    {
        var login = await _sut.LoginAsync("owner", RawPassword);
        var rawToken = login!.Value.RawRefreshToken;

        // Revoke it manually
        var hash = AuthService.HashToken(rawToken);
        var stored = _db.RefreshTokens.First(rt => rt.TokenHash == hash);
        stored.RevokedAt = DateTime.UtcNow;
        _db.SaveChanges();

        var result = await _sut.RefreshAsync(rawToken);
        Assert.Null(result);
    }

    [Fact]
    public async Task Refresh_ExpiredToken_ReturnsNull()
    {
        var login = await _sut.LoginAsync("owner", RawPassword);
        var rawToken = login!.Value.RawRefreshToken;

        // Expire it
        var hash = AuthService.HashToken(rawToken);
        var stored = _db.RefreshTokens.First(rt => rt.TokenHash == hash);
        stored.ExpiresAt = DateTime.UtcNow.AddDays(-1);
        _db.SaveChanges();

        var result = await _sut.RefreshAsync(rawToken);
        Assert.Null(result);
    }

    [Fact]
    public async Task Refresh_UnknownToken_ReturnsNull()
    {
        var result = await _sut.RefreshAsync("completely-unknown-token");
        Assert.Null(result);
    }

    // ── Logout ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task Logout_ValidToken_RevokesIt()
    {
        var login = await _sut.LoginAsync("owner", RawPassword);
        var rawToken = login!.Value.RawRefreshToken;

        await _sut.LogoutAsync(rawToken);

        var hash = AuthService.HashToken(rawToken);
        var stored = _db.RefreshTokens.First(rt => rt.TokenHash == hash);
        Assert.NotNull(stored.RevokedAt);
    }

    [Fact]
    public async Task Logout_UnknownToken_DoesNotThrow()
    {
        var exception = await Record.ExceptionAsync(() => _sut.LogoutAsync("ghost-token"));
        Assert.Null(exception);
    }

    // ── HashToken ──────────────────────────────────────────────────────────

    [Fact]
    public void HashToken_SameInput_ProducesSameHash()
    {
        var token = "some-raw-token";
        Assert.Equal(AuthService.HashToken(token), AuthService.HashToken(token));
    }

    [Fact]
    public void HashToken_DifferentInputs_ProduceDifferentHashes()
    {
        Assert.NotEqual(AuthService.HashToken("token-a"), AuthService.HashToken("token-b"));
    }
}
