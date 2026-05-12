using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pos.Api.DTOs.DebtPayments;
using Pos.Api.Services.Interfaces;

namespace Pos.Api.Controllers;

[ApiController]
[Route("api/debt-payments")]
[Authorize(Policy = "OwnerOrKasir")]
public class DebtPaymentsController(IDebtPaymentService debtPaymentService) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] DateOnly? date) =>
        Ok(await debtPaymentService.GetAllAsync(date));

    [HttpPost]
    [Authorize(Policy = "OwnerOnly")]
    public async Task<IActionResult> Create([FromBody] CreateDebtPaymentRequest request)
    {
        var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var (payment, error) = await debtPaymentService.CreateAsync(request, userId);
        if (payment is null) return BadRequest(new { message = error });
        return Ok(payment);
    }
}
