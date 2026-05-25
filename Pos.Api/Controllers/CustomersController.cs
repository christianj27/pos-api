using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pos.Api.DTOs.Customers;
using Pos.Api.Services.Interfaces;
using System.Security.Claims;

namespace Pos.Api.Controllers;

[ApiController]
[Route("api/customers")]
[Authorize(Policy = "AllStaff")]
public class CustomersController(ICustomerService customerService) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] bool activeOnly = false)
    {
        var role = User.FindFirstValue(ClaimTypes.Role);
        return Ok(await customerService.GetAllAsync(activeOnly, role));
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateCustomerRequest request)
    {
        var (customer, error) = await customerService.CreateAsync(request);
        if (customer is null) return BadRequest(new { message = error });
        return CreatedAtAction(nameof(GetAll), new { id = customer.Id }, customer);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateCustomerRequest request)
    {
        var role = User.FindFirstValue(ClaimTypes.Role);
        // Only owners can change the confidential flag; strip it for other roles
        var effectiveRequest = role != "owner" && request.IsConfidential.HasValue
            ? request with { IsConfidential = null }
            : request;
        var (customer, error) = await customerService.UpdateAsync(id, effectiveRequest);
        if (customer is null) return BadRequest(new { message = error });
        return Ok(customer);
    }

    [HttpGet("{id:guid}/pricing")]
    [Authorize(Policy = "AllStaff")]
    public async Task<IActionResult> GetPricing(Guid id)
    {
        var result = await customerService.GetPricingAsync(id);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpPut("{id:guid}/pricing")]
    [Authorize(Policy = "OwnerOnly")]
    public async Task<IActionResult> UpdatePricing(Guid id, [FromBody] UpdateCustomerPricingRequest request)
    {
        var (success, error) = await customerService.UpdatePricingAsync(id, request);
        if (!success) return BadRequest(new { message = error });
        return Ok(new { message = "Harga khusus berhasil diperbarui." });
    }

    [HttpGet("{id:guid}/debt")]
    [Authorize(Policy = "OwnerOnly")]
    public async Task<IActionResult> GetDebt(Guid id)
    {
        var result = await customerService.GetDebtAsync(id);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpGet("{id:guid}/container-loans")]
    [Authorize(Policy = "OwnerOnly")]
    public async Task<IActionResult> GetContainerLoans(Guid id)
    {
        var result = await customerService.GetContainerLoansAsync(id);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpGet("{id:guid}/debt-history")]
    [Authorize(Policy = "OwnerOrKasir")]
    public async Task<IActionResult> GetDebtHistory(Guid id)
    {
        var result = await customerService.GetDebtHistoryAsync(id);
        return result is null ? NotFound() : Ok(result);
    }
}
