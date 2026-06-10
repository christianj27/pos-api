using Pos.Api.Data;
using Pos.Api.Data.Enums;
using Pos.Api.DTOs.Stock;
using Pos.Api.Models;
using Pos.Api.Services.Implementations;
using Pos.Test.Helpers;

namespace Pos.Test.Services;

public class StockServiceTests
{
    private readonly AppDbContext _db;
    private readonly StockService _sut;

    private static readonly Guid CreatorId = Guid.NewGuid();
    private static readonly Guid LocationId = Guid.NewGuid();
    private static readonly Guid Location2Id = Guid.NewGuid();
    private static readonly Guid RefillableProductId = Guid.NewGuid();
    private static readonly Guid SimpleProductId = Guid.NewGuid();
    private static readonly Guid CustomerId = Guid.NewGuid();

    public StockServiceTests()
    {
        _db = DbContextFactory.Create(Guid.NewGuid().ToString());
        _sut = new StockService(_db);
        Seed();
    }

    private void Seed()
    {
        _db.Users.Add(new User
        {
            Id = CreatorId, Name = "Staff", Username = "staff",
            PasswordHash = "x", Role = UserRole.Kasir, IsActive = true
        });

        _db.Locations.AddRange(
            new Location { Id = LocationId, Name = "Gudang", Type = LocationType.Warehouse, IsActive = true },
            new Location { Id = Location2Id, Name = "Motor 1", Type = LocationType.Vehicle, IsActive = true }
        );

        _db.Products.AddRange(
            new Product
            {
                Id = RefillableProductId, Name = "Galon 19L", Category = ProductCategory.Refillable,
                ProductionType = ProductionType.SelfProduced, Type = ProductType.Air,
                Unit = "galon", BasePrice = 5000, IsActive = true
            },
            new Product
            {
                Id = SimpleProductId, Name = "Air Kemasan", Category = ProductCategory.Simple,
                Type = ProductType.Air, Unit = "karton", BasePrice = 25000, IsActive = true
            }
        );

        _db.Customers.Add(new Customer
        {
            Id = CustomerId, Name = "Test Customer", IsActive = true
        });

        // Seed movements: 10 filled in to Gudang, 3 filled out from Gudang, 5 empty in to Gudang
        _db.StockMovements.AddRange(
            new StockMovement
            {
                Id = Guid.NewGuid(), ProductId = RefillableProductId, MovementType = MovementType.Receive,
                ContainerStatus = ContainerStatus.Filled, Quantity = 10,
                ToLocationId = LocationId, CreatedBy = CreatorId, CreatedAt = DateTime.UtcNow
            },
            new StockMovement
            {
                Id = Guid.NewGuid(), ProductId = RefillableProductId, MovementType = MovementType.Dispatch,
                ContainerStatus = ContainerStatus.Filled, Quantity = 3,
                FromLocationId = LocationId, CreatedBy = CreatorId, CreatedAt = DateTime.UtcNow
            },
            new StockMovement
            {
                Id = Guid.NewGuid(), ProductId = RefillableProductId, MovementType = MovementType.Receive,
                ContainerStatus = ContainerStatus.Empty, Quantity = 5,
                ToLocationId = LocationId, CreatedBy = CreatorId, CreatedAt = DateTime.UtcNow
            },
            // Simple product: 20 in, 4 out
            new StockMovement
            {
                Id = Guid.NewGuid(), ProductId = SimpleProductId, MovementType = MovementType.Receive,
                ContainerStatus = ContainerStatus.Na, Quantity = 20,
                ToLocationId = LocationId, CreatedBy = CreatorId, CreatedAt = DateTime.UtcNow
            },
            new StockMovement
            {
                Id = Guid.NewGuid(), ProductId = SimpleProductId, MovementType = MovementType.Dispatch,
                ContainerStatus = ContainerStatus.Na, Quantity = 4,
                FromLocationId = LocationId, CreatedBy = CreatorId, CreatedAt = DateTime.UtcNow
            }
        );

        _db.SaveChanges();
    }

    // ── GetLevels ──────────────────────────────────────────────────────────

    [Fact]
    public async Task GetLevels_RefillableProduct_CalculatesFilledAndEmpty()
    {
        var levels = (await _sut.GetLevelsAsync(LocationId)).ToList();

        var galon = levels.First(l => l.ProductId == RefillableProductId && l.LocationId == LocationId);
        Assert.Equal(7, galon.QuantityFilled);   // 10 in - 3 out
        Assert.Equal(5, galon.QuantityEmpty);    // 5 in - 0 out
    }

    [Fact]
    public async Task GetLevels_SimpleProduct_CalculatesTotalQty()
    {
        var levels = (await _sut.GetLevelsAsync(LocationId)).ToList();

        var air = levels.First(l => l.ProductId == SimpleProductId && l.LocationId == LocationId);
        Assert.Equal(16, air.QuantityTotal);   // 20 in - 4 out
    }

