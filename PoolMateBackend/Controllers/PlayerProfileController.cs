using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PoolMate.Api.Dtos.PlayerProfile;
using PoolMate.Api.Services;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using PoolMate.Api.Common;
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
    private readonly ILogger<PlayerProfileController> _logger;

    public PlayerProfileController(IPlayerProfileService service, UserManager<ApplicationUser> userManager,
        ILogger<PlayerProfileController> logger)
    {
        _service = service;
        _userManager = userManager;
        _logger = logger;
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
                return BadRequest(
                    ApiResponse<CreatePlayerProfileResponseDto>.Fail(400, "Failed to create player profile"));

            return CreatedAtAction(
                actionName: nameof(GetMyPlayerProfiles),
                routeValues: null,
                value: ApiResponse<CreatePlayerProfileResponseDto>.Created(result,
                    "Player profile created successfully")
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


    [HttpGet("my-matches")]
    [ProducesResponseType(typeof(ApiResponse<PagingList<MatchHistoryDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<ApiResponse<PagingList<MatchHistoryDto>>>> GetMyMatches(
        [FromQuery] int pageIndex = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        try
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrWhiteSpace(userId))
            {
                return Unauthorized(ApiResponse<object>.Fail(401, "User not authenticated"));
            }

            var profiles = await _service.GetMyPlayerProfilesAsync(userId, ct);
            var mainProfile = profiles.FirstOrDefault();
            if (mainProfile == null)
            {
                return NotFound(ApiResponse<object>.Fail(404,
                    "You don't have a player profile yet. Please create one first."));
            }

            var history = await _service.GetMatchHistoryAsync(mainProfile.Id, pageIndex, pageSize, ct);
            return Ok(ApiResponse<PagingList<MatchHistoryDto>>.Ok(history));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching match history for user {UserId}",
                User.FindFirstValue(ClaimTypes.NameIdentifier));
            return StatusCode(500, ApiResponse<PagingList<MatchHistoryDto>>.Fail(500, "Internal server error"));
        }
    }
    
    [HttpGet("{playerId}/stats")]
    [ProducesResponseType(typeof(ApiResponse<PlayerStatsDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)] // ✅ Đã thêm
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)] // ✅ Đã thêm
    public async Task<ActionResult<ApiResponse<PlayerStatsDto>>> GetPlayerStats(
        int playerId,
        CancellationToken ct)
    {
        try
        {
            var stats = await _service.GetPlayerStatsAsync(playerId, ct);
            return Ok(ApiResponse<PlayerStatsDto>.Ok(stats));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching stats for player {PlayerId}", playerId);
            return StatusCode(500, ApiResponse<object>.Fail(500, "Internal server error"));
        }
    }

    [HttpGet("my-stats")]
    [ProducesResponseType(typeof(ApiResponse<PlayerStatsDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<PlayerStatsDto>>> GetMyStats(
        CancellationToken ct)
    {
        try
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrWhiteSpace(userId))
                return Unauthorized(ApiResponse<object>.Fail(401, "User not authenticated"));
            var profiles = await _service.GetMyPlayerProfilesAsync(userId, ct);
            var mainProfile = profiles.FirstOrDefault();

            if (mainProfile == null)
                return NotFound(ApiResponse<object>.Fail(404, "You don't have a player profile yet."));
            var stats = await _service.GetPlayerStatsAsync(mainProfile.Id, ct);
            return Ok(ApiResponse<PlayerStatsDto>.Ok(stats));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching stats for user {UserId}",
                User.FindFirstValue(ClaimTypes.NameIdentifier));
            return StatusCode(500, ApiResponse<object>.Fail(500, "Internal server error"));
        }
    }
}