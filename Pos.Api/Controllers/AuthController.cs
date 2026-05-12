using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Pos.Api.DTOs.Auth;
using Pos.Api.Services.Implementations;
using Pos.Api.Services.Interfaces;

namespace Pos.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController(IAuthService authService) : ControllerBase
{
    private const string CookieName = "refreshToken";
    private const string CookiePath = "/api/auth";

    [HttpPost("login")]
    [EnableRateLimiting("LoginPolicy")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Username))
            return BadRequest(new { message = "Username wajib diisi." });
        if (string.IsNullOrWhiteSpace(request.Password))
            return BadRequest(new { message = "Kata sandi wajib diisi." });

        var result = await authService.LoginAsync(request.Username, request.Password);
        if (result is null)
            return Unauthorized(new { message = "Username atau kata sandi salah." });

        SetRefreshCookie(result.Value.RawRefreshToken);
        return Ok(result.Value.Response);
    }

    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh()
    {
        var rawToken = Request.Cookies[CookieName];
        if (string.IsNullOrEmpty(rawToken))
            return Unauthorized(new { message = "Sesi Anda telah berakhir. Silakan masuk kembali." });

        var result = await authService.RefreshAsync(rawToken);
        if (result is null)
            return Unauthorized(new { message = "Sesi Anda telah berakhir. Silakan masuk kembali." });

        SetRefreshCookie(result.Value.RawRefreshToken);
        return Ok(result.Value.Response);
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        var rawToken = Request.Cookies[CookieName];
        if (!string.IsNullOrEmpty(rawToken))
            await authService.LogoutAsync(rawToken);

        Response.Cookies.Delete(CookieName, new CookieOptions { Path = CookiePath });
        return NoContent();
    }

    private void SetRefreshCookie(string rawToken) =>
        Response.Cookies.Append(CookieName, rawToken, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Strict,
            Expires = DateTimeOffset.UtcNow.AddDays(7),
            Path = CookiePath
        });
}
