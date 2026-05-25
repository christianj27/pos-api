using Pos.Api.Data;
using Pos.Api.Data.Enums;
using Pos.Api.DTOs.Profile;
using Pos.Api.Models;
using Pos.Api.Services.Implementations;
using Pos.Test.Helpers;

namespace Pos.Test.Services;

public class UserProfileServiceTests
{
    private readonly AppDbContext _db;
    private readonly UserProfileService _sut;

    private static readonly Guid UserId = Guid.NewGuid();
    private const string RawPassword = "Secret123!";

    public UserProfileServiceTests()
    {
        _db = DbContextFactory.Create(Guid.NewGuid().ToString());
        _sut = new UserProfileService(_db);
        SeedUser();
    }

    private void SeedUser()
    {
        _db.Users.Add(new User
        {
            Id = UserId,
            Name = "John Doe",
            Username = "johndoe",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(RawPassword, workFactor: 12),
            Role = UserRole.Kasir,
            IsActive = true
        });
        _db.SaveChanges();
    }

    // ── GetProfile ─────────────────────────────────────────────────────────

    [Fact]
    public async Task GetProfile_ExistingUser_ReturnsProfile()
    {
        var result = await _sut.GetProfileAsync(UserId);

        Assert.NotNull(result);
        Assert.Equal(UserId, result!.Id);
        Assert.Equal("johndoe", result.Username);
        Assert.Equal("kasir", result.Role);
    }

    [Fact]
    public async Task GetProfile_NonExistentUser_ReturnsNull()
    {
        var result = await _sut.GetProfileAsync(Guid.NewGuid());
        Assert.Null(result);
    }

    // ── UpdateProfile: name change ─────────────────────────────────────────

    [Fact]
    public async Task UpdateProfile_NameOnly_UpdatesName()
    {
        var req = new UpdateProfileRequest("New Name", null, null);
        var (success, err) = await _sut.UpdateProfileAsync(UserId, req);

        Assert.True(success);
        Assert.Null(err);
        Assert.Equal("New Name", _db.Users.First(u => u.Id == UserId).Name);
    }

    [Fact]
    public async Task UpdateProfile_NonExistentUser_ReturnsError()
    {
        var req = new UpdateProfileRequest("Name", null, null);
        var (success, err) = await _sut.UpdateProfileAsync(Guid.NewGuid(), req);

        Assert.False(success);
        Assert.NotNull(err);
    }

    // ── UpdateProfile: password change ────────────────────────────────────

    [Fact]
    public async Task UpdateProfile_CorrectCurrentPassword_ChangesPassword()
    {
        var req = new UpdateProfileRequest(null, RawPassword, "NewSecret99!");
        var (success, err) = await _sut.UpdateProfileAsync(UserId, req);

        Assert.True(success);
        Assert.Null(err);
        var stored = _db.Users.First(u => u.Id == UserId);
        Assert.True(BCrypt.Net.BCrypt.Verify("NewSecret99!", stored.PasswordHash));
    }

    [Fact]
    public async Task UpdateProfile_WrongCurrentPassword_ReturnsError()
    {
        var req = new UpdateProfileRequest(null, "WrongPass!", "NewSecret99!");
        var (success, err) = await _sut.UpdateProfileAsync(UserId, req);

        Assert.False(success);
        Assert.NotNull(err);
    }

    [Fact]
    public async Task UpdateProfile_NewPasswordTooShort_ReturnsError()
    {
        var req = new UpdateProfileRequest(null, RawPassword, "short");
        var (success, err) = await _sut.UpdateProfileAsync(UserId, req);

        Assert.False(success);
        Assert.NotNull(err);
    }

    [Fact]
    public async Task UpdateProfile_NewPasswordWithoutCurrentPassword_ReturnsError()
    {
        var req = new UpdateProfileRequest(null, null, "NewSecret99!");
        var (success, err) = await _sut.UpdateProfileAsync(UserId, req);

        Assert.False(success);
        Assert.NotNull(err);
    }
}
