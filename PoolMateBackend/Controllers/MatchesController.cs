using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PoolMate.Api.Dtos.Tournament;
using PoolMate.Api.Services;

namespace PoolMate.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class MatchesController : ControllerBase
{
    private readonly IMatchService _matchService;

    public MatchesController(IMatchService matchService)
        => _matchService = matchService;

    [HttpPut("{id:int}")]
    public async Task<ActionResult<MatchDto>> Update(int id, [FromBody] UpdateMatchRequest request, CancellationToken ct)
    {
        try
        {
            var result = await _matchService.UpdateMatchAsync(id, request, ct);
            return Ok(result);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("{id:int}/correct-result")]
    public async Task<ActionResult<MatchDto>> CorrectResult(int id, [FromBody] CorrectMatchResultRequest request, CancellationToken ct)
    {
        try
        {
            var result = await _matchService.CorrectMatchResultAsync(id, request, ct);
            return Ok(result);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}
