using Pos.Api.Data;
using Pos.Api.Data.Enums;
using Pos.Api.DTOs.Users;
using Pos.Api.Models;
using Pos.Api.Services.Implementations;
using Pos.Test.Helpers;

namespace Pos.Test.Services;

public class UserServiceTests
{
    private readonly AppDbContext _db;
    private readonly UserService _sut;

    public UserServiceTests()
    {
        _db = DbContextFactory.Create(Guid.NewGuid().ToString());
        _sut = new UserService(_db);
    }

    // ── Create ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task Create_ValidRequest_ReturnsUser()
    {
        var req = new CreateUserRequest("Owner One", "owner1", "Pass123!", "owner");
        var (result, err) = await _sut.CreateAsync(req);

        Assert.Null(err);
        Assert.NotNull(result);
        Assert.Equal("Owner One", result!.Name);
        Assert.Equal("owner1", result.Username);
        Assert.Equal("owner", result.Role);
        Assert.True(result.IsActive);
    }

    [Fact]
    public async Task Create_DuplicateUsername_ReturnsError()
    {
        await _sut.CreateAsync(new CreateUserRequest("User A", "dupuser", "Pass!", "kasir"));

        var (result, err) = await _sut.CreateAsync(
            new CreateUserRequest("User B", "dupuser", "Pass!", "kasir"));

        Assert.Null(result);
        Assert.NotNull(err);
    }

    [Fact]
    public async Task Create_InvalidRole_ReturnsError()
    {
        var req = new CreateUserRequest("Bad Role", "badrole", "Pass!", "superhero");
        var (result, err) = await _sut.CreateAsync(req);

        Assert.Null(result);
        Assert.NotNull(err);
    }

    [Fact]
    public async Task Create_PasswordIsHashed()
    {
        await _sut.CreateAsync(new CreateUserRequest("Hashed", "hashtest", "MySecret", "kurir"));

        var stored = _db.Users.First(u => u.Username == "hashtest");
        Assert.NotEqual("MySecret", stored.PasswordHash);
        Assert.True(BCrypt.Net.BCrypt.Verify("MySecret", stored.PasswordHash));
    }

    [Fact]
    public async Task Create_TrimsNameAndUsername()
    {
        var (result, _) = await _sut.CreateAsync(
            new CreateUserRequest("  Trimmed  ", "  trimuser  ", "Pass!", "kasir"));

        Assert.Equal("Trimmed", result!.Name);
        Assert.Equal("trimuser", result.Username);
    }

    // ── GetAll / GetById ───────────────────────────────────────────────────

    [Fact]
    public async Task GetAll_ReturnsAllUsers()
    {
        await _sut.CreateAsync(new CreateUserRequest("U1", "u1", "Pass!", "owner"));
        await _sut.CreateAsync(new CreateUserRequest("U2", "u2", "Pass!", "kurir"));

        var all = (await _sut.GetAllAsync()).ToList();

        Assert.Contains(all, u => u.Username == "u1");
        Assert.Contains(all, u => u.Username == "u2");
    }

    [Fact]
    public async Task GetById_ExistingUser_ReturnsIt()
    {
        var (created, _) = await _sut.CreateAsync(
            new CreateUserRequest("Find Me", "findme", "Pass!", "kasir"));

        var result = await _sut.GetByIdAsync(created!.Id);

        Assert.NotNull(result);
        Assert.Equal("findme", result!.Username);
    }

    [Fact]
    public async Task GetById_NonExistent_ReturnsNull()
    {
        var result = await _sut.GetByIdAsync(Guid.NewGuid());
        Assert.Null(result);
    }

    // ── Update ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task Update_ValidRequest_UpdatesFields()
    {
        var (created, _) = await _sut.CreateAsync(
            new CreateUserRequest("Old Name", "olduser", "Pass!", "kasir"));

        var (updated, err) = await _sut.UpdateAsync(
            created!.Id,
            new UpdateUserRequest("New Name", "newuser", null, "kasir", true),
            Guid.NewGuid());

        Assert.Null(err);
        Assert.Equal("New Name", updated!.Name);
        Assert.Equal("newuser", updated.Username);
    }

    [Fact]
    public async Task Update_DuplicateUsernameOnOtherUser_ReturnsError()
    {
        var (user1, _) = await _sut.CreateAsync(
            new CreateUserRequest("User 1", "uniqueA", "Pass!", "kasir"));
        var (user2, _) = await _sut.CreateAsync(
            new CreateUserRequest("User 2", "uniqueB", "Pass!", "kasir"));

        var (result, err) = await _sut.UpdateAsync(
            user2!.Id,
            new UpdateUserRequest("User 2", "uniqueA", null, "kasir", true),
            Guid.NewGuid());

        Assert.Null(result);
        Assert.NotNull(err);
    }

    [Fact]
    public async Task Update_WithNewPassword_HashesIt()
    {
        var (created, _) = await _sut.CreateAsync(
            new CreateUserRequest("PwUser", "pwuser", "OldPass!", "kurir"));

        await _sut.UpdateAsync(
            created!.Id,
            new UpdateUserRequest("PwUser", "pwuser", "NewPass!", "kurir", true),
            Guid.NewGuid());

        var stored = _db.Users.First(u => u.Id == created.Id);
        Assert.True(BCrypt.Net.BCrypt.Verify("NewPass!", stored.PasswordHash));
    }

    [Fact]
    public async Task Update_NonExistentUser_ReturnsError()
    {
        var (result, err) = await _sut.UpdateAsync(
            Guid.NewGuid(),
            new UpdateUserRequest("X", "x", null, "kasir", true),
            Guid.NewGuid());

        Assert.Null(result);
        Assert.NotNull(err);
    }

    [Fact]
    public async Task Update_InvalidRole_ReturnsError()
    {
        var (created, _) = await _sut.CreateAsync(
            new CreateUserRequest("Role Test", "roletest", "Pass!", "kasir"));

        var (result, err) = await _sut.UpdateAsync(
            created!.Id,
            new UpdateUserRequest("Role Test", "roletest", null, "wizard", true),
            Guid.NewGuid());

        Assert.Null(result);
        Assert.NotNull(err);
    }

    // ── Deactivate ─────────────────────────────────────────────────────────

    [Fact]
    public async Task Deactivate_OtherUser_SetsInactive()
    {
        var actorId = Guid.NewGuid();
        var (target, _) = await _sut.CreateAsync(
            new CreateUserRequest("Target", "target", "Pass!", "kasir"));

        var (success, err) = await _sut.DeactivateAsync(target!.Id, actorId);

        Assert.True(success);
        Assert.Null(err);
        Assert.False(_db.Users.First(u => u.Id == target.Id).IsActive);
    }

    [Fact]
    public async Task Deactivate_Self_ReturnsError()
    {
        var (user, _) = await _sut.CreateAsync(
            new CreateUserRequest("Self", "selfuser", "Pass!", "owner"));

        var (success, err) = await _sut.DeactivateAsync(user!.Id, user.Id);

        Assert.False(success);
        Assert.NotNull(err);
    }

    [Fact]
    public async Task Deactivate_NonExistentUser_ReturnsError()
    {
        var (success, err) = await _sut.DeactivateAsync(Guid.NewGuid(), Guid.NewGuid());

        Assert.False(success);
        Assert.NotNull(err);
    }
}
