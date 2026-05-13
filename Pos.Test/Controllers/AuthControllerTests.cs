using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Pos.Api.Controllers;
using Pos.Api.DTOs.Auth;
using Pos.Api.Services.Interfaces;

namespace Pos.Test.Controllers;

public class AuthControllerTests
{
    private readonly Mock<IAuthService> _authServiceMock;
    private readonly AuthController _sut;

    private static readonly Guid UserId = Guid.NewGuid();

    private static readonly LoginResponse SampleLoginResponse =
        new("access-token", "owner", UserId, "Test Owner");

    private static readonly RefreshResponse SampleRefreshResponse =
        new("new-access-token");

    public AuthControllerTests()
    {
        _authServiceMock = new Mock<IAuthService>();
        _sut = new AuthController(_authServiceMock.Object);

        // Provide a default HttpContext so cookie operations don't throw
        _sut.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };
    }

    // ── Login: input validation ────────────────────────────────────────────

    [Fact]
    public async Task Login_EmptyUsername_ReturnsBadRequest()
    {
        var result = await _sut.Login(new LoginRequest("", "password"));

        var bad = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal(400, bad.StatusCode);
    }

    [Fact]
    public async Task Login_WhitespaceUsername_ReturnsBadRequest()
    {
        var result = await _sut.Login(new LoginRequest("   ", "password"));

        var bad = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal(400, bad.StatusCode);
    }

    [Fact]
    public async Task Login_EmptyPassword_ReturnsBadRequest()
    {
        var result = await _sut.Login(new LoginRequest("owner", ""));

        var bad = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal(400, bad.StatusCode);
    }

    [Fact]
    public async Task Login_ServiceReturnsNull_ReturnsUnauthorized()
    {
        _authServiceMock
            .Setup(s => s.LoginAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync((( LoginResponse Response, string RawRefreshToken)?) null);

        var result = await _sut.Login(new LoginRequest("owner", "wrongpass"));

        Assert.IsType<UnauthorizedObjectResult>(result);
    }

    [Fact]
    public async Task Login_ValidCredentials_ReturnsOkWithLoginResponse()
    {
        _authServiceMock
            .Setup(s => s.LoginAsync("owner", "pass123"))
            .ReturnsAsync((SampleLoginResponse, "raw-refresh-token"));

        var result = await _sut.Login(new LoginRequest("owner", "pass123"));

        var ok = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<LoginResponse>(ok.Value);
        Assert.Equal("access-token", response.AccessToken);
        Assert.Equal("owner", response.Role);
    }

    [Fact]
    public async Task Login_Success_SetsHttpOnlyRefreshCookie()
    {
        _authServiceMock
            .Setup(s => s.LoginAsync("owner", "pass123"))
            .ReturnsAsync((SampleLoginResponse, "raw-refresh-token"));

        await _sut.Login(new LoginRequest("owner", "pass123"));

        // Cookie should be appended to the response
        var setCookieHeaders = _sut.Response.Headers["Set-Cookie"].ToString();
        Assert.Contains("refreshToken", setCookieHeaders);
    }

    // ── Refresh ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Refresh_NoCookiePresent_ReturnsUnauthorized()
    {
        // No cookie set on the request context
        var result = await _sut.Refresh();

        Assert.IsType<UnauthorizedObjectResult>(result);
    }

    [Fact]
    public async Task Refresh_ServiceReturnsNull_ReturnsUnauthorized()
    {
        SetRefreshCookie("some-token");

        _authServiceMock
            .Setup(s => s.RefreshAsync("some-token"))
            .ReturnsAsync((( RefreshResponse Response, string RawRefreshToken)?) null);

        var result = await _sut.Refresh();

        Assert.IsType<UnauthorizedObjectResult>(result);
    }

    [Fact]
    public async Task Refresh_ValidToken_ReturnsOkWithNewAccessToken()
    {
        SetRefreshCookie("valid-token");

        _authServiceMock
            .Setup(s => s.RefreshAsync("valid-token"))
            .ReturnsAsync((SampleRefreshResponse, "new-raw-token"));

        var result = await _sut.Refresh();

        var ok = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<RefreshResponse>(ok.Value);
        Assert.Equal("new-access-token", response.AccessToken);
    }

    [Fact]
    public async Task Refresh_ValidToken_RotatesCookie()
    {
        SetRefreshCookie("valid-token");

        _authServiceMock
            .Setup(s => s.RefreshAsync("valid-token"))
            .ReturnsAsync((SampleRefreshResponse, "new-raw-token"));

        await _sut.Refresh();

        var setCookieHeaders = _sut.Response.Headers["Set-Cookie"].ToString();
        Assert.Contains("refreshToken", setCookieHeaders);
    }

    // ── Logout ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task Logout_WithCookie_CallsServiceAndReturnsNoContent()
    {
        SetRefreshCookie("logout-token");

        _authServiceMock
            .Setup(s => s.LogoutAsync("logout-token"))
            .Returns(Task.CompletedTask);

        var result = await _sut.Logout();

        _authServiceMock.Verify(s => s.LogoutAsync("logout-token"), Times.Once);
        Assert.IsType<NoContentResult>(result);
    }

    [Fact]
    public async Task Logout_NoCookie_SkipsServiceCallAndReturnsNoContent()
    {
        var result = await _sut.Logout();

        _authServiceMock.Verify(s => s.LogoutAsync(It.IsAny<string>()), Times.Never);
        Assert.IsType<NoContentResult>(result);
    }

    // ── Helpers ────────────────────────────────────────────────────────────

    private void SetRefreshCookie(string value)
    {
        _sut.ControllerContext.HttpContext.Request.Headers["Cookie"] = $"refreshToken={value}";
    }
}