    [Fact]
    public async Task GetLevels_FilterByLocation_OnlyReturnsThatLocation()
    {
        var levels = (await _sut.GetLevelsAsync(LocationId)).ToList();

        Assert.All(levels, l => Assert.Equal(LocationId, l.LocationId));
    }

    [Fact]
    public async Task GetLevels_NoFilter_ReturnsAllLocations()
    {
        var levels = (await _sut.GetLevelsAsync(null)).ToList();

        Assert.Contains(levels, l => l.LocationId == LocationId);
        Assert.Contains(levels, l => l.LocationId == Location2Id);
    }

    // ── GetMovements ───────────────────────────────────────────────────────

    [Fact]
    public async Task GetMovements_OwnerRole_ShowsPurchaseCostOnReceive()
    {
        // Add a vendor-exchange receive movement with a cost
        _db.StockMovements.Add(new StockMovement
        {
            Id = Guid.NewGuid(), ProductId = RefillableProductId,
            MovementType = MovementType.Receive, ContainerStatus = ContainerStatus.Filled,
            Quantity = 5, ToLocationId = LocationId, PurchaseCost = 15000m,
            CreatedBy = CreatorId, CreatedAt = DateTime.UtcNow
        });
        _db.SaveChanges();

        var movements = (await _sut.GetMovementsAsync(null, "owner")).ToList();
        var withCost = movements.First(m => m.PurchaseCost == 15000m);

        Assert.Equal(15000m, withCost.PurchaseCost);
    }

    [Fact]
    public async Task GetMovements_KurirRole_HidesPurchaseCostOnReceive()
    {
        _db.StockMovements.Add(new StockMovement
        {
            Id = Guid.NewGuid(), ProductId = RefillableProductId,
            MovementType = MovementType.Receive, ContainerStatus = ContainerStatus.Filled,
            Quantity = 5, ToLocationId = LocationId, PurchaseCost = 15000m,
            CreatedBy = CreatorId, CreatedAt = DateTime.UtcNow
        });
        _db.SaveChanges();

        var movements = (await _sut.GetMovementsAsync(null, "kurir")).ToList();
        var receiveMovement = movements.First(m => m.MovementType == "receive" && m.PurchaseCost != null
                                                   || m.MovementType == "receive");
        // All receive movements for kurir must have null cost
        Assert.All(
            movements.Where(m => m.MovementType == "receive"),
            m => Assert.Null(m.PurchaseCost));
    }

    [Fact]
    public async Task GetMovements_DateFilter_ReturnsOnlyMatchingDay()
    {
        var today = Pos.Api.Services.WibTimeZone.TodayWib();
        var movements = (await _sut.GetMovementsAsync(today, "owner")).ToList();

        // All seeded movements have CreatedAt = UtcNow, which falls within today WIB
        Assert.NotEmpty(movements);
    }

    // ── CreateMovement ─────────────────────────────────────────────────────

    [Fact]
    public async Task CreateMovement_ValidDefectWithNote_Succeeds()
    {
        var req = new CreateMovementRequest(
            SimpleProductId, "defect", "na", 2,
            LocationId, null, null, "Rusak saat pengiriman");

        var (success, error) = await _sut.CreateMovementAsync(req, CreatorId);

        Assert.True(success);
        Assert.Null(error);
        Assert.True(_db.StockMovements.Any(m => m.MovementType == MovementType.Defect && m.Note == "Rusak saat pengiriman"));
    }

    [Fact]
    public async Task CreateMovement_DefectWithoutNote_ReturnsError()
    {
        var req = new CreateMovementRequest(
            SimpleProductId, "defect", "na", 1,
            LocationId, null, null, null);

        var (success, error) = await _sut.CreateMovementAsync(req, CreatorId);

        Assert.False(success);
        Assert.NotNull(error);
    }

    [Fact]
    public async Task CreateMovement_InvalidMovementType_ReturnsError()
    {
        var req = new CreateMovementRequest(
            SimpleProductId, "unknown_type", "na", 1,
            null, null, null, null);

        var (success, error) = await _sut.CreateMovementAsync(req, CreatorId);

        Assert.False(success);
        Assert.NotNull(error);
    }

    [Fact]
    public async Task CreateMovement_InvalidContainerStatus_ReturnsError()
    {
        var req = new CreateMovementRequest(
            RefillableProductId, "receive", "badstatus", 1,
            null, LocationId, null, null);

        var (success, error) = await _sut.CreateMovementAsync(req, CreatorId);

        Assert.False(success);
        Assert.NotNull(error);
    }

    // ── Transfer ───────────────────────────────────────────────────────────

