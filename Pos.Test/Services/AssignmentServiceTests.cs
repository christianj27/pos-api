using Pos.Api.Data;
using Pos.Api.Data.Enums;
using Pos.Api.DTOs.Assignments;
using Pos.Api.Models;
using Pos.Api.Services.Implementations;
using Pos.Test.Helpers;

namespace Pos.Test.Services;

public class AssignmentServiceTests
{
    private readonly AppDbContext _db;
    private readonly AssignmentService _sut;

    private static readonly Guid OwnerId = Guid.NewGuid();
    private static readonly Guid KurirId = Guid.NewGuid();
    private static readonly Guid Kurir2Id = Guid.NewGuid();
    private static readonly Guid KasirId = Guid.NewGuid();
    private static readonly Guid CustomerId = Guid.NewGuid();
    private static readonly Guid VehicleId = Guid.NewGuid();
    private static readonly Guid WarehouseId = Guid.NewGuid();
    private static readonly Guid ProductId = Guid.NewGuid();

    public AssignmentServiceTests()
    {
        _db = DbContextFactory.Create(Guid.NewGuid().ToString());
        var txService = new TransactionService(_db);
        _sut = new AssignmentService(_db, txService);
        Seed();
    }

    private void Seed()
    {
        _db.Users.AddRange(
            new User { Id = OwnerId, Name = "Owner", Username = "owner", PasswordHash = "x", Role = UserRole.Owner, IsActive = true },
            new User { Id = KurirId, Name = "Kurir 1", Username = "kurir1", PasswordHash = "x", Role = UserRole.Kurir, IsActive = true },
            new User { Id = Kurir2Id, Name = "Kurir 2", Username = "kurir2", PasswordHash = "x", Role = UserRole.Kurir, IsActive = true },
            new User { Id = KasirId, Name = "Kasir", Username = "kasir", PasswordHash = "x", Role = UserRole.Kasir, IsActive = true }
        );
        _db.Customers.Add(new Customer { Id = CustomerId, Name = "Budi", IsActive = true });
        _db.Locations.AddRange(
            new Location { Id = WarehouseId, Name = "Gudang", Type = LocationType.Warehouse, IsActive = true },
            new Location { Id = VehicleId, Name = "Motor 1", Type = LocationType.Vehicle, AssignedTo = KurirId, IsActive = true }
        );
        _db.Products.Add(new Product
        {
            Id = ProductId, Name = "Galon", Category = ProductCategory.Refillable,
            ProductionType = ProductionType.SelfProduced, Type = ProductType.Air,
            Unit = "galon", BasePrice = 5000, IsActive = true
        });
        _db.SaveChanges();
    }

    private CreateAssignmentRequest MakeRequest() =>
        new(KurirId, CustomerId, VehicleId,
            new[] { new AssignmentItemRequest(ProductId, 3, 5000m) },
            "Tolong cepat");

    // ── Create ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task Create_ValidRequest_CreatesAssignment()
    {
        var (result, err) = await _sut.CreateAsync(MakeRequest(), OwnerId);

        Assert.Null(err);
        Assert.NotNull(result);
        Assert.Equal("pending", result!.Status);
        Assert.Equal(KurirId, result.KurirId);
        Assert.Equal(CustomerId, result.CustomerId);
    }

    [Fact]
    public async Task Create_InvalidKurir_NonExistent_ReturnsError()
    {
        var req = new CreateAssignmentRequest(Guid.NewGuid(), CustomerId, VehicleId,
            new[] { new AssignmentItemRequest(ProductId, 1, 5000m) }, null);

        var (result, err) = await _sut.CreateAsync(req, OwnerId);

        Assert.Null(result);
        Assert.NotNull(err);
    }

    [Fact]
    public async Task Create_KasirAsKurir_ReturnsError()
    {
        // KasirId is a Kasir role, not Kurir
        var req = new CreateAssignmentRequest(KasirId, CustomerId, VehicleId,
            new[] { new AssignmentItemRequest(ProductId, 1, 5000m) }, null);

        var (result, err) = await _sut.CreateAsync(req, OwnerId);

        Assert.Null(result);
        Assert.NotNull(err);
    }

    [Fact]
    public async Task Create_InactiveCustomer_ReturnsError()
    {
        var inactiveCustId = Guid.NewGuid();
        _db.Customers.Add(new Customer { Id = inactiveCustId, Name = "Inactive", IsActive = false });
        _db.SaveChanges();

        var req = new CreateAssignmentRequest(KurirId, inactiveCustId, VehicleId,
            new[] { new AssignmentItemRequest(ProductId, 1, 5000m) }, null);

        var (result, err) = await _sut.CreateAsync(req, OwnerId);

        Assert.Null(result);
        Assert.NotNull(err);
    }

    [Fact]
    public async Task Create_UnknownCustomer_ReturnsError()
    {
        var req = new CreateAssignmentRequest(KurirId, Guid.NewGuid(), VehicleId,
            new[] { new AssignmentItemRequest(ProductId, 1, 5000m) }, null);

        var (result, err) = await _sut.CreateAsync(req, OwnerId);

        Assert.Null(result);
        Assert.NotNull(err);
    }

    // ── GetAll ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetAll_KurirRole_OnlySeesOwnAssignments()
    {
        // Kurir1 gets one assignment, Kurir2 gets another
        await _sut.CreateAsync(MakeRequest(), OwnerId);
        await _sut.CreateAsync(new CreateAssignmentRequest(Kurir2Id, CustomerId, VehicleId,
            new[] { new AssignmentItemRequest(ProductId, 1, 5000m) }, null), OwnerId);

        var kurir1Assignments = (await _sut.GetAllAsync(KurirId, "kurir")).ToList();

        Assert.All(kurir1Assignments, a => Assert.Equal(KurirId, a.KurirId));
    }

