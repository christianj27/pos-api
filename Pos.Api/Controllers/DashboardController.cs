using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pos.Api.Services;
using Pos.Api.Services.Interfaces;

namespace Pos.Api.Controllers;

[ApiController]
[Route("api/dashboard")]
[Authorize(Policy = "OwnerOnly")]
public class DashboardController(IDashboardService dashboardService) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetDashboard([FromQuery] DateOnly? date)
    {
        var target = date ?? WibTimeZone.TodayWib();
        return Ok(await dashboardService.GetDashboardAsync(target));
    }
}
