using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pos.Api.DTOs.ContainerLoans;
using Pos.Api.Services.Interfaces;

namespace Pos.Api.Controllers;

[ApiController]
[Route("api/container-loans")]
[Authorize(Policy = "OwnerOnly")]
public class ContainerLoansController(IContainerLoanService containerLoanService) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] Guid? customerId) =>
        Ok(await containerLoanService.GetAllAsync(customerId));

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateContainerLoanRequest request)
    {
        var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var (loan, error) = await containerLoanService.CreateAsync(request, userId);
        if (loan is null) return BadRequest(new { message = error });
        return Ok(loan);
    }
}
