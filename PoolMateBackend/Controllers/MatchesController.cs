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
    private readonly IBracketService _bracketService;

    public MatchesController(IBracketService bracketService)
        => _bracketService = bracketService;

    [HttpPost("{id:int}/force-complete")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> ForceComplete(int id, CancellationToken ct)
    {
        try
        {
            await _bracketService.ForceCompleteMatchAsync(id, ct);
            return NoContent();
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

    [HttpPut("{id:int}")]
    public async Task<ActionResult<MatchDto>> Update(int id, [FromBody] UpdateMatchRequest request, CancellationToken ct)
    {
        try
        {
            var result = await _bracketService.UpdateMatchAsync(id, request, ct);
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
            var result = await _bracketService.CorrectMatchResultAsync(id, request, ct);
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
