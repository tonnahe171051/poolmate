namespace PoolMate.Api.Dtos.Admin.Player;

/// <summary>
/// DTO for duplicate players search result
/// </summary>
public class DuplicatePlayersDto
{
    public List<DuplicateGroupDto> DuplicateGroups { get; set; } = new();
    public int TotalGroups { get; set; }
    public int TotalDuplicates { get; set; }
}

/// <summary>
/// Group of potential duplicate players
/// </summary>
public class DuplicateGroupDto
{
    public List<DuplicatePlayerInfoDto> Players { get; set; } = new();
    public int MatchScore { get; set; }  // 0-100 confidence score
    public List<string> MatchReasons { get; set; } = new();  // ["Same email", "Similar name (95%)"]
}

/// <summary>
/// Player info in duplicate detection
/// </summary>
public class DuplicatePlayerInfoDto
{
    public int Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? Country { get; set; }
    public string? City { get; set; }
    public int TournamentsCount { get; set; }
    public DateTime? LastTournamentDate { get; set; }
    public bool HasLinkedAccount { get; set; }
    public string? LinkedUserId { get; set; }
}

/// <summary>
/// DTO for merging duplicate players
/// </summary>
public class MergePlayersDto
{
    public int PrimaryPlayerId { get; set; }  // Keep this player
    public List<int> DuplicatePlayerIds { get; set; } = new();  // Merge these into primary
    public MergeStrategy Strategy { get; set; } = MergeStrategy.KeepPrimary;
    public bool DryRun { get; set; } = false;  // Preview merge without executing
}

/// <summary>
/// Strategy for merging player data
/// </summary>
public enum MergeStrategy
{
    KeepPrimary,      // Keep all data from primary player
    PreferNewest,     // Use newest data
    PreferMostData    // Use player with most complete data
}

/// <summary>
/// Result of merge operation
/// </summary>
public class MergePlayersResultDto
{
    public bool Success { get; set; }
    public int PrimaryPlayerId { get; set; }
    public PlayerDetailDto? PrimaryPlayer { get; set; }
    public List<int> MergedPlayerIds { get; set; } = new();
    public int AffectedTournaments { get; set; }
    public MergedDataSummaryDto MergedData { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
}

/// <summary>
/// Summary of merged data
/// </summary>
public class MergedDataSummaryDto
{
    public string FullName { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public int? SkillLevel { get; set; }
    public int TotalTournaments { get; set; }
}

