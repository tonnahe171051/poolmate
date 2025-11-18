namespace PoolMate.Api.Dtos.Admin.Player;

/// <summary>
/// DTO cho thống kê tổng quan về Players trong hệ thống
/// </summary>
public class PlayerStatisticsDto
{
    // Overview Statistics
    public int TotalPlayers { get; set; }
    public int PlayersWithLinkedAccount { get; set; }
    public int PlayersWithoutLinkedAccount { get; set; }
    public double LinkedAccountPercentage { get; set; }
    
    // Recent Activity
    public int PlayersCreatedLast30Days { get; set; }
    public int PlayersCreatedLast7Days { get; set; }
    public int PlayersCreatedToday { get; set; }
    
    // Skill Level Distribution
    public List<SkillLevelDistributionDto> SkillLevelDistribution { get; set; } = new();
    
    // Geographic Distribution (Top 10)
    public List<GeographicDistributionDto> TopCountries { get; set; } = new();
    public List<GeographicDistributionDto> TopCities { get; set; } = new();
    
    // Tournament Activity
    public int PlayersWithTournaments { get; set; }
    public int PlayersWithoutTournaments { get; set; }
    public int ActivePlayersLast30Days { get; set; }
    
    // Growth Trends (Last 6 months)
    public List<PlayerGrowthTrendDto> MonthlyGrowth { get; set; } = new();
}

/// <summary>
/// Phân bố theo Skill Level
/// </summary>
public class SkillLevelDistributionDto
{
    public int? SkillLevel { get; set; }
    public int Count { get; set; }
    public double Percentage { get; set; }
}

/// <summary>
/// Phân bố theo địa lý (Country/City)
/// </summary>
public class GeographicDistributionDto
{
    public string Location { get; set; } = string.Empty;
    public int Count { get; set; }
    public double Percentage { get; set; }
}

/// <summary>
/// Xu hướng tăng trưởng theo tháng
/// </summary>
public class PlayerGrowthTrendDto
{
    public int Year { get; set; }
    public int Month { get; set; }
    public string MonthName { get; set; } = string.Empty;
    public int NewPlayers { get; set; }
    public int TotalPlayers { get; set; }
}

