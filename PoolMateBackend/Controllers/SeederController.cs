using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PoolMate.Api.Data;
using PoolMate.Api.Services;

namespace PoolMate.Api.Controllers;

/// <summary>
/// Seeder controller for development/testing purposes
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class SeederController : ControllerBase
{
    private readonly DashboardDataSeeder _seeder;
    private readonly ApplicationDbContext _context;
    private readonly ILogger<SeederController> _logger;

    public SeederController(
        DashboardDataSeeder seeder,
        ApplicationDbContext context,
        ILogger<SeederController> logger)
    {
        _seeder = seeder;
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// Seed database with test data for dashboard testing
    /// WARNING: Use only in development environment
    /// </summary>
    /// <returns>Seeding result</returns>
    [HttpPost("seed-dashboard-data")]
    public async Task<IActionResult> SeedDashboardData()
    {
        try
        {
            _logger.LogInformation("Seeding request received");

            // Check before seeding
            var usersBefore = await _context.Users.CountAsync();
            var venuesBefore = await _context.Venues.CountAsync();
            var tournamentsBefore = await _context.Tournaments.CountAsync();

            _logger.LogInformation($"Before seeding: {usersBefore} users, {venuesBefore} venues, {tournamentsBefore} tournaments");

            // Run seeder
            await _seeder.SeedAsync();

            // Verify after seeding
            var usersAfter = await _context.Users.CountAsync();
            var venuesAfter = await _context.Venues.CountAsync();
            var tournamentsAfter = await _context.Tournaments.CountAsync();
            var playersAfter = await _context.TournamentPlayers.CountAsync();

            _logger.LogInformation($"After seeding: {usersAfter} users, {venuesAfter} venues, {tournamentsAfter} tournaments, {playersAfter} players");

            // Calculate what was actually created
            var usersCreated = usersAfter - usersBefore;
            var venuesCreated = venuesAfter - venuesBefore;
            var tournamentsCreated = tournamentsAfter - tournamentsBefore;

            if (usersCreated == 0 && venuesCreated == 0 && tournamentsCreated == 0)
            {
                return Ok(new
                {
                    success = true,
                    message = "⚠️ Database already contains data. No new data was seeded.",
                    currentCounts = new
                    {
                        users = usersAfter,
                        venues = venuesAfter,
                        tournaments = tournamentsAfter,
                        players = playersAfter
                    }
                });
            }

            return Ok(new
            {
                success = true,
                message = "✅ Database seeded successfully with test data for dashboard!",
                created = new
                {
                    users = usersCreated,
                    venues = venuesCreated,
                    tournaments = tournamentsCreated,
                    players = playersAfter
                },
                totalCounts = new
                {
                    users = usersAfter,
                    venues = venuesAfter,
                    tournaments = tournamentsAfter,
                    players = playersAfter
                },
                note = "You can now test the Admin Dashboard APIs at GET /api/admin/dashboard/stats"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during seeding");
            return StatusCode(500, new
            {
                success = false,
                message = "❌ Error seeding database",
                error = ex.Message,
                stackTrace = ex.StackTrace
            });
        }
    }

    /// <summary>
    /// Check if database already has seeded data
    /// </summary>
    [HttpGet("check-data")]
    public async Task<IActionResult> CheckData()
    {
        var now = DateTime.UtcNow;
        var thisMonthStart = new DateTime(now.Year, now.Month, 1);

        var totalUsers = await _context.Users.CountAsync();
        var confirmedUsers = await _context.Users.CountAsync(u => u.EmailConfirmed);
        var usersBeforeThisMonth = await _context.Users.CountAsync(u => u.CreatedAt < thisMonthStart);
        
        var totalVenues = await _context.Venues.CountAsync();
        var totalTournaments = await _context.Tournaments.CountAsync();
        var totalPlayers = await _context.TournamentPlayers.CountAsync();

        // Sample users
        var sampleUsers = await _context.Users
            .OrderBy(u => u.CreatedAt)
            .Take(5)
            .Select(u => new
            {
                u.Email,
                u.EmailConfirmed,
                u.CreatedAt
            })
            .ToListAsync();

        return Ok(new
        {
            currentDateTime = now,
            thisMonthStart,
            database = new
            {
                totalUsers,
                confirmedUsers,
                usersBeforeThisMonth,
                totalVenues,
                totalTournaments,
                totalPlayers
            },
            sampleUsers,
            note = totalUsers == 0
                ? "⚠️ Database is empty! Run POST /api/seeder/seed-dashboard-data to seed data"
                : "✅ Database has data. If dashboard shows 0, there may be a query issue."
        });
    }
}

