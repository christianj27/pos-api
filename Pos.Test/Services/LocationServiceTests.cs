using Pos.Api.Data;
using Pos.Api.Data.Enums;
using Pos.Api.DTOs.Locations;
using Pos.Api.Models;
using Pos.Api.Services.Implementations;
using Pos.Test.Helpers;

namespace Pos.Test.Services;

public class LocationServiceTests
{
    private readonly AppDbContext _db;
    private readonly LocationService _sut;

    private static readonly Guid ActiveUserId = Guid.NewGuid();
    private static readonly Guid InactiveUserId = Guid.NewGuid();

    public LocationServiceTests()
    {
        _db = DbContextFactory.Create(Guid.NewGuid().ToString());
        _sut = new LocationService(_db);
        Seed();
    }

    private void Seed()
    {
        _db.Users.AddRange(
            new User
            {
                Id = ActiveUserId, Name = "Kurir 1", Username = "kurir1",
                PasswordHash = "x", Role = UserRole.Kurir, IsActive = true
            },
            new User
            {
                Id = InactiveUserId, Name = "Inactive User", Username = "inactive",
                PasswordHash = "x", Role = UserRole.Kurir, IsActive = false
            }
        );
        _db.SaveChanges();
    }

    // ── Create ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task Create_Warehouse_NoAssignment_Succeeds()
    {
        var req = new CreateLocationRequest("Gudang Utama", "warehouse", null);
        var (result, err) = await _sut.CreateAsync(req);

        Assert.Null(err);
        Assert.NotNull(result);
        Assert.Equal("Gudang Utama", result!.Name);
        Assert.Equal("warehouse", result.Type);
    }

    [Fact]
    public async Task Create_Vehicle_WithActiveUser_Succeeds()
    {
        var req = new CreateLocationRequest("Motor 1", "vehicle", ActiveUserId);
        var (result, err) = await _sut.CreateAsync(req);

        Assert.Null(err);
        Assert.NotNull(result);
        Assert.Equal(ActiveUserId, result!.AssignedTo);
    }

    [Fact]
    public async Task Create_Vehicle_WithoutAssignedTo_ReturnsError()
    {
        var req = new CreateLocationRequest("Motor 2", "vehicle", null);
        var (result, err) = await _sut.CreateAsync(req);

        Assert.Null(result);
        Assert.NotNull(err);
    }

    [Fact]
    public async Task Create_Vehicle_WithInactiveUser_ReturnsError()
    {
        var req = new CreateLocationRequest("Motor 3", "vehicle", InactiveUserId);
        var (result, err) = await _sut.CreateAsync(req);

        Assert.Null(result);
        Assert.NotNull(err);
    }

    [Fact]
    public async Task Create_Vehicle_WithNonExistentUser_ReturnsError()
    {
        var req = new CreateLocationRequest("Motor 4", "vehicle", Guid.NewGuid());
        var (result, err) = await _sut.CreateAsync(req);

        Assert.Null(result);
        Assert.NotNull(err);
    }

    [Fact]
    public async Task Create_InvalidLocationType_ReturnsError()
    {
        var req = new CreateLocationRequest("Bad", "rooftop", null);
        var (result, err) = await _sut.CreateAsync(req);

        Assert.Null(result);
        Assert.NotNull(err);
    }

    // ── GetAll / GetById ───────────────────────────────────────────────────

    [Fact]
    public async Task GetAll_ReturnsAllLocations()
    {
        await _sut.CreateAsync(new CreateLocationRequest("L1", "warehouse", null));
        await _sut.CreateAsync(new CreateLocationRequest("L2", "vehicle", ActiveUserId));

        var all = (await _sut.GetAllAsync()).ToList();

        Assert.Contains(all, l => l.Name == "L1");
        Assert.Contains(all, l => l.Name == "L2");
    }

    [Fact]
    public async Task GetById_ExistingLocation_ReturnsIt()
    {
        var (created, _) = await _sut.CreateAsync(new CreateLocationRequest("FindMe", "warehouse", null));

        var result = await _sut.GetByIdAsync(created!.Id);

        Assert.NotNull(result);
        Assert.Equal("FindMe", result!.Name);
    }

    [Fact]
    public async Task GetById_NonExistent_ReturnsNull()
    {
        var result = await _sut.GetByIdAsync(Guid.NewGuid());
        Assert.Null(result);
    }

    // ── Update ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task Update_ExistingLocation_UpdatesNameAndActiveFlag()
    {
        var (created, _) = await _sut.CreateAsync(new CreateLocationRequest("Old", "warehouse", null));

        var (updated, err) = await _sut.UpdateAsync(created!.Id,
            new UpdateLocationRequest("New Name", null, false));

        Assert.Null(err);
        Assert.Equal("New Name", updated!.Name);
        Assert.False(updated.IsActive);
    }

    [Fact]
    public async Task Update_NonExistentLocation_ReturnsError()
    {
        var (result, err) = await _sut.UpdateAsync(Guid.NewGuid(),
            new UpdateLocationRequest("X", null, true));

        Assert.Null(result);
        Assert.NotNull(err);
    }

    [Fact]
    public async Task Update_VehicleRemoveAssignment_ReturnsError()
    {
        var (created, _) = await _sut.CreateAsync(new CreateLocationRequest("Veh", "vehicle", ActiveUserId));

        // Trying to update vehicle with null assignedTo should fail
        var (result, err) = await _sut.UpdateAsync(created!.Id,
            new UpdateLocationRequest("Veh", null, true));

        Assert.Null(result);
        Assert.NotNull(err);
    }
}
