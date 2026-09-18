using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pos.Api.Services;
using Pos.Api.Services.Interfaces;
using System.Security.Claims;

namespace Pos.Api.Controllers;

[ApiController]
[Route("api/dashboard")]
[Authorize]
public class DashboardController(IDashboardService dashboardService) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetDashboard([FromQuery] DateOnly? date)
    {
        var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var role   = User.FindFirstValue(ClaimTypes.Role) ?? "kasir";
        var target = date ?? WibTimeZone.TodayWib();
        return Ok(await dashboardService.GetDashboardAsync(target, userId, role));
    }

    /// <summary>
    /// FR-DSH-012 "Pergerakan Stok" — period-filtered summary for this Dashboard section only.
    /// Every other dashboard section keeps using <see cref="GetDashboard"/> and its single-date filter.
    /// </summary>
    [HttpGet("stock-summary")]
    public async Task<IActionResult> GetStockSummary(
        [FromQuery] string? period,
        [FromQuery] DateOnly? date,
        [FromQuery(Name = "start_date")] DateOnly? startDate,
        [FromQuery(Name = "end_date")] DateOnly? endDate)
    {
        var anchor = date ?? WibTimeZone.TodayWib();

        if (!StockPeriodRange.TryResolve(period, anchor, startDate, endDate,
                out var normalizedPeriod, out var rangeStart, out var rangeEnd, out var error))
            return BadRequest(new { message = error });

        return Ok(await dashboardService.GetStockSummaryAsync(normalizedPeriod, rangeStart, rangeEnd));
    }
}
