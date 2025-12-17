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
    /// Get dashboard chart statistics for User Registrations and Tournament Creations.
    /// Compares monthly data for Current Year vs Previous Year.
    /// </summary>
    /// <returns>DashboardChartResponse containing monthly statistics for charts</returns>
    [HttpGet("stats")]
    public async Task<IActionResult> GetDashboardStats()
    {
        try
        {
            var currentYear = DateTime.UtcNow.Year;
            var previousYear = currentYear - 1;

            _logger.LogInformation("Fetching dashboard stats for years {CurrentYear} and {PreviousYear}", 
                currentYear, previousYear);

            // Get user registration stats
            var userStats = await GetUserRegistrationStatsAsync(currentYear, previousYear);
            
            // Get tournament creation stats
            var tournamentStats = await GetTournamentCreationStatsAsync(currentYear, previousYear);

            var response = new DashboardChartResponse
            {
                CurrentYear = currentYear,
                PreviousYear = previousYear,
                UserRegistrations = userStats,
                TournamentCreations = tournamentStats,
                GeneratedAt = DateTimeOffset.UtcNow
            };

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching dashboard stats: {Message}", ex.Message);
            return StatusCode(500, new { message = "Error fetching dashboard statistics", error = ex.Message });
        }
    }

    /// <summary>
    /// Get monthly user registration statistics for two years.
    /// Performs efficient database-level grouping.
    /// </summary>
    private async Task<ChartComparisonDto> GetUserRegistrationStatsAsync(int currentYear, int previousYear)
    {
        // Define date range to filter (Jan 1 of previous year to Dec 31 of current year)
        var startDate = new DateTime(previousYear, 1, 1);
        var endDate = new DateTime(currentYear, 12, 31, 23, 59, 59);

        // First: Filter at DB level to reduce data, then group in memory
        // This avoids EF Core translation issues with complex GroupBy
        var filteredData = await _userManager.Users
            .AsNoTracking()
            .Where(u => u.CreatedAt >= startDate && u.CreatedAt <= endDate)
            .Select(u => new { u.CreatedAt.Year, u.CreatedAt.Month })
            .ToListAsync();

        // Group in memory (data is already filtered, so this is efficient)
        var monthlyData = filteredData
            .GroupBy(x => new { x.Year, x.Month })
            .Select(g => new MonthlyCountDto
            {
                Year = g.Key.Year,
                Month = g.Key.Month,
                Count = g.Count()
            })
            .ToList();

        return BuildChartComparison(monthlyData, currentYear, previousYear, "User Registrations");
    }

    /// <summary>
    /// Get monthly tournament creation statistics for two years.
    /// Performs efficient database-level grouping.
    /// </summary>
    private async Task<ChartComparisonDto> GetTournamentCreationStatsAsync(int currentYear, int previousYear)
    {
        // Define date range to filter
        var startDate = new DateTime(previousYear, 1, 1);
        var endDate = new DateTime(currentYear, 12, 31, 23, 59, 59);

        // First: Filter at DB level to reduce data, then group in memory
        var filteredData = await _db.Tournaments
            .AsNoTracking()
            .Where(t => t.CreatedAt >= startDate && t.CreatedAt <= endDate)
            .Select(t => new { t.CreatedAt.Year, t.CreatedAt.Month })
            .ToListAsync();

        // Group in memory
        var monthlyData = filteredData
            .GroupBy(x => new { x.Year, x.Month })
            .Select(g => new MonthlyCountDto
            {
                Year = g.Key.Year,
                Month = g.Key.Month,
                Count = g.Count()
            })
            .ToList();

        return BuildChartComparison(monthlyData, currentYear, previousYear, "Tournaments Created");
    }

    /// <summary>
    /// Build ChartComparisonDto from raw monthly data.
    /// Ensures all 12 months are present (fills missing months with 0).
    /// </summary>
    private static ChartComparisonDto BuildChartComparison(
        List<MonthlyCountDto> monthlyData, 
        int currentYear, 
        int previousYear,
        string label)
    {
        // Initialize arrays with 12 zeros (one for each month)
        var currentYearData = new int[12];
        var previousYearData = new int[12];

        // Fill in the actual data from query results
        foreach (var item in monthlyData)
        {
            // Month is 1-based, array is 0-based
            int index = item.Month - 1;
            
            if (index >= 0 && index < 12)
            {
                if (item.Year == currentYear)
                {
                    currentYearData[index] = item.Count;
                }
                else if (item.Year == previousYear)
                {
                    previousYearData[index] = item.Count;
                }
            }
        }

        // Calculate totals
        var currentYearTotal = currentYearData.Sum();
        var previousYearTotal = previousYearData.Sum();

        // Calculate percentage change (avoid division by zero)
        double percentageChange = 0;
        if (previousYearTotal > 0)
        {
            percentageChange = Math.Round(
                ((double)(currentYearTotal - previousYearTotal) / previousYearTotal) * 100, 
                2);
        }
        else if (currentYearTotal > 0)
        {
            percentageChange = 100; // 100% growth from 0
        }

        return new ChartComparisonDto
        {
            CurrentYear = new ChartSeriesDto
            {
                Year = currentYear,
                Label = $"{label} {currentYear}",
                Data = currentYearData
            },
            PreviousYear = new ChartSeriesDto
            {
                Year = previousYear,
                Label = $"{label} {previousYear}",
                Data = previousYearData
            },
            CurrentYearTotal = currentYearTotal,
            PreviousYearTotal = previousYearTotal,
            PercentageChange = percentageChange
        };
    }
}

/// <summary>
/// Internal DTO for monthly count data from database query.
/// </summary>
public class MonthlyCountDto
{
    public int Year { get; set; }
    public int Month { get; set; }
    public int Count { get; set; }
}