    [Fact]
    public async Task Transfer_Valid_CreatesTransferMovement()
    {
        var req = new TransferRequest(
            RefillableProductId, "filled", 3,
            LocationId, Location2Id, null);

        var (success, error) = await _sut.TransferAsync(req, CreatorId);

        Assert.True(success);
        Assert.Null(error);
        Assert.True(_db.StockMovements.Any(m =>
            m.MovementType == MovementType.Transfer &&
            m.FromLocationId == LocationId &&
            m.ToLocationId == Location2Id &&
            m.Quantity == 3));
    }

    [Fact]
    public async Task BulkTransfer_Valid_CreatesMultipleMovements()
    {
        var req = new BulkTransferRequest(
            LocationId, Location2Id, "bulk transfer",
            new[]
            {
                new BulkTransferItem(RefillableProductId, "filled", 2),
                new BulkTransferItem(SimpleProductId, "na", 5)
            });

        var (success, error) = await _sut.BulkTransferAsync(req, CreatorId);

        Assert.True(success);
        Assert.Null(error);
        Assert.Equal(2, _db.StockMovements.Count(m =>
            m.MovementType == MovementType.Transfer &&
            m.Note == "bulk transfer"));
    }

    // ── VendorExchange ─────────────────────────────────────────────────────

    [Fact]
    public async Task VendorExchange_Valid_CreatesTwoPairedMovements()
    {
        var before = _db.StockMovements.Count();
        var req = new VendorExchangeRequest(LocationId, RefillableProductId, 8, 8, 10000m, "exchange");

        var (success, error) = await _sut.VendorExchangeAsync(req, CreatorId);

        Assert.True(success);
        Assert.Null(error);
        // Two new movements: one empty-out transfer, one filled-in receive
        Assert.Equal(before + 2, _db.StockMovements.Count());
    }

    // ── Production ─────────────────────────────────────────────────────────

    [Fact]
    public async Task Production_SelfProducedRefillable_CreatesTwoMovements()
    {
        var before = _db.StockMovements.Count();
        var req = new ProductionRequest(RefillableProductId, LocationId, 10, 2000m, null);

        var (success, error) = await _sut.ProductionAsync(req, CreatorId);

        Assert.True(success);
        Assert.Null(error);
        Assert.Equal(before + 2, _db.StockMovements.Count());
    }

    [Fact]
    public async Task Production_SimpleProduct_ReturnsError()
    {
        var req = new ProductionRequest(SimpleProductId, LocationId, 5, null, null);

        var (success, error) = await _sut.ProductionAsync(req, CreatorId);

        Assert.False(success);
        Assert.NotNull(error);
    }

    [Fact]
    public async Task Production_UnknownProduct_ReturnsError()
    {
        var req = new ProductionRequest(Guid.NewGuid(), LocationId, 5, null, null);

        var (success, error) = await _sut.ProductionAsync(req, CreatorId);

        Assert.False(success);
        Assert.NotNull(error);
    }

    // ── ReverseMovement with ContainerLoan ──────────────────────────────────

    [Fact]
    public async Task ReverseMovement_WithContainerLoan_AlsoReversesLoan()
    {
        // Seed a ContainerLoan + paired StockMovement (mimicking CreateBulkAsync)
        var loanId = Guid.NewGuid();
        var batchId = Guid.NewGuid();
        var movementId = Guid.NewGuid();

        _db.ContainerLoans.Add(new ContainerLoan
        {
            Id = loanId,
            CustomerId = CustomerId,
            ProductId = RefillableProductId,
            Quantity = 5,
            Note = "Pinjam 5 galon",
            CreatedBy = CreatorId
        });

        _db.StockMovements.Add(new StockMovement
        {
            Id = movementId,
            ProductId = RefillableProductId,
            MovementType = MovementType.Adjustment,
            ContainerStatus = ContainerStatus.Filled,
            Quantity = 5,
            FromLocationId = LocationId,
            ToLocationId = null,
            Note = "Kontainer manual",
            CreatedBy = CreatorId,
            BatchId = batchId,
            ContainerLoanId = loanId
        });

        _db.SaveChanges();

        // Act
        var (movements, error) = await _sut.ReverseMovementAsync(movementId, CreatorId);

        // Assert
        Assert.Null(error);
        Assert.NotNull(movements);

        // ContainerLoan should be marked as reversed
        var loan = await _db.ContainerLoans.FindAsync(loanId);
        Assert.NotNull(loan);
        Assert.True(loan.IsReversed);

        // Original movement should be marked reversed
        var original = await _db.StockMovements.FindAsync(movementId);
        Assert.NotNull(original);
        Assert.True(original.IsReversed);

        // Compensating movement should exist with IsReversal = true
        var reversal = _db.StockMovements.FirstOrDefault(m => m.IsReversal);
        Assert.NotNull(reversal);
        Assert.Equal(5, reversal.Quantity);
        Assert.Equal(ContainerStatus.Filled, reversal.ContainerStatus);
        // Direction swapped: original From=LocationId, reversal To=LocationId
        Assert.Equal(LocationId, reversal.ToLocationId);
        Assert.Null(reversal.FromLocationId);
    }
}
