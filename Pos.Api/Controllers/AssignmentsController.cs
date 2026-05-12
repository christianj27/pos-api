using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pos.Api.DTOs.Assignments;
using Pos.Api.Services.Interfaces;

namespace Pos.Api.Controllers;

[ApiController]
[Route("api/assignments")]
[Authorize(Policy = "AllStaff")]
public class AssignmentsController(IAssignmentService assignmentService) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var userId = GetUserId();
        var role = GetRole();
        return Ok(await assignmentService.GetAllAsync(userId, role));
    }

    [HttpPost]
    [Authorize(Policy = "OwnerOrKasir")]
    public async Task<IActionResult> Create([FromBody] CreateAssignmentRequest request)
    {
        var userId = GetUserId();
        var (assignment, error) = await assignmentService.CreateAsync(request, userId);
        if (assignment is null) return BadRequest(new { message = error });
        return CreatedAtAction(nameof(GetAll), new { id = assignment.Id }, assignment);
    }

    [HttpPost("{id:guid}/fulfill")]
    [Authorize(Policy = "OwnerOrKurir")]
    public async Task<IActionResult> Fulfill(Guid id, [FromBody] FulfillAssignmentRequest request)
    {
        var userId = GetUserId();
        var (success, error) = await assignmentService.FulfillAsync(id, request, userId);
        if (!success) return BadRequest(new { message = error });
        return Ok(new { message = "Penugasan berhasil diproses." });
    }

    [HttpPut("{id:guid}/cancel")]
    [Authorize(Policy = "OwnerOrKasir")]
    public async Task<IActionResult> Cancel(Guid id)
    {
        var userId = GetUserId();
        var role = GetRole();
        var (success, error) = await assignmentService.CancelAsync(id, userId, role);
        if (!success) return BadRequest(new { message = error });
        return Ok(new { message = "Penugasan berhasil dibatalkan." });
    }

    private Guid GetUserId() =>
        Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    private string GetRole() =>
        User.FindFirstValue(ClaimTypes.Role) ?? "kasir";
}
