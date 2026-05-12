using Pos.Api.DTOs.Auth;

namespace Pos.Api.Services.Interfaces;

public interface IAuthService
{
    Task<(LoginResponse Response, string RawRefreshToken)?> LoginAsync(string username, string password);
    Task<(RefreshResponse Response, string RawRefreshToken)?> RefreshAsync(string rawToken);
    Task LogoutAsync(string rawToken);
}
