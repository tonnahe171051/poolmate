namespace PoolMate.Api.Dtos.Admin.Player;

/// DTO for duplicate players search result
public class DuplicatePlayersDto
{
    public List<DuplicateGroupDto> DuplicateGroups { get; set; } = new();
    public int TotalGroups { get; set; }
    public int TotalDuplicates { get; set; }
}

/// Group of potential duplicate players
public class DuplicateGroupDto
{
    public List<DuplicatePlayerInfoDto> Players { get; set; } = new();
    public int MatchScore { get; set; }  // 0-100 confidence score
    public List<string> MatchReasons { get; set; } = new();  // ["Same email", "Similar name (95%)"]
}

/// Player info in duplicate detection
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
}

/// DTO for merging duplicate players
public class MergePlayersDto
{
    public int PrimaryPlayerId { get; set; }  // Keep this player
    public List<int> DuplicatePlayerIds { get; set; } = new();  // Merge these into primary
    public MergeStrategy Strategy { get; set; } = MergeStrategy.KeepPrimary;
    public bool DryRun { get; set; } = false;  // Preview merge without executing
}

/// Strategy for merging player data
public enum MergeStrategy
{
    KeepPrimary,      // Keep all data from primary player
    PreferNewest,     // Use newest data
    PreferMostData    // Use player with most complete data
}

/// Result of merge operation
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

/// Summary of merged data
public class MergedDataSummaryDto
{
    public string FullName { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public int? SkillLevel { get; set; }
    public int TotalTournaments { get; set; }
}

