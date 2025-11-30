using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PoolMate.Api.Data;
using PoolMate.Api.Dtos.Dashboard;
using PoolMate.Api.Models;
using System.Security.Claims;
using PoolMate.Api.Dtos.Response;
using PoolMate.Api.Services;

namespace PoolMate.Api.Controllers;

[ApiController]
[Route("api/organizer/dashboard")]
[Authorize ]
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

    [HttpGet("activities")]
    [ProducesResponseType(typeof(ApiResponse<List<OrganizerActivityDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<List<OrganizerActivityDto>>>> GetActivities(
        [FromQuery] int limit = 20,
        CancellationToken ct = default)
    {
        try
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
            var activities = await _service.GetRecentActivitiesAsync(userId, limit, ct);

            return Ok(ApiResponse<List<OrganizerActivityDto>>.Ok(activities));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching activities");
            return StatusCode(500, ApiResponse<object>.Fail(500, "Internal server error"));
        }
    }
}