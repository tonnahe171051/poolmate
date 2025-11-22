using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PoolMate.Api.Dtos.PlayerProfile;
using PoolMate.Api.Services;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using PoolMate.Api.Models;
using PoolMate.Api.Dtos.Response;

namespace PoolMate.Api.Controllers;

[ApiController]
[Route("api/players")]
[Authorize]
public class PlayerProfileController : ControllerBase
{
    private readonly IPlayerProfileService _service;
    private readonly UserManager<ApplicationUser> _userManager;

    public PlayerProfileController(IPlayerProfileService service, UserManager<ApplicationUser> userManager)
    {
        _service = service;
        _userManager = userManager;
    }


    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<CreatePlayerProfileResponseDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<CreatePlayerProfileResponseDto>> CreatePlayerProfile(
        CancellationToken ct)
    {
        try
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrWhiteSpace(userId))
            {
                return Unauthorized(ApiResponse<CreatePlayerProfileResponseDto>.Fail(401, "User not authenticated"));
            }

            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
            {
                return BadRequest(ApiResponse<CreatePlayerProfileResponseDto>.Fail(400, "User account not found"));
            }

            var result = await _service.CreatePlayerProfileAsync(userId, user, ct);
            if (result == null)
                return BadRequest(ApiResponse<CreatePlayerProfileResponseDto>.Fail(400, "Failed to create player profile"));
            
            return CreatedAtAction(
                actionName: nameof(GetMyPlayerProfiles),
                routeValues: null,
                value: ApiResponse<CreatePlayerProfileResponseDto>.Created(result, "Player profile created successfully")
            );
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(ApiResponse<CreatePlayerProfileResponseDto>.Fail(409, ex.Message));
        }
        catch (Exception e)
        {
            return StatusCode(500, ApiResponse<CreatePlayerProfileResponseDto>.Fail(500, "Internal server error"));
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
                return Unauthorized(ApiResponse<List<PlayerProfileDetailDto>>.Fail(401, "User not authenticated"));
            }

            var profiles = await _service.GetMyPlayerProfilesAsync(userId, ct);
            if (profiles == null) profiles = new List<PlayerProfileDetailDto>();

            return Ok(ApiResponse<List<PlayerProfileDetailDto>>.Ok(profiles));
        }
        catch (Exception e)
        {
            return StatusCode(500, ApiResponse<List<PlayerProfileDetailDto>>.Fail(500, "Internal server error"));
        }
    }
}