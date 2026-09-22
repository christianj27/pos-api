using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pos.Api.DTOs.Settlements;
using Pos.Api.Services.Interfaces;

namespace Pos.Api.Controllers;

/// <summary>
/// Daily kas settlement ("Tutup Kas") — FR-STL.
/// All staff may preview, submit and read their own settlements; only the owner may verify,
/// reopen, or book a Selisih Kas adjustment.
/// </summary>
[ApiController]
[Route("api/settlements")]
[Authorize(Policy = "AllStaff")]
public class SettlementsController(ISettlementService settlementService) : ControllerBase
{
    [HttpGet("preview")]
    public async Task<IActionResult> GetPreview([FromQuery] DateOnly? date)
    {
        var result = await settlementService.GetPreviewAsync(GetUserId(), date);
        return result is null
            ? NotFound(new { message = "Tanggal settlement tidak valid." })
            : Ok(result);
    }

    /// <summary>Blocking status for the caller — drives the frontend "settle first" banner.</summary>
    [HttpGet("status")]
    public async Task<IActionResult> GetStatus() =>
        Ok(await settlementService.GetStatusAsync(GetUserId()));

    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to,
        [FromQuery] Guid? user_id,
        [FromQuery] string? status) =>
        Ok(await settlementService.ListAsync(from, to, user_id, status, GetUserId(), GetRole()));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var result = await settlementService.GetByIdAsync(id, GetUserId(), GetRole());
        return result is null ? NotFound() : Ok(result);
    }

    [HttpPost("submit")]
    public async Task<IActionResult> Submit([FromBody] SubmitSettlementRequest request)
    {
        var (settlement, error, code) = await settlementService.SubmitAsync(request, GetUserId());

        if (settlement is null)
            return code is null
                ? BadRequest(new { message = error })
                : Conflict(new { message = error, code });

        return Ok(settlement);
    }

    [HttpPost("{id:guid}/approve")]
    [Authorize(Policy = "OwnerOnly")]
    public async Task<IActionResult> Approve(Guid id)
    {
        var (settlement, notFound, error) = await settlementService.ApproveAsync(id, null, GetUserId());
        if (notFound) return NotFound();
        if (settlement is null) return BadRequest(new { message = error });
        return Ok(settlement);
    }

    [HttpPost("{id:guid}/reject")]
    [Authorize(Policy = "OwnerOnly")]
    public async Task<IActionResult> Reject(Guid id, [FromBody] RejectSettlementRequest request)
    {
        var (settlement, notFound, error) = await settlementService.RejectAsync(id, request, GetUserId());
        if (notFound) return NotFound();
        if (settlement is null) return BadRequest(new { message = error });
        return Ok(settlement);
    }

    [HttpPost("{id:guid}/reopen")]
    [Authorize(Policy = "OwnerOnly")]
    public async Task<IActionResult> Reopen(Guid id, [FromBody] ReopenSettlementRequest request)
    {
        var (settlement, notFound, error) = await settlementService.ReopenAsync(id, request, GetUserId());
        if (notFound) return NotFound();
        if (settlement is null) return BadRequest(new { message = error });
        return Ok(settlement);
    }

    /// <summary>Owner-only "Selisih Kas" correction.</summary>
    [HttpPost("adjustments")]
    [Authorize(Policy = "OwnerOnly")]
    public async Task<IActionResult> CreateAdjustment([FromBody] CashAdjustmentRequest request)
    {
        var (success, error) = await settlementService.CreateAdjustmentAsync(request, GetUserId());
        if (!success) return BadRequest(new { message = error });
        return Ok(new { message = "Selisih kas berhasil dicatat." });
    }

    private Guid GetUserId() =>
        Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    private string GetRole() =>
        User.FindFirstValue(ClaimTypes.Role) ?? "kasir";
}
