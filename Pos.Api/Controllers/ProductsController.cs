using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pos.Api.DTOs.Products;
using Pos.Api.Services.Interfaces;

namespace Pos.Api.Controllers;

[ApiController]
[Route("api/products")]
[Authorize(Policy = "AllStaff")]
public class ProductsController(IProductService productService) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] bool activeOnly = false) =>
        Ok(await productService.GetAllAsync(activeOnly));

    [HttpPost]
    [Authorize(Policy = "OwnerOnly")]
    public async Task<IActionResult> Create([FromBody] CreateProductRequest request)
    {
        var (product, error) = await productService.CreateAsync(request);
        if (product is null) return BadRequest(new { message = error });
        return CreatedAtAction(nameof(GetAll), new { id = product.Id }, product);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = "OwnerOnly")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateProductRequest request)
    {
        var (product, error) = await productService.UpdateAsync(id, request);
        if (product is null) return BadRequest(new { message = error });
        return Ok(product);
    }

    [HttpPatch("{id:guid}")]
    [Authorize(Policy = "OwnerOnly")]
    public async Task<IActionResult> ToggleActive(Guid id)
    {
        var (success, error) = await productService.ToggleActiveAsync(id);
        if (!success) return BadRequest(new { message = error });
        return NoContent();
    }
}
