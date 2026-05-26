using Microsoft.EntityFrameworkCore;
using Pos.Api.Data;
using Pos.Api.Data.Enums;
using Pos.Api.DTOs.Stock;
using Pos.Api.Models;
using Pos.Api.Services.Interfaces;

namespace Pos.Api.Services.Implementations;

public class StockService(AppDbContext db) : IStockService
{
    public async Task<IEnumerable<StockLevelResponse>> GetLevelsAsync(Guid? locationId)
    {
        var locQuery = db.Locations.Where(l => l.IsActive);
        if (locationId.HasValue) locQuery = locQuery.Where(l => l.Id == locationId.Value);
        var locations = await locQuery.OrderByDescending(o => o.Type).ToListAsync();

        var products = await db.Products.Where(p => p.IsActive).ToListAsync();

        // Load all movements in one query grouped by product+location+container_status
        var inbound = await db.StockMovements
            .Where(m => m.ToLocationId != null)
            .GroupBy(m => new { m.ProductId, m.ToLocationId, m.ContainerStatus })
            .Select(g => new { g.Key.ProductId, LocationId = g.Key.ToLocationId!.Value, g.Key.ContainerStatus, Total = g.Sum(m => m.Quantity) })
            .ToListAsync();

        var outbound = await db.StockMovements
            .Where(m => m.FromLocationId != null)
            .GroupBy(m => new { m.ProductId, m.FromLocationId, m.ContainerStatus })
            .Select(g => new { g.Key.ProductId, LocationId = g.Key.FromLocationId!.Value, g.Key.ContainerStatus, Total = g.Sum(m => m.Quantity) })
            .ToListAsync();

        var results = new List<StockLevelResponse>();

        foreach (var loc in locations)
        {
            foreach (var product in products)
            {
                int In(ContainerStatus status) =>
                    inbound.Where(x => x.ProductId == product.Id && x.LocationId == loc.Id && x.ContainerStatus == status).Sum(x => x.Total);
                int Out(ContainerStatus status) =>
                    outbound.Where(x => x.ProductId == product.Id && x.LocationId == loc.Id && x.ContainerStatus == status).Sum(x => x.Total);

                if (product.Category == ProductCategory.Refillable)
                {
                    var filled = In(ContainerStatus.Filled) - Out(ContainerStatus.Filled);
                    var empty = In(ContainerStatus.Empty) - Out(ContainerStatus.Empty);
                    results.Add(new StockLevelResponse(
                        product.Id, product.Name, product.Unit, product.Category.ToString().ToLower(),
                        loc.Id, loc.Name, filled, empty, null));
                }
                else
                {
                    var total = In(ContainerStatus.Na) - Out(ContainerStatus.Na);
                    results.Add(new StockLevelResponse(
                        product.Id, product.Name, product.Unit, product.Category.ToString().ToLower(),
                        loc.Id, loc.Name, null, null, total));
                }
            }
        }

        return results;
    }

    public async Task<IEnumerable<StockMovementResponse>> GetMovementsAsync(DateOnly? date, string role)
    {
        var filter = date ?? WibTimeZone.TodayWib();
        var (start, end) = WibTimeZone.GetUtcDayBounds(filter);

        var q = db.StockMovements
            .Include(m => m.Product)
            .Include(m => m.FromLocation)
            .Include(m => m.ToLocation)
            .Include(m => m.Creator)
            .Include(m => m.Transaction!).ThenInclude(t => t.Customer)
            .Where(m => m.CreatedAt >= start && m.CreatedAt < end)
            .OrderByDescending(m => m.CreatedAt);

        var movements = await q.ToListAsync();

        return movements.Select(m =>
        {
            // Hide purchase_cost for vendor_exchange receive movements from non-owner roles
            var showCost = role == "owner" ||
                           m.MovementType != MovementType.Receive ||
                           m.PurchaseCost == null;

            var customerName = m.MovementType == MovementType.Dispatch
                ? m.Transaction?.Customer?.Name
                : null;

            return new StockMovementResponse(
                m.Id, m.ProductId, m.Product.Name,
                m.MovementType.ToString().ToLower(), m.ContainerStatus.ToString().ToLower(),
                m.Quantity, m.FromLocationId, m.FromLocation?.Name,
                m.ToLocationId, m.ToLocation?.Name,
                showCost ? m.PurchaseCost : null,
                m.Note, m.Creator.Name, m.CreatedAt, customerName,
                m.BatchId, m.IsReversed, m.IsReversal);
        });
    }

