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
    /// Get dashboard statistics for admin
    /// </summary>
    /// <returns>Dashboard stats including users, tournaments, posts, venues, and revenue</returns>
    [HttpGet("stats")]
    public async Task<ActionResult<DashboardStatsDto>> GetDashboardStats()
    {
        try
        {
            var now = DateTime.UtcNow;
            var thisMonthStart = new DateTime(now.Year, now.Month, 1);

            // 1. Total Users (with trend now that we have CreatedAt)
            var totalUsers = await _userManager.Users
                .Where(u => u.EmailConfirmed)
                .CountAsync();

            var lastMonthUsers = await _userManager.Users
                .Where(u => u.EmailConfirmed && u.CreatedAt < thisMonthStart)
                .CountAsync();

            var usersChange = lastMonthUsers > 0
                ? ((totalUsers - lastMonthUsers) / (double)lastMonthUsers) * 100
                : 0;

            // 2. Total Tournaments
            var totalTournaments = await _db.Tournaments.CountAsync();
            var lastMonthTournaments = await _db.Tournaments
                .Where(t => t.CreatedAt < thisMonthStart)
                .CountAsync();
            var tournamentsChange = lastMonthTournaments > 0
                ? ((totalTournaments - lastMonthTournaments) / (double)lastMonthTournaments) * 100
                : 0;

            // 3. Active Tournaments (In Progress)
            var activeTournaments = await _db.Tournaments
                .Where(t => t.Status == TournamentStatus.InProgress)
                .CountAsync();
            
            var lastMonthActive = await _db.Tournaments
                .Where(t => t.Status == TournamentStatus.InProgress && t.CreatedAt < thisMonthStart)
                .CountAsync();
            
            var activeTournamentsChange = lastMonthActive > 0
                ? ((activeTournaments - lastMonthActive) / (double)lastMonthActive) * 100
                : 0;

            // 4. Total Venues
            var totalVenues = await _db.Venues.CountAsync();
            var lastMonthVenues = await _db.Venues
                .Where(v => v.CreatedAt < thisMonthStart)
                .CountAsync();
            var venuesChange = lastMonthVenues > 0
                ? ((totalVenues - lastMonthVenues) / (double)lastMonthVenues) * 100
                : 0;

            // 6. Monthly Revenue (Estimated)
            var thisMonthTournaments = await _db.Tournaments
                .Where(t => t.CreatedAt >= thisMonthStart)
                .Include(t => t.TournamentPlayers)
                .ToListAsync();

            var monthlyRevenue = thisMonthTournaments.Sum(t =>
            {
                var playerCount = t.TournamentPlayers.Count > 0
                    ? t.TournamentPlayers.Count
                    : t.BracketSizeEstimate ?? 0;
                var entryFee = t.EntryFee ?? 0;
                return entryFee * playerCount;
            });

            // Previous month revenue for comparison
            var prevMonthStart = thisMonthStart.AddMonths(-1);
            var prevMonthTournaments = await _db.Tournaments
                .Where(t => t.CreatedAt >= prevMonthStart && t.CreatedAt < thisMonthStart)
                .Include(t => t.TournamentPlayers)
                .ToListAsync();

            var prevMonthRevenue = prevMonthTournaments.Sum(t =>
            {
                var playerCount = t.TournamentPlayers.Count > 0
                    ? t.TournamentPlayers.Count
                    : t.BracketSizeEstimate ?? 0;
                var entryFee = t.EntryFee ?? 0;
                return entryFee * playerCount;
            });

            var revenueChange = prevMonthRevenue > 0
                ? (double)(((monthlyRevenue - prevMonthRevenue) / prevMonthRevenue) * 100)
                : 0;

            // 7. Average Players per Tournament
            var tournamentsWithPlayers = await _db.Tournaments
                .Where(t => t.TournamentPlayers.Any())
                .Select(t => t.TournamentPlayers.Count)
                .ToListAsync();

            var avgPlayersPerTournament = tournamentsWithPlayers.Any()
                ? tournamentsWithPlayers.Average()
                : 0;

            // Build response
            var stats = new DashboardStatsDto
            {
                Overview = new OverviewStats
                {
                    TotalUsers = totalUsers,
                    TotalUsersChange = Math.Round(usersChange, 1),
                    TotalTournaments = totalTournaments,
                    TotalTournamentsChange = Math.Round(tournamentsChange, 1),
                    ActiveTournaments = activeTournaments,
                    ActiveTournamentsChange = Math.Round(activeTournamentsChange, 1),
                    TotalVenues = totalVenues,
                    TotalVenuesChange = Math.Round(venuesChange, 1),
                    MonthlyRevenue = Math.Round(monthlyRevenue, 2),
                    MonthlyRevenueChange = Math.Round(revenueChange, 1),
                    AvgPlayersPerTournament = Math.Round(avgPlayersPerTournament, 1)
                },
                Timestamp = DateTime.UtcNow
            };

            _logger.LogInformation("Dashboard stats retrieved successfully at {Timestamp}", stats.Timestamp);

            return Ok(stats);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching dashboard stats");
            return StatusCode(500, new { message = "Error fetching dashboard stats", error = ex.Message });
        }
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
    /// Diagnostic endpoint to debug why stats are returning 0
    /// </summary>
    [HttpGet("debug")]
    public async Task<IActionResult> GetDebugInfo()
    {
        try
        {
            var now = DateTime.UtcNow;
            var thisMonthStart = new DateTime(now.Year, now.Month, 1);

            // Raw counts
            var allUsers = await _userManager.Users.CountAsync();
            var confirmedUsers = await _userManager.Users.Where(u => u.EmailConfirmed).CountAsync();
            var usersBeforeThisMonth = await _userManager.Users
                .Where(u => u.EmailConfirmed && u.CreatedAt < thisMonthStart)
                .CountAsync();

            var allTournaments = await _db.Tournaments.CountAsync();
            var tournamentsBeforeThisMonth = await _db.Tournaments
                .Where(t => t.CreatedAt < thisMonthStart)
                .CountAsync();

            var allVenues = await _db.Venues.CountAsync();
            var venuesBeforeThisMonth = await _db.Venues
                .Where(v => v.CreatedAt < thisMonthStart)
                .CountAsync();

            // Sample data
            var sampleUsers = await _userManager.Users
                .OrderBy(u => u.CreatedAt)
                .Take(5)
                .Select(u => new
                {
                    u.Email,
                    u.EmailConfirmed,
                    u.CreatedAt,
                    isBeforeThisMonth = u.CreatedAt < thisMonthStart
                })
                .ToListAsync();

            var sampleTournaments = await _db.Tournaments
                .OrderBy(t => t.CreatedAt)
                .Take(5)
                .Select(t => new
                {
                    t.Name,
                    t.Status,
                    t.CreatedAt,
                    isBeforeThisMonth = t.CreatedAt < thisMonthStart
                })
                .ToListAsync();

            return Ok(new
            {
                currentDateTime = now,
                thisMonthStart = thisMonthStart,
                rawCounts = new
                {
                    allUsers,
                    confirmedUsers,
                    usersBeforeThisMonth,
                    usersThisMonth = confirmedUsers - usersBeforeThisMonth,
                    allTournaments,
                    tournamentsBeforeThisMonth,
                    tournamentsThisMonth = allTournaments - tournamentsBeforeThisMonth,
                    allVenues,
                    venuesBeforeThisMonth,
                    venuesThisMonth = allVenues - venuesBeforeThisMonth
                },
                calculations = new
                {
                    usersChange = usersBeforeThisMonth > 0 
                        ? ((confirmedUsers - usersBeforeThisMonth) / (double)usersBeforeThisMonth) * 100 
                        : 0,
                    tournamentsChange = tournamentsBeforeThisMonth > 0
                        ? ((allTournaments - tournamentsBeforeThisMonth) / (double)tournamentsBeforeThisMonth) * 100
                        : 0
                },
                sampleUsers,
                sampleTournaments,
                diagnosis = allUsers == 0 
                    ? "❌ Database is EMPTY! No users found."
                    : confirmedUsers == 0
                    ? "❌ No users with EmailConfirmed = true!"
                    : "✅ Data exists. Check if dates are correct."
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in debug endpoint");
            return StatusCode(500, new { message = "Error in debug", error = ex.Message });
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

