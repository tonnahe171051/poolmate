namespace PoolMate.Api.Dtos.Admin.Player;

/// <summary>
/// DTO for filtering and searching players
/// </summary>
public class PlayerFilterDto
{
    /// <summary>
    /// Search by full name (partial match, case-insensitive)
    /// </summary>
    public string? SearchName { get; set; }
    
    /// <summary>
    /// Search by email (partial match, case-insensitive)
    /// </summary>
    public string? SearchEmail { get; set; }
    
    /// <summary>
    /// Search by phone (partial match)
    /// </summary>
    public string? SearchPhone { get; set; }
    
    /// <summary>
    /// Search by tournament name (players who participated in this tournament)
    /// </summary>
    public string? SearchTournament { get; set; }
    
    /// <summary>
    /// Filter by country
    /// </summary>
    public string? Country { get; set; }
    
    /// <summary>
    /// Filter by city
    /// </summary>
    public string? City { get; set; }
    
    /// <summary>
    /// Minimum skill level (inclusive)
    /// </summary>
    public int? MinSkillLevel { get; set; }
    
    /// <summary>
    /// Maximum skill level (inclusive)
    /// </summary>
    public int? MaxSkillLevel { get; set; }
    
    /// <summary>
    /// Filter by created date from (inclusive)
    /// </summary>
    public DateTime? CreatedFrom { get; set; }
    
    /// <summary>
    /// Filter by created date to (inclusive)
    /// </summary>
    public DateTime? CreatedTo { get; set; }
    
    /// <summary>
    /// Filter by last tournament date from
    /// </summary>
    public DateTime? LastTournamentFrom { get; set; }
    
    /// <summary>
    /// Filter by last tournament date to
    /// </summary>
    public DateTime? LastTournamentTo { get; set; }
    
    /// <summary>
    /// Filter by data quality: HasEmail, HasPhone, HasSkillLevel
    /// </summary>
    public bool? HasEmail { get; set; }
    public bool? HasPhone { get; set; }
    public bool? HasSkillLevel { get; set; }
    
    /// <summary>
    /// Page number (starts from 1)
    /// </summary>
    public int PageIndex { get; set; } = 1;
    
    /// <summary>
    /// Number of items per page
    /// </summary>
    public int PageSize { get; set; } = 10;
    
    /// <summary>
    /// Sort by CreatedAt descending (newest first) by default
    /// Can be extended to support more sort options
    /// </summary>
    public string SortBy { get; set; } = "CreatedAt";
    
    /// <summary>
    /// Sort direction: "desc" or "asc"
    /// </summary>
    public string SortOrder { get; set; } = "desc";
}

