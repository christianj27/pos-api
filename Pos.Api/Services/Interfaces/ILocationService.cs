using Pos.Api.DTOs.Locations;

namespace Pos.Api.Services.Interfaces;

public interface ILocationService
{
    Task<IEnumerable<LocationResponse>> GetAllAsync();
    Task<LocationResponse?> GetByIdAsync(Guid id);
    Task<(LocationResponse? Location, string? Error)> CreateAsync(CreateLocationRequest request);
    Task<(LocationResponse? Location, string? Error)> UpdateAsync(Guid id, UpdateLocationRequest request);
}
