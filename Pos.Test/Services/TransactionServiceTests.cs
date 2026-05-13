using Pos.Api.Data;
using Pos.Api.Data.Enums;
using Pos.Api.DTOs.Transactions;
using Pos.Api.Models;
using Pos.Api.Services.Implementations;
using Pos.Test.Helpers;

namespace Pos.Test.Services;

public class TransactionServiceTests
{
    private readonly AppDbContext _db;
    private readonly TransactionService _sut;

    private static readonly Guid OwnerId = Guid.NewGuid();
    private static readonly Guid KurirId = Guid.NewGuid();
    private static readonly Guid KasirId = Guid.NewGuid();
    private static readonly Guid CustomerId = Guid.NewGuid();
    private static readonly Guid LocationId = Guid.NewGuid();
    private static readonly Guid VehicleId = Guid.NewGuid();
    private static readonly Guid RefillableProductId = Guid.NewGuid();
    private static readonly Guid SimpleProductId = Guid.NewGuid();

    public TransactionServiceTests()
    {
        _db = DbContextFactory.Create(Guid.NewGuid().ToString());
        _sut = new TransactionService(_db);
        Seed();
    }

    private void Seed()
    {
        _db.Users.AddRange(
            new User { Id = OwnerId, Name = "Owner", Username = "owner", PasswordHash = "x", Role = UserRole.Owner, IsActive = true },
            new User { Id = KurirId, Name = "Kurir", Username = "kurir", PasswordHash = "x", Role = UserRole.Kurir, IsActive = true },
            new User { Id = KasirId, Name = "Kasir", Username = "kasir", PasswordHash = "x", Role = UserRole.Kasir, IsActive = true }
        );
        _db.Customers.Add(new Customer { Id = CustomerId, Name = "Budi", IsActive = true });
        _db.Locations.AddRange(
            new Location { Id = LocationId, Name = "Gudang", Type = LocationType.Warehouse, IsActive = true },
            new Location { Id = VehicleId, Name = "Motor 1", Type = LocationType.Vehicle, AssignedTo = KurirId, IsActive = true }
        );
        _db.Products.AddRange(
            new Product
            {
                Id = RefillableProductId, Name = "Galon", Category = ProductCategory.Refillable,
                ProductionType = ProductionType.SelfProduced, Type = ProductType.Air,
                Unit = "galon", BasePrice = 5000, IsActive = true
            },
            new Product
            {
                Id = SimpleProductId, Name = "Kemasan", Category = ProductCategory.Simple,
                Type = ProductType.Air, Unit = "karton", BasePrice = 25000, IsActive = true
            }
        );
        _db.SaveChanges();
    }

    // ── Helpers ────────────────────────────────────────────────────────────

    private CreateTransactionRequest MakeCounterRequest(Guid locationId, decimal paid = 25000m) =>
        new("counter", CustomerId, locationId,
            new[] { new TransactionItemRequest(SimpleProductId, 1, 25000m) },
            paid, "cash", null, null, null);

    private CreateTransactionRequest MakeDeliveryRequest(Guid locationId, decimal paid = 5000m) =>
        new("delivery", CustomerId, locationId,
            new[] { new TransactionItemRequest(RefillableProductId, 1, 5000m) },
            paid, "cash", null, null, null);

    // ── Create: role enforcement ───────────────────────────────────────────

    [Fact]
    public async Task Create_KurirWithDelivery_Succeeds()
    {
        var (tx, err) = await _sut.CreateAsync(MakeDeliveryRequest(VehicleId), KurirId, "kurir");
        Assert.Null(err);
        Assert.NotNull(tx);
    }

    [Fact]
    public async Task Create_KurirWithCounter_ReturnsError()
    {
        var (tx, err) = await _sut.CreateAsync(MakeCounterRequest(VehicleId), KurirId, "kurir");
        Assert.Null(tx);
        Assert.NotNull(err);
    }

    [Fact]
    public async Task Create_KasirWithCounter_Succeeds()
    {
        var (tx, err) = await _sut.CreateAsync(MakeCounterRequest(LocationId), KasirId, "kasir");
        Assert.Null(err);
        Assert.NotNull(tx);
    }

    [Fact]
    public async Task Create_KasirWithDelivery_ReturnsError()
    {
        var (tx, err) = await _sut.CreateAsync(MakeDeliveryRequest(LocationId), KasirId, "kasir");
        Assert.Null(tx);
        Assert.NotNull(err);
    }

    [Fact]
    public async Task Create_OwnerCanCreateAnyType_Succeeds()
    {
        var (txDelivery, errD) = await _sut.CreateAsync(MakeDeliveryRequest(VehicleId), OwnerId, "owner");
        var (txCounter, errC) = await _sut.CreateAsync(MakeCounterRequest(LocationId), OwnerId, "owner");

        Assert.Null(errD);
        Assert.NotNull(txDelivery);
        Assert.Null(errC);
        Assert.NotNull(txCounter);
    }

