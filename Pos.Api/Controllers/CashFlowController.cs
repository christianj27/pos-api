using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pos.Api.Services;
using Pos.Api.Services.Interfaces;

namespace Pos.Api.Controllers;

[ApiController]
[Route("api/cash-flow")]
[Authorize(Policy = "OwnerOnly")]
public class CashFlowController(ICashFlowService cashFlowService) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetCashFlow([FromQuery] DateOnly? date)
    {
        var target = date ?? WibTimeZone.TodayWib();
        return Ok(await cashFlowService.GetCashFlowAsync(target));
    }
}
