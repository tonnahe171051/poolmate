using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PoolMate.Api.Data;
using PoolMate.Api.Dtos.Auth;
using PoolMate.Api.Dtos.Dashboard;
using PoolMate.Api.Models;

namespace PoolMate.Api.Controllers;

[ApiController]
[Route("api/admin/dashboard")]
[Authorize(Roles = UserRoles.ADMIN)]
public class AdminDashboardController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILogger<AdminDashboardController> _logger;

    public AdminDashboardController(
        ApplicationDbContext db,
        UserManager<ApplicationUser> userManager,
        ILogger<AdminDashboardController> logger)
    {
        _db = db;
        _userManager = userManager;
        _logger = logger;
    }
    
    /// <summary>
    /// Get quick summary stats (lighter version for frequent polling)
    /// </summary>
    [HttpGet("summary")]
    public async Task<IActionResult> GetQuickSummary()
    {
        try
        {
            var summary = new
            {
                totalUsers = await _userManager.Users.CountAsync(),
                totalTournaments = await _db.Tournaments.CountAsync(),
                activeTournaments = await _db.Tournaments
                    .Where(t => t.Status == TournamentStatus.InProgress)
                    .CountAsync(),
                totalVenues = await _db.Venues.CountAsync()
            };

            return Ok(summary);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching quick summary");
            return StatusCode(500, new { message = "Error fetching quick summary" });
        }
    }
    
}