    public async Task<(bool Success, string? Error)> CreateMovementAsync(CreateMovementRequest request, Guid createdBy)
    {
        if (!Enum.TryParse<MovementType>(request.MovementType, ignoreCase: true, out var movType))
            return (false, "Tipe pergerakan wajib dipilih.");

        var product = await db.Products.FindAsync(request.ProductId);
        if (product is null) return (false, "Produk tidak ditemukan.");

        ContainerStatus status;
        if (product.Category == ProductCategory.Simple)
            status = ContainerStatus.Na;
        else if (!Enum.TryParse<ContainerStatus>(request.ContainerStatus, ignoreCase: true, out status))
            return (false, "Status kontainer wajib dipilih.");

        if (movType == MovementType.Defect && string.IsNullOrWhiteSpace(request.Note))
            return (false, "Catatan wajib diisi untuk barang cacat.");

        db.StockMovements.Add(new StockMovement
        {
            ProductId = request.ProductId,
            MovementType = movType,
            ContainerStatus = status,
            Quantity = request.Quantity,
            FromLocationId = request.FromLocationId,
            ToLocationId = request.ToLocationId,
            PurchaseCost = request.PurchaseCost,
            Note = request.Note,
            CreatedBy = createdBy,
            BatchId = Guid.NewGuid()
        });

        await db.SaveChangesAsync();
        return (true, null);
    }

    public async Task<(bool Success, string? Error)> BulkCreateMovementAsync(BulkCreateMovementRequest request, Guid createdBy)
    {
        if (!Enum.TryParse<MovementType>(request.MovementType, ignoreCase: true, out var movType))
            return (false, "Tipe pergerakan wajib dipilih.");

        var productIds = request.Items.Select(i => i.ProductId).Distinct().ToList();
        var simpleProductIds = (await db.Products
            .Where(p => productIds.Contains(p.Id) && p.Category == ProductCategory.Simple)
            .Select(p => p.Id)
            .ToListAsync())
            .ToHashSet();

        var batchId = Guid.NewGuid();
        foreach (var item in request.Items)
        {
            ContainerStatus status;
            if (simpleProductIds.Contains(item.ProductId))
                status = ContainerStatus.Na;
            else if (!Enum.TryParse<ContainerStatus>(item.ContainerStatus, ignoreCase: true, out status))
                return (false, "Status kontainer wajib dipilih.");

            db.StockMovements.Add(new StockMovement
            {
                ProductId = item.ProductId,
                MovementType = movType,
                ContainerStatus = status,
                Quantity = item.Quantity,
                ToLocationId = request.ToLocationId,
                PurchaseCost = item.PurchaseCost,
                Note = request.Note,
                CreatedBy = createdBy,
                BatchId = batchId
            });
        }

        await db.SaveChangesAsync();
        return (true, null);
    }

    public async Task<(bool Success, string? Error)> TransferAsync(TransferRequest request, Guid createdBy)
    {
        var product = await db.Products.FindAsync(request.ProductId);
        if (product is null) return (false, "Produk tidak ditemukan.");

        ContainerStatus status;
        if (product.Category == ProductCategory.Simple)
            status = ContainerStatus.Na;
        else if (!Enum.TryParse<ContainerStatus>(request.ContainerStatus, ignoreCase: true, out status))
            return (false, "Status kontainer wajib dipilih.");

        db.StockMovements.Add(new StockMovement
        {
            ProductId = request.ProductId,
            MovementType = MovementType.Transfer,
            ContainerStatus = status,
            Quantity = request.Quantity,
            FromLocationId = request.FromLocationId,
            ToLocationId = request.ToLocationId,
            Note = request.Note,
            CreatedBy = createdBy,
            BatchId = Guid.NewGuid()
        });

        await db.SaveChangesAsync();
        return (true, null);
    }

    public async Task<(bool Success, string? Error)> BulkTransferAsync(BulkTransferRequest request, Guid createdBy)
    {
        var productIds = request.Items.Select(i => i.ProductId).Distinct().ToList();
        var simpleProductIds = (await db.Products
            .Where(p => productIds.Contains(p.Id) && p.Category == ProductCategory.Simple)
            .Select(p => p.Id)
            .ToListAsync())
            .ToHashSet();

        var batchId = Guid.NewGuid();
        foreach (var item in request.Items)
        {
            ContainerStatus status;
            if (simpleProductIds.Contains(item.ProductId))
                status = ContainerStatus.Na;
            else if (!Enum.TryParse<ContainerStatus>(item.ContainerStatus, ignoreCase: true, out status))
                return (false, "Status kontainer wajib dipilih.");

            db.StockMovements.Add(new StockMovement
            {
                ProductId = item.ProductId,
                MovementType = MovementType.Transfer,
                ContainerStatus = status,
                Quantity = item.Quantity,
                FromLocationId = request.FromLocationId,
                ToLocationId = request.ToLocationId,
                Note = request.Note,
                CreatedBy = createdBy,
                BatchId = batchId
            });
        }

        await db.SaveChangesAsync();
        return (true, null);
    }

