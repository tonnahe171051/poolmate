namespace PoolMate.Api.Dtos.Admin.Player;

/// <summary>
/// DTO for player export request
/// </summary>
public class ExportPlayersDto
{
    public ExportFormat Format { get; set; } = ExportFormat.CSV;
    public PlayerFilterDto? Filters { get; set; }
    public bool IncludeTournamentHistory { get; set; } = false;
    public bool IncludeLinkedUsers { get; set; } = false;
}

/// <summary>
/// Export format options
/// </summary>
public enum ExportFormat
{
    CSV,
    Excel
}

/// <summary>
/// Result of export operation
/// </summary>
public class ExportResultDto
{
    public string JobId { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;  // "Processing", "Completed", "Failed"
    public string? DownloadUrl { get; set; }
    public string? FileName { get; set; }
    public long? FileSize { get; set; }
    public int TotalRecords { get; set; }
    public DateTime? CompletedAt { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// DTO for player CRUD operations
/// </summary>
public class CreatePlayerDto
{
    public string FullName { get; set; } = string.Empty;
    public string? Nickname { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? Country { get; set; }
    public string? City { get; set; }
    public int? SkillLevel { get; set; }
    public string? LinkedUserId { get; set; }  // Optional: Link immediately
}


/// <summary>
/// Result of player CRUD operation
/// </summary>
public class PlayerOperationResultDto
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public PlayerDetailDto? Player { get; set; }
    public List<string> Warnings { get; set; } = new();
}

