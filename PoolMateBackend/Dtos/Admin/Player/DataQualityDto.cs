namespace PoolMate.Api.Dtos.Admin.Player;

/// DTO for data quality report
public class DataQualityReportDto
{
    public DataQualityOverviewDto Overview { get; set; } = new();
    public DataQualityIssuesDto Issues { get; set; } = new();
    public List<string> Recommendations { get; set; } = new();
    public DateTime GeneratedAt { get; set; }
}

/// Overview of data quality
public class DataQualityOverviewDto
{
    public int TotalPlayers { get; set; }
    public int PlayersWithIssues { get; set; }
    public double IssuePercentage { get; set; }
    public int HealthyPlayers { get; set; }
    public double HealthyPercentage { get; set; }
}

/// Detailed breakdown of issues
public class DataQualityIssuesDto
{
    // Missing data
    public int MissingEmail { get; set; }
    public int MissingPhone { get; set; }
    public int MissingSkillLevel { get; set; }
    public int MissingLocation { get; set; }  // No country or city
    
    // Invalid data
    public int InvalidEmail { get; set; }
    public int InvalidPhone { get; set; }
    public int InvalidSkillLevel { get; set; }  // Out of range
    
    // Activity issues
    public int InactivePlayers { get; set; }  // No tournament > 1 year
    public int NeverPlayedTournament { get; set; }
    
    // Duplicates
    public int PotentialDuplicates { get; set; }
}

/// List of players with specific data quality issues
public class PlayersWithIssuesDto
{
    public List<PlayerIssueDto> Players { get; set; } = new();
    public int TotalCount { get; set; }
}

/// Player with data quality issues
public class PlayerIssueDto
{
    public int Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public List<string> Issues { get; set; } = new();
    public DateTime CreatedAt { get; set; }
    public DateTime? LastTournamentDate { get; set; }
}

/// DTO for player validation request
public class ValidatePlayerDto
{
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public int? SkillLevel { get; set; }
}

/// Result of player validation
public class ValidationResultDto
{
    public bool IsValid { get; set; }
    public List<ValidationErrorDto> Errors { get; set; } = new();
}

/// Validation error detail
public class ValidationErrorDto
{
    public string Field { get; set; } = string.Empty;
    public string ErrorMessage { get; set; } = string.Empty;
    public string? SuggestedFix { get; set; }
}