    public async Task<(bool Success, string? Error)> VendorExchangeAsync(VendorExchangeRequest request, Guid createdBy)
    {
        await using var tx = await db.Database.BeginTransactionAsync();
        try
        {
            var batchId = Guid.NewGuid();
            db.StockMovements.Add(new StockMovement
            {
                ProductId = request.ProductId,
                MovementType = MovementType.Transfer,
                ContainerStatus = ContainerStatus.Empty,
                Quantity = request.EmptyQuantity,
                FromLocationId = request.LocationId,
                ToLocationId = null,
                Note = request.Note,
                CreatedBy = createdBy,
                BatchId = batchId
            });

            db.StockMovements.Add(new StockMovement
            {
                ProductId = request.ProductId,
                MovementType = MovementType.Receive,
                ContainerStatus = ContainerStatus.Filled,
                Quantity = request.FilledQuantity,
                FromLocationId = null,
                ToLocationId = request.LocationId,
                PurchaseCost = request.PurchaseCost,
                Note = request.Note,
                CreatedBy = createdBy,
                BatchId = batchId
            });

            await db.SaveChangesAsync();
            await tx.CommitAsync();
            return (true, null);
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }
    }

    public async Task<(bool Success, string? Error)> BulkVendorExchangeAsync(BulkVendorExchangeRequest request, Guid createdBy)
    {
        await using var tx = await db.Database.BeginTransactionAsync();
        try
        {
            foreach (var item in request.Items)
            {
                var itemBatchId = Guid.NewGuid();
                db.StockMovements.Add(new StockMovement
                {
                    ProductId = item.ProductId,
                    MovementType = MovementType.Transfer,
                    ContainerStatus = ContainerStatus.Empty,
                    Quantity = item.EmptyQuantity,
                    FromLocationId = request.LocationId,
                    ToLocationId = null,
                    Note = request.Note,
                    CreatedBy = createdBy,
                    BatchId = itemBatchId
                });

                db.StockMovements.Add(new StockMovement
                {
                    ProductId = item.ProductId,
                    MovementType = MovementType.Receive,
                    ContainerStatus = ContainerStatus.Filled,
                    Quantity = item.FilledQuantity,
                    FromLocationId = null,
                    ToLocationId = request.LocationId,
                    PurchaseCost = item.PurchaseCost,
                    Note = request.Note,
                    CreatedBy = createdBy,
                    BatchId = itemBatchId
                });
            }

            await db.SaveChangesAsync();
            await tx.CommitAsync();
            return (true, null);
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }
    }

    public async Task<(bool Success, string? Error)> ProductionAsync(ProductionRequest request, Guid createdBy)
    {
        var product = await db.Products.FindAsync(request.ProductId);
        if (product is null) return (false, "Produk tidak ditemukan.");

        if (product.Category != ProductCategory.Refillable || product.ProductionType != ProductionType.SelfProduced)
            return (false, "Produksi hanya berlaku untuk produk isi ulang produksi sendiri.");

        var productionBatchId = Guid.NewGuid();

        // Empty containers are consumed (outbound only)
        db.StockMovements.Add(new StockMovement
        {
            ProductId = request.ProductId,
            MovementType = MovementType.Production,
            ContainerStatus = ContainerStatus.Empty,
            Quantity = request.Quantity,
            FromLocationId = request.LocationId,
            ToLocationId = null,
            PurchaseCost = request.ProductionCost,
            Note = request.Note,
            CreatedBy = createdBy,
            BatchId = productionBatchId
        });

        // Filled containers are produced (inbound only)
        db.StockMovements.Add(new StockMovement
        {
            ProductId = request.ProductId,
            MovementType = MovementType.Production,
            ContainerStatus = ContainerStatus.Filled,
            Quantity = request.Quantity,
            FromLocationId = null,
            ToLocationId = request.LocationId,
            Note = request.Note,
            CreatedBy = createdBy,
            BatchId = productionBatchId
        });

        await db.SaveChangesAsync();
        return (true, null);
    }

