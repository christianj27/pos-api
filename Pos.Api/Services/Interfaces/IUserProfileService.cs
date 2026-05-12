using Pos.Api.DTOs.Profile;

namespace Pos.Api.Services.Interfaces;

public interface IUserProfileService
{
    Task<ProfileResponse?> GetProfileAsync(Guid userId);
    Task<(bool Success, string? Error)> UpdateProfileAsync(Guid userId, UpdateProfileRequest request);
}
