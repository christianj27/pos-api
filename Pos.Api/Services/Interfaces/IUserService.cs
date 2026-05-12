using Pos.Api.DTOs.Users;

namespace Pos.Api.Services.Interfaces;

public interface IUserService
{
    Task<IEnumerable<UserResponse>> GetAllAsync();
    Task<UserResponse?> GetByIdAsync(Guid id);
    Task<(UserResponse? User, string? Error)> CreateAsync(CreateUserRequest request);
    Task<(UserResponse? User, string? Error)> UpdateAsync(Guid id, UpdateUserRequest request, Guid currentUserId);
    Task<(bool Success, string? Error)> DeactivateAsync(Guid id, Guid currentUserId);
}
