using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pos.Api.DTOs.Locations;
using Pos.Api.Services.Interfaces;

namespace Pos.Api.Controllers;

[ApiController]
[Route("api/locations")]
[Authorize(Policy = "AllStaff")]
public class LocationsController(ILocationService locationService) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll() =>
        Ok(await locationService.GetAllAsync());

    [HttpPost]
    [Authorize(Policy = "OwnerOnly")]
    public async Task<IActionResult> Create([FromBody] CreateLocationRequest request)
    {
        var (location, error) = await locationService.CreateAsync(request);
        if (location is null) return BadRequest(new { message = error });
        return CreatedAtAction(nameof(GetAll), new { id = location.Id }, location);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = "OwnerOnly")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateLocationRequest request)
    {
        var (location, error) = await locationService.UpdateAsync(id, request);
        if (location is null) return BadRequest(new { message = error });
        return Ok(location);
    }
}
