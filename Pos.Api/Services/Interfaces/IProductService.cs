using Pos.Api.DTOs.Products;

namespace Pos.Api.Services.Interfaces;

public interface IProductService
{
    Task<IEnumerable<ProductResponse>> GetAllAsync(bool activeOnly = false);
    Task<ProductResponse?> GetByIdAsync(Guid id);
    Task<(ProductResponse? Product, string? Error)> CreateAsync(CreateProductRequest request);
    Task<(ProductResponse? Product, string? Error)> UpdateAsync(Guid id, UpdateProductRequest request);
    Task<(bool Success, string? Error)> ToggleActiveAsync(Guid id);
}
