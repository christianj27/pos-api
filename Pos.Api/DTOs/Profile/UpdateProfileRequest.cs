namespace Pos.Api.DTOs.Profile;

public record UpdateProfileRequest(
    string? Name,
    string? CurrentPassword,
    string? NewPassword
);
