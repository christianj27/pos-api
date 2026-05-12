using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pos.Api.DTOs.Profile;
using Pos.Api.Services.Interfaces;

namespace Pos.Api.Controllers;

[ApiController]
[Route("api/profile")]
[Authorize(Policy = "AllStaff")]
public class ProfileController(IUserProfileService profileService) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetProfile()
    {
        var userId = GetUserId();
        var profile = await profileService.GetProfileAsync(userId);
        return profile is null ? NotFound() : Ok(profile);
    }

    [HttpPut]
    public async Task<IActionResult> UpdateProfile([FromBody] UpdateProfileRequest request)
    {
        var userId = GetUserId();
        var (success, error) = await profileService.UpdateProfileAsync(userId, request);
        if (!success) return BadRequest(new { message = error });
        return Ok(new { message = "Profil berhasil diperbarui." });
    }

    private Guid GetUserId() =>
        Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
}
