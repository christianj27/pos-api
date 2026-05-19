using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pos.Api.DTOs.Users;
using Pos.Api.Services.Interfaces;

namespace Pos.Api.Controllers;

[ApiController]
[Route("api/users")]
[Authorize(Policy = "OwnerOrKasir")]
public class UsersController(IUserService userService) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll() =>
        Ok(await userService.GetAllAsync());

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateUserRequest request)
    {
        var (user, error) = await userService.CreateAsync(request);
        if (user is null) return BadRequest(new { message = error });
        return CreatedAtAction(nameof(GetAll), new { id = user.Id }, user);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateUserRequest request)
    {
        var currentUserId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var (user, error) = await userService.UpdateAsync(id, request, currentUserId);
        if (user is null) return BadRequest(new { message = error });
        return Ok(user);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Deactivate(Guid id)
    {
        var currentUserId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var (success, error) = await userService.DeactivateAsync(id, currentUserId);
        if (!success) return BadRequest(new { message = error });
        return NoContent();
    }
}
