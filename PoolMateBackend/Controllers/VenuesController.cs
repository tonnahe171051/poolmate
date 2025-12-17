using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PoolMate.Api.Dtos.Auth;
using PoolMate.Api.Dtos.Venue;
using PoolMate.Api.Services;
using System.Security.Claims;

namespace PoolMate.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class VenuesController : ControllerBase
{
    private readonly IVenueService _svc;
    public VenuesController(IVenueService svc) => _svc = svc;

    [HttpGet]
    public async Task<IActionResult> Search([FromQuery] string? query, [FromQuery] string? city,
                                            [FromQuery] string? country, [FromQuery] int take = 10,
                                            CancellationToken ct = default)
        => Ok(await _svc.SearchAsync(query, city, country, take, ct));

    [HttpPost]
    [Authorize(Roles = UserRoles.ORGANIZER)]
    public async Task<IActionResult> Create([FromBody] CreateVenueRequest m, CancellationToken ct)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var id = await _svc.CreateAsync(userId, m, ct);
        return id is null ? BadRequest(new { message = "Create venue failed" }) : Ok(new { id });
    }
}
