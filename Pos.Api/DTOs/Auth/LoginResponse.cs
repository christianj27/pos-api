namespace Pos.Api.DTOs.Auth;

public record LoginResponse(string AccessToken, string Role, Guid UserId, string Name);