    // ── Create: paid amount validation ────────────────────────────────────

    [Fact]
    public async Task Create_PaidAmountExceedsTotal_ReturnsError()
    {
        var req = MakeCounterRequest(LocationId, paid: 99999m);
        var (tx, err) = await _sut.CreateAsync(req, KasirId, "kasir");

        Assert.Null(tx);
        Assert.NotNull(err);
    }

    [Fact]
    public async Task Create_PaidAmountNegative_ReturnsError()
    {
        var req = MakeCounterRequest(LocationId, paid: -1m);
        var (tx, err) = await _sut.CreateAsync(req, KasirId, "kasir");

        Assert.Null(tx);
        Assert.NotNull(err);
    }

    // ── Create: items required ─────────────────────────────────────────────

    [Fact]
    public async Task Create_NoItems_ReturnsError()
    {
        var req = new CreateTransactionRequest("counter", null, LocationId,
            Array.Empty<TransactionItemRequest>(), 0m, "cash", null, null, null);

        var (tx, err) = await _sut.CreateAsync(req, KasirId, "kasir");

        Assert.Null(tx);
        Assert.NotNull(err);
    }

    // ── Create: side effects ───────────────────────────────────────────────

    [Fact]
    public async Task Create_WithPaidAmount_CreatesPaymentRecord()
    {
        var req = MakeCounterRequest(LocationId, paid: 25000m);
        var (tx, _) = await _sut.CreateAsync(req, KasirId, "kasir");

        Assert.True(_db.Payments.Any(p => p.TransactionId == tx!.Id && p.Amount == 25000m));
    }

    [Fact]
    public async Task Create_ZeroPaidAmount_DoesNotCreatePaymentRecord()
    {
        var req = MakeCounterRequest(LocationId, paid: 0m);
        var (tx, _) = await _sut.CreateAsync(req, KasirId, "kasir");

        Assert.False(_db.Payments.Any(p => p.TransactionId == tx!.Id));
    }

    [Fact]
    public async Task Create_DebtAmount_IsComputedFromTotalMinusPaid()
    {
        var req = MakeCounterRequest(LocationId, paid: 10000m);
        var (tx, _) = await _sut.CreateAsync(req, KasirId, "kasir");

        Assert.Equal(15000m, tx!.DebtAmount);
    }

    [Fact]
    public async Task Create_RefillableProductWithCustomer_CreatesContainerLoan()
    {
        var req = MakeDeliveryRequest(VehicleId, paid: 5000m);
        var (tx, _) = await _sut.CreateAsync(req, KurirId, "kurir");

        Assert.True(_db.ContainerLoans.Any(cl =>
            cl.TransactionId == tx!.Id &&
            cl.CustomerId == CustomerId &&
            cl.Quantity == 1));
    }

    [Fact]
    public async Task Create_CreatesDispatchStockMovement()
    {
        var req = MakeCounterRequest(LocationId, paid: 25000m);
        var (tx, _) = await _sut.CreateAsync(req, KasirId, "kasir");

        Assert.True(_db.StockMovements.Any(m =>
            m.TransactionId == tx!.Id &&
            m.MovementType == MovementType.Dispatch &&
            m.FromLocationId == LocationId));
    }

    [Fact]
    public async Task Create_WithContainerReturns_CreatesNegativeLoanAndReceiveMovement()
    {
        var req = new CreateTransactionRequest("delivery", CustomerId, VehicleId,
            new[] { new TransactionItemRequest(RefillableProductId, 2, 5000m) },
            10000m, "cash", null,
            new[] { new ContainerReturnRequest(RefillableProductId, 1) },
            null);

        var (tx, _) = await _sut.CreateAsync(req, KurirId, "kurir");

        // Negative loan (return)
        Assert.True(_db.ContainerLoans.Any(cl =>
            cl.TransactionId == tx!.Id && cl.Quantity == -1));

        // Empty stock receive movement
        Assert.True(_db.StockMovements.Any(m =>
            m.TransactionId == tx!.Id &&
            m.MovementType == MovementType.Receive &&
            m.ContainerStatus == ContainerStatus.Empty));
    }

    [Fact]
    public async Task Create_WithDebtPaymentAmount_CreatesDebtPayment()
    {
        var req = new CreateTransactionRequest("delivery", CustomerId, VehicleId,
            new[] { new TransactionItemRequest(RefillableProductId, 1, 5000m) },
            5000m, "cash", null, null, 3000m);

        var (tx, _) = await _sut.CreateAsync(req, KurirId, "kurir");

        Assert.True(_db.DebtPayments.Any(dp =>
            dp.CustomerId == CustomerId && dp.Amount == 3000m));
    }

