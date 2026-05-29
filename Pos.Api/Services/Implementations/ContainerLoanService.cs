using Microsoft.EntityFrameworkCore;
using Pos.Api.Data;
using Pos.Api.Data.Enums;
using Pos.Api.DTOs.ContainerLoans;
using Pos.Api.Models;
using Pos.Api.Services.Interfaces;

namespace Pos.Api.Services.Implementations;

public class ContainerLoanService(AppDbContext db) : IContainerLoanService
{
    public async Task<IEnumerable<ContainerLoanResponse>> GetAllAsync(Guid? customerId)
    {
        var q = db.ContainerLoans
            .Include(cl => cl.Customer)
            .Include(cl => cl.Product)
            .Include(cl => cl.Creator)
            .AsQueryable();

        if (customerId.HasValue)
            q = q.Where(cl => cl.CustomerId == customerId.Value);

        return await q
            .OrderByDescending(cl => cl.CreatedAt)
            .Select(cl => new ContainerLoanResponse(
                cl.Id, cl.TransactionId, cl.CustomerId, cl.Customer.Name,
                cl.ProductId, cl.Product.Name, cl.Product.Unit,
                cl.Quantity, cl.Note, cl.Creator.Name, cl.CreatedAt))
            .ToListAsync();
    }

    [Obsolete("Use CreateBulkAsync instead for better performance and stock movement recording")]
    public async Task<(ContainerLoanResponse? Loan, string? Error)> CreateAsync(
        CreateContainerLoanRequest request, Guid createdBy)
    {
        var product = await db.Products.FindAsync(request.ProductId);
        if (product is null || product.Category != ProductCategory.Refillable)
            return (null, "Hanya produk refillable yang bisa dipinjamkan.");

        var customer = await db.Customers.FindAsync(request.CustomerId);
        if (customer is null || !customer.IsActive)
            return (null, "Pelanggan tidak valid.");

        if (request.Quantity == 0)
            return (null, "Jumlah wajib diisi dan tidak boleh nol.");

        var loan = new ContainerLoan
        {
            CustomerId = request.CustomerId,
            ProductId = request.ProductId,
            Quantity = request.Quantity,
            Note = request.Notes,
            CreatedBy = createdBy
        };
        db.ContainerLoans.Add(loan);
        await db.SaveChangesAsync();

        var creator = await db.Users.FindAsync(createdBy);
        return (new ContainerLoanResponse(
            loan.Id, null, customer.Id, customer.Name,
            product.Id, product.Name, product.Unit,
            loan.Quantity, loan.Note, creator!.Name, loan.CreatedAt), null);
    }

    public async Task<(IEnumerable<ContainerLoanResponse>? Loans, string? Error)> CreateBulkAsync(
        BulkCreateContainerLoanRequest request, Guid createdBy)
    {
        var customer = await db.Customers.FindAsync(request.CustomerId);
        if (customer is null || !customer.IsActive)
            return (null, "Pelanggan tidak valid.");

        var location = await db.Locations.FindAsync(request.LocationId);
        if (location is null || !location.IsActive)
            return (null, "Lokasi tidak valid.");

        var productIds = request.Items.Select(i => i.ProductId).Distinct().ToList();
        var products = await db.Products
            .Where(p => productIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id);

        foreach (var item in request.Items)
        {
            if (!products.TryGetValue(item.ProductId, out var prod) || prod.Category != ProductCategory.Refillable)
                return (null, $"Produk {item.ProductId} tidak ditemukan atau bukan produk refillable.");
            if (item.Quantity == 0)
                return (null, "Jumlah tidak boleh nol.");
            if (!Enum.TryParse<ContainerStatus>(item.ContainerStatus, ignoreCase: true, out var cs) || cs == ContainerStatus.Na)
                return (null, $"Status kontainer tidak valid untuk produk {item.ProductId}. Pilih 'filled' atau 'empty'.");
        }

        var creator = await db.Users.FindAsync(createdBy);
        var loans = new List<ContainerLoan>();
        var batchId = Guid.NewGuid();

        foreach (var item in request.Items)
        {
            var prod = products[item.ProductId];
            var loan = new ContainerLoan
            {
                CustomerId = request.CustomerId,
                ProductId = item.ProductId,
                Quantity = item.Quantity,
                Note = item.Note ?? request.Note,
                CreatedBy = createdBy
            };
            db.ContainerLoans.Add(loan);
            loans.Add(loan);

            Enum.TryParse<ContainerStatus>(item.ContainerStatus, ignoreCase: true, out var containerStatus);
            var isLending = item.Quantity > 0; // positive = lending to customer = containers leave location
            db.StockMovements.Add(new StockMovement
            {
                ProductId = item.ProductId,
                MovementType = MovementType.Adjustment,
                ContainerStatus = containerStatus,
                Quantity = Math.Abs(item.Quantity),
                FromLocationId = isLending ? request.LocationId : null,
                ToLocationId = isLending ? null : request.LocationId,
                Note = item.Note ?? request.Note ?? "Kontainer manual",
                CreatedBy = createdBy,
                BatchId = batchId
            });
        }

        await db.SaveChangesAsync();

        var responses = loans.Select(l =>
        {
            var prod = products[l.ProductId];
            return new ContainerLoanResponse(
                l.Id, null, customer.Id, customer.Name,
                prod.Id, prod.Name, prod.Unit,
                l.Quantity, l.Note, creator!.Name, l.CreatedAt);
        });

        return (responses, null);
    }
}
