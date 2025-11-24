namespace PoolMate.Api.Dtos.Admin.Player;

public class PlayerStatisticsDto
{
    // Overview Statistics
    public int TotalPlayers { get; set; }
    public int ClaimedPlayers { get; set; }
    public int UnclaimedPlayers { get; set; }
    
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

/// Phân bố theo Skill Level
public class SkillLevelDistributionDto
{
    public int? SkillLevel { get; set; }
    public int Count { get; set; }
    public double Percentage { get; set; }
}

/// Phân bố theo địa lý (Country/City)
public class GeographicDistributionDto
{
    public string Location { get; set; } = string.Empty;
    public int Count { get; set; }
    public double Percentage { get; set; }
}

/// Xu hướng tăng trưởng theo tháng
public class PlayerGrowthTrendDto
{
    public int Year { get; set; }
    public int Month { get; set; }
    public string MonthName { get; set; } = string.Empty;
    public int NewPlayers { get; set; }
    public int TotalPlayers { get; set; }
}

