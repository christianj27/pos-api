using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pos.Api.DTOs.Expenses;
using Pos.Api.Services.Interfaces;

namespace Pos.Api.Controllers;

[ApiController]
[Route("api/expenses")]
[Authorize(Policy = "OwnerOnly")]
public class ExpensesController(IExpenseService expenseService) : ControllerBase
{
    /// <summary>Lists operational expenses for a business date (defaults to today WIB).</summary>
    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] DateOnly? date) =>
        Ok(await expenseService.GetAllAsync(date));

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateExpenseRequest request)
    {
        var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var (expense, error) = await expenseService.CreateAsync(request, userId);
        if (expense is null) return BadRequest(new { message = error });
        return Ok(expense);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateExpenseRequest request)
    {
        var (expense, notFound, error) = await expenseService.UpdateAsync(id, request);
        if (expense is null) return notFound ? NotFound(new { message = error }) : BadRequest(new { message = error });
        return Ok(expense);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var (success, notFound, error) = await expenseService.DeleteAsync(id);
        if (!success) return notFound ? NotFound(new { message = error }) : BadRequest(new { message = error });
        return NoContent();
    }
}
