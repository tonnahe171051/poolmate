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
    

    /// <summary>
    /// TEST endpoint without authentication - to verify query logic
    /// </summary>
    [HttpGet("test-no-auth")]
    [AllowAnonymous]
    public async Task<IActionResult> TestNoAuth()
    {
        try
        {
            var now = DateTime.UtcNow;
            var thisMonthStart = new DateTime(now.Year, now.Month, 1);

            var totalUsers = await _userManager.Users
                .Where(u => u.EmailConfirmed)
                .CountAsync();

            var lastMonthUsers = await _userManager.Users
                .Where(u => u.EmailConfirmed && u.CreatedAt < thisMonthStart)
                .CountAsync();

            var usersChange = lastMonthUsers > 0
                ? ((totalUsers - lastMonthUsers) / (double)lastMonthUsers) * 100
                : 0;

            var totalTournaments = await _db.Tournaments.CountAsync();
            var lastMonthTournaments = await _db.Tournaments
                .Where(t => t.CreatedAt < thisMonthStart)
                .CountAsync();
            var tournamentsChange = lastMonthTournaments > 0
                ? ((totalTournaments - lastMonthTournaments) / (double)lastMonthTournaments) * 100
                : 0;

            var totalVenues = await _db.Venues.CountAsync();
            var lastMonthVenues = await _db.Venues
                .Where(v => v.CreatedAt < thisMonthStart)
                .CountAsync();
            var venuesChange = lastMonthVenues > 0
                ? ((totalVenues - lastMonthVenues) / (double)lastMonthVenues) * 100
                : 0;

            return Ok(new
            {
                message = "TEST - No authentication required",
                currentDateTime = now,
                thisMonthStart,
                results = new
                {
                    totalUsers,
                    lastMonthUsers,
                    usersChange = Math.Round(usersChange, 1),
                    totalTournaments,
                    lastMonthTournaments,
                    tournamentsChange = Math.Round(tournamentsChange, 1),
                    totalVenues,
                    lastMonthVenues,
                    venuesChange = Math.Round(venuesChange, 1)
                },
                note = totalUsers > 0 
                    ? "✅ Query logic works! If /stats returns 0, check authorization/authentication."
                    : "❌ No data found!"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in test-no-auth");
            return StatusCode(500, new { error = ex.Message, stackTrace = ex.StackTrace });
        }
    }
}