    [Fact]
    public async Task GetAll_OwnerRole_SeesAllAssignments()
    {
        await _sut.CreateAsync(MakeRequest(), OwnerId);
        await _sut.CreateAsync(new CreateAssignmentRequest(Kurir2Id, CustomerId, VehicleId,
            new[] { new AssignmentItemRequest(ProductId, 1, 5000m) }, null), OwnerId);

        var all = (await _sut.GetAllAsync(OwnerId, "owner")).ToList();

        Assert.True(all.Any(a => a.KurirId == KurirId));
        Assert.True(all.Any(a => a.KurirId == Kurir2Id));
    }

    // ── Fulfill ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Fulfill_ValidRequest_CreatesTransactionAndMarksFulfilled()
    {
        var (assignment, _) = await _sut.CreateAsync(MakeRequest(), OwnerId);
        var fulfillReq = new FulfillAssignmentRequest(15000m, "cash", null, null, null, null);

        var (success, err) = await _sut.FulfillAsync(assignment!.Id, fulfillReq, KurirId);

        Assert.True(success);
        Assert.Null(err);

        var stored = _db.DeliveryAssignments.First(a => a.Id == assignment.Id);
        Assert.Equal(AssignmentStatus.Fulfilled, stored.Status);
        Assert.NotNull(stored.TransactionId);
    }

    [Fact]
    public async Task Fulfill_AlreadyFulfilled_ReturnsError()
    {
        var (assignment, _) = await _sut.CreateAsync(MakeRequest(), OwnerId);
        var fulfillReq = new FulfillAssignmentRequest(15000m, "cash", null, null, null, null);
        await _sut.FulfillAsync(assignment!.Id, fulfillReq, KurirId);

        var (success, err) = await _sut.FulfillAsync(assignment.Id, fulfillReq, KurirId);

        Assert.False(success);
        Assert.NotNull(err);
    }

    [Fact]
    public async Task Fulfill_WrongKurir_ReturnsError()
    {
        var (assignment, _) = await _sut.CreateAsync(MakeRequest(), OwnerId);
        var fulfillReq = new FulfillAssignmentRequest(15000m, "cash", null, null, null, null);

        // Kurir2 tries to fulfill Kurir1's assignment
        var (success, err) = await _sut.FulfillAsync(assignment!.Id, fulfillReq, Kurir2Id);

        Assert.False(success);
        Assert.NotNull(err);
    }

    [Fact]
    public async Task Fulfill_Kurir2WithExplicitLocation_Succeeds()
    {
        // With the new CreateAssignmentRequest contract, LocationId is always explicit.
        // The service uses the stored location directly and never looks up a vehicle;
        // Kurir2 not having an assigned vehicle is therefore not a blocking condition.
        var (assignment, _) = await _sut.CreateAsync(
            new CreateAssignmentRequest(Kurir2Id, CustomerId, VehicleId,
                new[] { new AssignmentItemRequest(ProductId, 1, 5000m) }, null),
            OwnerId);

        var fulfillReq = new FulfillAssignmentRequest(5000m, "cash", null, null, null, null);
        var (success, err) = await _sut.FulfillAsync(assignment!.Id, fulfillReq, Kurir2Id);

        Assert.True(success);
        Assert.Null(err);
    }

    [Fact]
    public async Task Fulfill_NonExistentAssignment_ReturnsError()
    {
        var fulfillReq = new FulfillAssignmentRequest(5000m, "cash", null, null, null, null);
        var (success, err) = await _sut.FulfillAsync(Guid.NewGuid(), fulfillReq, KurirId);

        Assert.False(success);
        Assert.NotNull(err);
    }

    // ── Cancel ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task Cancel_PendingAssignment_MarksCancelled()
    {
        var (assignment, _) = await _sut.CreateAsync(MakeRequest(), OwnerId);

        var (success, err) = await _sut.CancelAsync(assignment!.Id, OwnerId, "owner");

        Assert.True(success);
        Assert.Null(err);
        Assert.Equal(AssignmentStatus.Cancelled,
            _db.DeliveryAssignments.First(a => a.Id == assignment.Id).Status);
    }

    [Fact]
    public async Task Cancel_AlreadyCancelled_ReturnsError()
    {
        var (assignment, _) = await _sut.CreateAsync(MakeRequest(), OwnerId);
        await _sut.CancelAsync(assignment!.Id, OwnerId, "owner");

        var (success, err) = await _sut.CancelAsync(assignment.Id, OwnerId, "owner");

        Assert.False(success);
        Assert.NotNull(err);
    }

    [Fact]
    public async Task Cancel_FulfilledAssignment_ReturnsError()
    {
        var (assignment, _) = await _sut.CreateAsync(MakeRequest(), OwnerId);
        var fulfillReq = new FulfillAssignmentRequest(15000m, "cash", null, null, null, null);
        await _sut.FulfillAsync(assignment!.Id, fulfillReq, KurirId);

        var (success, err) = await _sut.CancelAsync(assignment.Id, OwnerId, "owner");

        Assert.False(success);
        Assert.NotNull(err);
    }

    [Fact]
    public async Task Cancel_NonExistent_ReturnsError()
    {
        var (success, err) = await _sut.CancelAsync(Guid.NewGuid(), OwnerId, "owner");

        Assert.False(success);
        Assert.NotNull(err);
    }
}