    // ── Cancel ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task Cancel_CompletedTransaction_UpdatesStatusAndReversesStock()
    {
        var (created, _) = await _sut.CreateAsync(MakeCounterRequest(LocationId, paid: 25000m), KasirId, "kasir");
        var txId = created!.Id;

        var movementsBefore = _db.StockMovements.Count(m => m.TransactionId == txId);

        var (success, err) = await _sut.UpdateStatusAsync(
            txId, new UpdateTransactionStatusRequest("cancelled"), KasirId, "kasir");

        Assert.True(success);
        Assert.Null(err);
        Assert.Equal(TransactionStatus.Cancelled, _db.Transactions.First(t => t.Id == txId).Status);
        // A reversal receive movement should have been added
        Assert.True(_db.StockMovements.Count(m => m.TransactionId == txId) > movementsBefore);
    }

    [Fact]
    public async Task Cancel_AlreadyCancelled_ReturnsError()
    {
        var (created, _) = await _sut.CreateAsync(MakeCounterRequest(LocationId, paid: 25000m), KasirId, "kasir");
        await _sut.UpdateStatusAsync(
            created!.Id, new UpdateTransactionStatusRequest("cancelled"), KasirId, "kasir");

        var (success, err) = await _sut.UpdateStatusAsync(
            created.Id, new UpdateTransactionStatusRequest("cancelled"), KasirId, "kasir");

        Assert.False(success);
        Assert.NotNull(err);
    }

    [Fact]
    public async Task Cancel_NonExistentTransaction_ReturnsError()
    {
        var (success, err) = await _sut.UpdateStatusAsync(
            Guid.NewGuid(), new UpdateTransactionStatusRequest("cancelled"), KasirId, "kasir");

        Assert.False(success);
        Assert.NotNull(err);
    }

    [Fact]
    public async Task Cancel_KurirCancellingOthersTransaction_ReturnsError()
    {
        var (created, _) = await _sut.CreateAsync(MakeCounterRequest(LocationId, paid: 25000m), KasirId, "kasir");

        var (success, err) = await _sut.UpdateStatusAsync(
            created!.Id, new UpdateTransactionStatusRequest("cancelled"), KurirId, "kurir");

        Assert.False(success);
        Assert.NotNull(err);
    }

    // ── GetAll ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetAll_KurirRole_OnlySeesOwnTransactions()
    {
        // Kurir creates a delivery
        await _sut.CreateAsync(MakeDeliveryRequest(VehicleId), KurirId, "kurir");
        // Kasir creates a counter sale
        await _sut.CreateAsync(MakeCounterRequest(LocationId), KasirId, "kasir");

        var kurirTxns = (await _sut.GetAllAsync(KurirId, "kurir", null)).ToList();

        Assert.All(kurirTxns, t => Assert.Equal(KurirId, t.StaffId));
    }

    [Fact]
    public async Task GetAll_OwnerRole_SeesAllTransactions()
    {
        await _sut.CreateAsync(MakeDeliveryRequest(VehicleId), KurirId, "kurir");
        await _sut.CreateAsync(MakeCounterRequest(LocationId), KasirId, "kasir");

        var all = (await _sut.GetAllAsync(OwnerId, "owner", null)).ToList();

        Assert.True(all.Any(t => t.StaffId == KurirId));
        Assert.True(all.Any(t => t.StaffId == KasirId));
    }

    // ── GetById ────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetById_OwnerCanAccessAnyTransaction()
    {
        var (created, _) = await _sut.CreateAsync(MakeDeliveryRequest(VehicleId), KurirId, "kurir");

        var detail = await _sut.GetByIdAsync(created!.Id, OwnerId, "owner");

        Assert.NotNull(detail);
    }

    [Fact]
    public async Task GetById_KurirCannotAccessOtherStaffTransaction()
    {
        var (created, _) = await _sut.CreateAsync(MakeCounterRequest(LocationId), KasirId, "kasir");

        var detail = await _sut.GetByIdAsync(created!.Id, KurirId, "kurir");

        Assert.Null(detail);
    }

    [Fact]
    public async Task GetById_NonExistent_ReturnsNull()
    {
        var detail = await _sut.GetByIdAsync(Guid.NewGuid(), OwnerId, "owner");
        Assert.Null(detail);
    }

    // ── Invalid enum inputs ───────────────────────────────────────────────

    [Fact]
    public async Task Create_InvalidTransactionType_ReturnsError()
    {
        var req = new CreateTransactionRequest("invalid_type", null, LocationId,
            new[] { new TransactionItemRequest(SimpleProductId, 1, 25000m) },
            25000m, "cash", null, null, null);

        var (tx, err) = await _sut.CreateAsync(req, KasirId, "kasir");

        Assert.Null(tx);
        Assert.NotNull(err);
    }

    [Fact]
    public async Task Create_InvalidPaymentMethod_ReturnsError()
    {
        var req = new CreateTransactionRequest("counter", null, LocationId,
            new[] { new TransactionItemRequest(SimpleProductId, 1, 25000m) },
            25000m, "telepati", null, null, null);

        var (tx, err) = await _sut.CreateAsync(req, KasirId, "kasir");

        Assert.Null(tx);
        Assert.NotNull(err);
    }
}
