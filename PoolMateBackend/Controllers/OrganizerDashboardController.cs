using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PoolMate.Api.Common;
using PoolMate.Api.Dtos.Dashboard;
using PoolMate.Api.Models;
using System.Security.Claims;
using PoolMate.Api.Dtos.Response;
using PoolMate.Api.Services;

namespace PoolMate.Api.Controllers;

[ApiController]
[Route("api/organizer/dashboard")]
[Authorize(Roles = "Organizer")]
public class OrganizerDashboardController : ControllerBase
{
    private readonly IOrganizerDashboardService _service;
    private readonly ILogger<OrganizerDashboardController> _logger;

    public OrganizerDashboardController(
        IOrganizerDashboardService service, 
        ILogger<OrganizerDashboardController> logger)
    {
        _service = service;
        _logger = logger;
    }

    [HttpGet("stats")]
    [ProducesResponseType(typeof(ApiResponse<OrganizerDashboardStatsDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<OrganizerDashboardStatsDto>>> GetStats(CancellationToken ct)
    {
        try
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
            var stats = await _service.GetStatsAsync(userId, ct);
            return Ok(ApiResponse<OrganizerDashboardStatsDto>.Ok(stats));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching stats");
            return StatusCode(500, ApiResponse<object>.Fail(500, "Internal server error"));
        }
    }
    

    
    [HttpGet("players")]
    [ProducesResponseType(typeof(ApiResponse<PagingList<OrganizerPlayerDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<ApiResponse<PagingList<OrganizerPlayerDto>>>> GetPlayers(
        [FromQuery] int? tournamentId, 
        [FromQuery] string? search,
        [FromQuery] int pageIndex = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        try
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
            
            var result = await _service.GetMyPlayersAsync(userId, tournamentId, search, pageIndex, pageSize, ct);
            
            return Ok(ApiResponse<PagingList<OrganizerPlayerDto>>.Ok(result));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching organizer players");
            return StatusCode(500, ApiResponse<object>.Fail(500, "Internal server error"));
        }
    }
    
    
    [HttpGet("tournaments")]
    [ProducesResponseType(typeof(ApiResponse<PagingList<OrganizerTournamentDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<ApiResponse<PagingList<OrganizerTournamentDto>>>> GetMyTournaments(
        [FromQuery] string? search,
        [FromQuery] TournamentStatus? status,
        [FromQuery] int pageIndex = 1,
        [FromQuery] int pageSize = 10,
        CancellationToken ct = default)
    {
        try
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
            
            var result = await _service.GetMyTournamentsAsync(userId, search, status, pageIndex, pageSize, ct);
            
            return Ok(ApiResponse<PagingList<OrganizerTournamentDto>>.Ok(result));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching organizer tournaments");
            return StatusCode(500, ApiResponse<object>.Fail(500, "Internal server error"));
        }
    }

    [HttpGet("tournament/{id}/overview")]
    [ProducesResponseType(typeof(ApiResponse<TournamentOverviewDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<ApiResponse<TournamentOverviewDto>>> GetTournamentOverview(
        int id,
        CancellationToken ct)
    {
        try
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
            var result = await _service.GetTournamentOverviewAsync(id, userId, ct);

            if (result == null)
                return NotFound(ApiResponse<object>.Fail(404, "Tournament not found or you are not the owner"));

            return Ok(ApiResponse<TournamentOverviewDto>.Ok(result));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching tournament overview");
            return StatusCode(500, ApiResponse<object>.Fail(500, "Internal server error"));
        }
    }
}