    public async Task<(IEnumerable<StockMovementResponse>? Movements, string? Error)> ReverseMovementAsync(
        Guid movementId, Guid requestedBy)
    {
        var movement = await db.StockMovements
            .Include(m => m.Product)
            .Include(m => m.Creator)
            .FirstOrDefaultAsync(m => m.Id == movementId);

        if (movement is null) return (null, "Pergerakan stok tidak ditemukan.");
        if (movement.MovementType == MovementType.Dispatch)
            return (null, "Pergerakan dispatch tidak dapat dibatalkan di sini. Batalkan transaksinya.");
        if (movement.IsReversed || movement.IsReversal)
            return (null, "Pergerakan ini sudah pernah dibatalkan.");

        // Gather all movements in the same batch
        var batch = movement.BatchId.HasValue
            ? await db.StockMovements
                .Include(m => m.Product)
                .Include(m => m.Creator)
                .Where(m => m.BatchId == movement.BatchId.Value && !m.IsReversed && !m.IsReversal)
                .ToListAsync()
            : [movement];

        if (batch.Any(m => m.IsReversed || m.IsReversal))
            return (null, "Satu atau lebih pergerakan dalam batch ini sudah dibatalkan.");

        await using var tx = await db.Database.BeginTransactionAsync();
        try
        {
            var reversalBatchId = Guid.NewGuid();
            var created = new List<StockMovement>();

            foreach (var m in batch)
            {
                // Universal direction-invert: swap From and To
                var reversal = new StockMovement
                {
                    ProductId = m.ProductId,
                    MovementType = m.MovementType,
                    ContainerStatus = m.ContainerStatus,
                    Quantity = m.Quantity,
                    FromLocationId = m.ToLocationId,   // swapped
                    ToLocationId = m.FromLocationId,   // swapped
                    Note = $"Koreksi/pembatalan pergerakan {m.Id}",
                    CreatedBy = requestedBy,
                    BatchId = reversalBatchId,
                    IsReversal = true
                };
                db.StockMovements.Add(reversal);
                m.IsReversed = true;
                created.Add(reversal);
            }

            await db.SaveChangesAsync();
            await tx.CommitAsync();

            // Build responses for created reversal movements
            var creator = await db.Users.FindAsync(requestedBy);
            var responses = created.Select(r =>
            {
                var orig = batch.First(m => m.ProductId == r.ProductId && m.ContainerStatus == r.ContainerStatus);
                return new StockMovementResponse(
                    r.Id, r.ProductId, orig.Product.Name,
                    r.MovementType.ToString().ToLower(), r.ContainerStatus.ToString().ToLower(),
                    r.Quantity, r.FromLocationId, null, r.ToLocationId, null,
                    null, r.Note, creator?.Name ?? string.Empty, r.CreatedAt,
                    null, r.BatchId, r.IsReversed, r.IsReversal);
            });

            return (responses, null);
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }
    }

    public async Task<(bool Success, string? Error)> AdjustmentAsync(AdjustmentRequest request, Guid createdBy)
    {
        if (request.AdjustmentQuantity == 0)
            return (false, "Jumlah penyesuaian tidak boleh nol.");

        var product = await db.Products.FindAsync(request.ProductId);
        if (product is null) return (false, "Produk tidak ditemukan.");

        ContainerStatus status;
        if (product.Category == ProductCategory.Simple)
            status = ContainerStatus.Na;
        else if (!Enum.TryParse<ContainerStatus>(request.ContainerStatus, ignoreCase: true, out status))
            return (false, "Status kontainer wajib dipilih untuk produk refillable.");

        if (string.IsNullOrWhiteSpace(request.Note))
            return (false, "Catatan wajib diisi untuk penyesuaian stok.");

        var isAddition = request.AdjustmentQuantity > 0;
        db.StockMovements.Add(new StockMovement
        {
            ProductId = request.ProductId,
            MovementType = MovementType.Adjustment,
            ContainerStatus = status,
            Quantity = Math.Abs(request.AdjustmentQuantity),
            FromLocationId = isAddition ? null : request.LocationId,
            ToLocationId = isAddition ? request.LocationId : null,
            Note = request.Note,
            CreatedBy = createdBy,
            BatchId = Guid.NewGuid()
        });

        await db.SaveChangesAsync();
        return (true, null);
    }
}

