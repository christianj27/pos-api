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
}
