using Pos.Api.DTOs.Stock;

namespace Pos.Api.Services.Interfaces;

public interface IStockService
{
    Task<IEnumerable<StockLevelResponse>> GetLevelsAsync(Guid? locationId);
    Task<IEnumerable<StockMovementResponse>> GetMovementsAsync(DateOnly? date, string role);
    Task<(bool Success, string? Error)> CreateMovementAsync(CreateMovementRequest request, Guid createdBy);
    Task<(bool Success, string? Error)> BulkCreateMovementAsync(BulkCreateMovementRequest request, Guid createdBy);
    Task<(bool Success, string? Error)> TransferAsync(TransferRequest request, Guid createdBy);
    Task<(bool Success, string? Error)> BulkTransferAsync(BulkTransferRequest request, Guid createdBy);
    Task<(bool Success, string? Error)> VendorExchangeAsync(VendorExchangeRequest request, Guid createdBy);
    Task<(bool Success, string? Error)> BulkVendorExchangeAsync(BulkVendorExchangeRequest request, Guid createdBy);
    Task<(bool Success, string? Error)> ProductionAsync(ProductionRequest request, Guid createdBy);
}
