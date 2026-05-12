using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pos.Api.DTOs.Payments;
using Pos.Api.DTOs.Transactions;
using Pos.Api.Services.Interfaces;

namespace Pos.Api.Controllers;

[ApiController]
[Route("api/transactions")]
[Authorize(Policy = "AllStaff")]
public class TransactionsController(
    ITransactionService transactionService,
    IPaymentService paymentService) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] DateOnly? date)
    {
        var userId = GetUserId();
        var role = GetRole();
        return Ok(await transactionService.GetAllAsync(userId, role, date));
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateTransactionRequest request)
    {
        var userId = GetUserId();
        var role = GetRole();
        var (transaction, error) = await transactionService.CreateAsync(request, userId, role);
        if (transaction is null) return BadRequest(new { message = error });
        return CreatedAtAction(nameof(GetById), new { id = transaction.Id }, transaction);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var userId = GetUserId();
        var role = GetRole();
        var result = await transactionService.GetByIdAsync(id, userId, role);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpPut("{id:guid}/status")]
    public async Task<IActionResult> UpdateStatus(Guid id, [FromBody] UpdateTransactionStatusRequest request)
    {
        var userId = GetUserId();
        var role = GetRole();
        var (success, error) = await transactionService.UpdateStatusAsync(id, request, userId, role);
        if (!success) return BadRequest(new { message = error });
        return Ok(new { message = "Status transaksi berhasil diperbarui." });
    }

    [HttpPost("{id:guid}/payments")]
    [Authorize(Policy = "OwnerOnly")]
    public async Task<IActionResult> AddPayment(Guid id, [FromBody] CreatePaymentRequest request)
    {
        var (payment, error) = await paymentService.AddPaymentAsync(id, request);
        if (payment is null) return BadRequest(new { message = error });
        return Ok(payment);
    }

    private Guid GetUserId() =>
        Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    private string GetRole() =>
        User.FindFirstValue(ClaimTypes.Role) ?? "kasir";
}
