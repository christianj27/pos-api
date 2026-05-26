using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pos.Api.DTOs.Stock;
using Pos.Api.Services.Interfaces;

namespace Pos.Api.Controllers;

[ApiController]
[Route("api/stock")]
[Authorize(Policy = "AllStaff")]
public class StockController(IStockService stockService) : ControllerBase
{
    [HttpGet("levels")]
    public async Task<IActionResult> GetLevels([FromQuery] Guid? locationId) =>
        Ok(await stockService.GetLevelsAsync(locationId));

    [HttpGet("movements")]
    public async Task<IActionResult> GetMovements([FromQuery] DateOnly? date)
    {
        var role = User.FindFirstValue(ClaimTypes.Role) ?? "kasir";
        return Ok(await stockService.GetMovementsAsync(date, role));
    }

    [HttpPost("movements")]
    [Authorize(Policy = "OwnerOnly")]
    public async Task<IActionResult> CreateMovement([FromBody] CreateMovementRequest request)
    {
        var userId = GetUserId();
        var (success, error) = await stockService.CreateMovementAsync(request, userId);
        if (!success) return BadRequest(new { message = error });
        return Ok(new { message = "Pergerakan stok berhasil dicatat." });
    }

    [HttpPost("movements/bulk")]
    [Authorize(Policy = "OwnerOnly")]
    public async Task<IActionResult> BulkCreateMovement([FromBody] BulkCreateMovementRequest request)
    {
        var userId = GetUserId();
        var (success, error) = await stockService.BulkCreateMovementAsync(request, userId);
        if (!success) return BadRequest(new { message = error });
        return Ok(new { message = "Pergerakan stok berhasil dicatat." });
    }

    [HttpPost("transfer")]
    [Authorize(Policy = "OwnerOrKasir")]
    public async Task<IActionResult> Transfer([FromBody] TransferRequest request)
    {
        var userId = GetUserId();
        var (success, error) = await stockService.TransferAsync(request, userId);
        if (!success) return BadRequest(new { message = error });
        return Ok(new { message = "Transfer stok berhasil." });
    }

    [HttpPost("transfer/bulk")]
    [Authorize(Policy = "OwnerOrKasir")]
    public async Task<IActionResult> BulkTransfer([FromBody] BulkTransferRequest request)
    {
        var userId = GetUserId();
        var (success, error) = await stockService.BulkTransferAsync(request, userId);
        if (!success) return BadRequest(new { message = error });
        return Ok(new { message = "Transfer stok berhasil." });
    }

    [HttpPost("vendor-exchange")]
    [Authorize(Policy = "OwnerOnly")]
    public async Task<IActionResult> VendorExchange([FromBody] VendorExchangeRequest request)
    {
        var userId = GetUserId();
        var (success, error) = await stockService.VendorExchangeAsync(request, userId);
        if (!success) return BadRequest(new { message = error });
        return Ok(new { message = "Pertukaran vendor berhasil dicatat." });
    }

    [HttpPost("vendor-exchange/bulk")]
    [Authorize(Policy = "OwnerOnly")]
    public async Task<IActionResult> BulkVendorExchange([FromBody] BulkVendorExchangeRequest request)
    {
        var userId = GetUserId();
        var (success, error) = await stockService.BulkVendorExchangeAsync(request, userId);
        if (!success) return BadRequest(new { message = error });
        return Ok(new { message = "Pertukaran vendor berhasil dicatat." });
    }

    [HttpPost("production")]
    [Authorize(Policy = "OwnerOrKasir")]
    public async Task<IActionResult> Production([FromBody] ProductionRequest request)
    {
        var userId = GetUserId();
        var (success, error) = await stockService.ProductionAsync(request, userId);
        if (!success) return BadRequest(new { message = error });
        return Ok(new { message = "Produksi berhasil dicatat." });
    }

    [HttpPost("movements/{id:guid}/reverse")]
    [Authorize(Policy = "OwnerOnly")]
    public async Task<IActionResult> ReverseMovement(Guid id)
    {
        var userId = GetUserId();
        var (movements, error) = await stockService.ReverseMovementAsync(id, userId);
        if (movements is null) return BadRequest(new { message = error });
        return Ok(movements);
    }

    [HttpPost("adjustment")]
    [Authorize(Policy = "OwnerOnly")]
    public async Task<IActionResult> Adjustment([FromBody] AdjustmentRequest request)
    {
        var userId = GetUserId();
        var (success, error) = await stockService.AdjustmentAsync(request, userId);
        if (!success) return BadRequest(new { message = error });
        return Ok(new { message = "Penyesuaian stok berhasil dicatat." });
    }

    [HttpPost("adjustment/bulk")]
    [Authorize(Policy = "OwnerOnly")]
    public async Task<IActionResult> BulkAdjustment([FromBody] BulkAdjustmentRequest request)
    {
        var userId = GetUserId();
        var (success, error) = await stockService.BulkAdjustmentAsync(request, userId);
        if (!success) return BadRequest(new { message = error });
        return Ok(new { message = "Penyesuaian stok berhasil dicatat." });
    }

    private Guid GetUserId() =>
        Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
}
