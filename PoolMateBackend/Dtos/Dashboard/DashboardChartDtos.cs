namespace PoolMate.Api.Dtos.Dashboard;

/// <summary>
/// Response DTO for dashboard chart statistics.
/// Contains data for User Registration and Tournament creation charts.
/// </summary>
public class DashboardChartResponse
{
    /// <summary>
    /// The current year being compared (e.g., 2025)
    /// </summary>
    public int CurrentYear { get; set; }
    
    /// <summary>
    /// The previous year being compared (e.g., 2024)
    /// </summary>
    public int PreviousYear { get; set; }
    
    /// <summary>
    /// User registration statistics for current vs previous year
    /// </summary>
    public ChartComparisonDto UserRegistrations { get; set; } = new();
    
    /// <summary>
    /// Tournament creation statistics for current vs previous year
    /// </summary>
    public ChartComparisonDto TournamentCreations { get; set; } = new();
    
    /// <summary>
    /// Timestamp when this data was generated
    /// </summary>
    public DateTimeOffset GeneratedAt { get; set; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// Comparison data for current year vs previous year.
/// Contains two series of 12 monthly data points each.
/// </summary>
public class ChartComparisonDto
{
    /// <summary>
    /// Data series for the current year
    /// </summary>
    public ChartSeriesDto CurrentYear { get; set; } = new();
    
    /// <summary>
    /// Data series for the previous year
    /// </summary>
    public ChartSeriesDto PreviousYear { get; set; } = new();
    
    /// <summary>
    /// Total count for current year
    /// </summary>
    public int CurrentYearTotal { get; set; }
    
    /// <summary>
    /// Total count for previous year
    /// </summary>
    public int PreviousYearTotal { get; set; }
    
    /// <summary>
    /// Percentage change from previous year to current year.
    /// Positive = growth, Negative = decline.
    /// </summary>
    public double PercentageChange { get; set; }
}

/// <summary>
/// A single data series representing monthly counts for one year.
/// </summary>
public class ChartSeriesDto
{
    /// <summary>
    /// The year this series represents
    /// </summary>
    public int Year { get; set; }
    
    /// <summary>
    /// Series name/label for the chart legend
    /// </summary>
    public string Label { get; set; } = string.Empty;
    
    /// <summary>
    /// Array of 12 integers representing counts for each month (Jan=index 0, Dec=index 11).
    /// Months with no data will have value 0.
    /// </summary>
    public int[] Data { get; set; } = new int[12];
    
    /// <summary>
    /// Month labels for X-axis (Jan, Feb, Mar, etc.)
    /// </summary>
    public string[] Labels { get; set; } = new[]
    {
        "Jan", "Feb", "Mar", "Apr", "May", "Jun",
        "Jul", "Aug", "Sep", "Oct", "Nov", "Dec"
    };
}

