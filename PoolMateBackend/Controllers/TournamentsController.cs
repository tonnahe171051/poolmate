using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PoolMate.Api.Dtos.Tournament;
using PoolMate.Api.Services;
using System.Security.Claims;

namespace PoolMate.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class TournamentsController : ControllerBase
{
    private readonly ITournamentService _svc;
    public TournamentsController(ITournamentService svc) => _svc = svc;

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateTournamentModel m, CancellationToken ct)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var id = await _svc.CreateAsync(userId, m, ct);
        if (id is null) return BadRequest(new { message = "Create failed" });
        return Ok(new { id });
    }

    [HttpGet("{id:int}")]
    [AllowAnonymous]
    public async Task<IActionResult> Get(int id, CancellationToken ct)
    {
        var t = await _svc.GetAsync(id, ct);
        return t is null ? NotFound() : Ok(t);
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateTournamentModel m, CancellationToken ct)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var ok = await _svc.UpdateAsync(id, userId, m, ct);
        return ok ? Ok(new { id }) : Forbid();
    }

    [HttpPatch("{id:int}/flyer")]
    public async Task<IActionResult> UpdateFlyer(int id, [FromBody] UpdateFlyerModel m, CancellationToken ct)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var ok = await _svc.UpdateFlyerAsync(id, userId, m, ct);
        return ok ? Ok(new { id }) : Forbid();
    }

    [HttpPost("{id:int}/start")]
    public async Task<IActionResult> Start(int id, CancellationToken ct)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var ok = await _svc.StartAsync(id, userId, ct);
        return ok ? Ok(new { id }) : Forbid();
    }

    [HttpPost("{id:int}/end")]
    public async Task<IActionResult> End(int id, CancellationToken ct)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var ok = await _svc.EndAsync(id, userId, ct);
        return ok ? Ok(new { id }) : Forbid();
    }

    [HttpPost("payout/preview")]
    [AllowAnonymous] 
    public async Task<IActionResult> PreviewPayout([FromBody] PreviewPayoutRequest model, CancellationToken ct)
    {
        var resp = await _svc.PreviewPayoutAsync(model, ct);
        return Ok(resp);
    }

}
