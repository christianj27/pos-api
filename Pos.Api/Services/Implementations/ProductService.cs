using Microsoft.EntityFrameworkCore;
using Pos.Api.Data;
using Pos.Api.Data.Enums;
using Pos.Api.DTOs.Products;
using Pos.Api.Models;
using Pos.Api.Services.Interfaces;

namespace Pos.Api.Services.Implementations;

public class ProductService(AppDbContext db) : IProductService
{
    private static readonly HashSet<string> ValidUnits = new(StringComparer.OrdinalIgnoreCase)
    {
        "galon", "tabung 3kg", "tabung 12kg", "karton", "dus", "cup", "botol", "pcs"
    };

    public async Task<IEnumerable<ProductResponse>> GetAllAsync(bool activeOnly = false)
    {
        var q = db.Products.AsQueryable();
        if (activeOnly) q = q.Where(p => p.IsActive);
        return await q.OrderBy(p => p.IsActive ? 0 : 1).ThenBy(p => p.Name).Select(p => MapToResponse(p)).ToListAsync();
    }

    public async Task<ProductResponse?> GetByIdAsync(Guid id)
    {
        var p = await db.Products.FindAsync(id);
        return p is null ? null : MapToResponse(p);
    }

    public async Task<(ProductResponse? Product, string? Error)> CreateAsync(CreateProductRequest request)
    {
        if (!Enum.TryParse<ProductCategory>(request.Category, ignoreCase: true, out var category))
            return (null, "Kategori produk wajib dipilih.");

        if (!Enum.TryParse<ProductType>(request.Type, ignoreCase: true, out var type))
            return (null, "Jenis produk wajib dipilih.");

        if (!ValidUnits.Contains(request.Unit))
            return (null, "Satuan tidak valid.");

        ProductionType? productionType = null;
        if (category == ProductCategory.Refillable)
        {
            if (string.IsNullOrWhiteSpace(request.ProductionType) ||
                !Enum.TryParse<ProductionType>(request.ProductionType, ignoreCase: true, out var pt))
                return (null, "Tipe produksi wajib dipilih untuk produk refillable.");
            productionType = pt;
        }

        var product = new Product
        {
            Name = request.Name.Trim(),
            Category = category,
            ProductionType = productionType,
            Type = type,
            Unit = request.Unit.ToLower(),
            BasePrice = request.BasePrice
        };

        db.Products.Add(product);
        await db.SaveChangesAsync();
        return (MapToResponse(product), null);
    }

    public async Task<(ProductResponse? Product, string? Error)> UpdateAsync(Guid id, UpdateProductRequest request)
    {
        var product = await db.Products.FindAsync(id);
        if (product is null) return (null, "Product not found.");

        if (!Enum.TryParse<ProductCategory>(request.Category, ignoreCase: true, out var category))
            return (null, "Kategori produk wajib dipilih.");

        if (!Enum.TryParse<ProductType>(request.Type, ignoreCase: true, out var type))
            return (null, "Jenis produk wajib dipilih.");

        if (!ValidUnits.Contains(request.Unit))
            return (null, "Satuan tidak valid.");

        ProductionType? productionType = null;
        if (category == ProductCategory.Refillable)
        {
            if (string.IsNullOrWhiteSpace(request.ProductionType) ||
                !Enum.TryParse<ProductionType>(request.ProductionType, ignoreCase: true, out var pt))
                return (null, "Tipe produksi wajib dipilih untuk produk refillable.");
            productionType = pt;
        }

        product.Name = request.Name.Trim();
        product.Category = category;
        product.ProductionType = productionType;
        product.Type = type;
        product.Unit = request.Unit.ToLower();
        product.BasePrice = request.BasePrice;
        product.IsActive = request.IsActive;

        await db.SaveChangesAsync();
        return (MapToResponse(product), null);
    }

    public async Task<(bool Success, string? Error)> ToggleActiveAsync(Guid id)
    {
        var product = await db.Products.FindAsync(id);
        if (product is null) return (false, "Product not found.");
        product.IsActive = !product.IsActive;
        await db.SaveChangesAsync();
        return (true, null);
    }

    private static ProductResponse MapToResponse(Product p) =>
        new(p.Id, p.Name, p.Category.ToString().ToLower(),
            p.ProductionType?.ToString().ToLower(), p.Type.ToString().ToLower(),
            p.Unit, p.BasePrice, p.IsActive, p.CreatedAt);
}
