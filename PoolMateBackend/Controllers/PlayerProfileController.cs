using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PoolMate.Api.Dtos.PlayerProfile;
using PoolMate.Api.Services;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;

namespace PoolMate.Api.Controllers;

[ApiController]
[Route("api/players")]
[Authorize]
public class PlayerProfileController : ControllerBase
{
    private readonly IPlayerProfileService _service;

    public PlayerProfileController(IPlayerProfileService service)
    {
        _service = service;
    }


    [HttpPost]
    [ProducesResponseType(typeof(CreatePlayerProfileResponseDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status409Conflict)] 
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<CreatePlayerProfileResponseDto>> CreatePlayerProfile(
        [FromBody] CreatePlayerProfileDto dto,
        CancellationToken ct)
    {
        try
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrWhiteSpace(userId))
                return Unauthorized(new { message = "User not authenticated or invalid token" });
            var result = await _service.CreatePlayerProfileAsync(dto, userId, ct);
            if (result == null) 
                return BadRequest(new { message = "Failed to create player profile" });

            return CreatedAtAction(
                actionName: nameof(GetMyPlayerProfiles), 
                routeValues: null,
                value: result
            );
        }
        catch (InvalidOperationException ex) 
        {
            return Conflict(new { message = ex.Message });
        }
        catch (Exception e)
        {
            return StatusCode(500, new { message = "Internal server error." });
        }
    }

    [HttpGet("my-profiles")]
    [ProducesResponseType(typeof(List<PlayerProfileDetailDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<List<PlayerProfileDetailDto>>> GetMyPlayerProfiles(
        CancellationToken ct)
    {
        try
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (string.IsNullOrWhiteSpace(userId))
            {
                return Unauthorized(new { message = "User not authenticated" });
            }

            var profiles = await _service.GetMyPlayerProfilesAsync(userId, ct);
            if (profiles == null)
            {
                profiles = new List<PlayerProfileDetailDto>();
            }

            return Ok(profiles);
        }
        catch (Exception e)
        {
            return StatusCode(500, new { message = "Internal server error. Please try again later." });
        }
    }
    

}