namespace PoolMate.Api.Dtos.Admin.Player;

/// <summary>
/// DTO for bulk link players to users
/// </summary>
public class BulkLinkPlayersDto
{
    public List<PlayerUserLinkDto> Links { get; set; } = new();
    public string? Reason { get; set; }
}


public class PlayerUserLinkDto
{
    public int PlayerId { get; set; }
    public string UserId { get; set; } = string.Empty;
}

/// <summary>
/// DTO for bulk unlink players
/// </summary>
public class BulkUnlinkPlayersDto
{
    public List<int> PlayerIds { get; set; } = new();
    public string? Reason { get; set; }
}

/// <summary>
/// DTO for bulk delete players
/// </summary>
public class BulkDeletePlayersDto
{
    public List<int> PlayerIds { get; set; } = new();
    public string? Reason { get; set; }
    public bool Force { get; set; } = false;  // Force delete players with tournament history
}

/// <summary>
/// Result for bulk operations
/// </summary>
public class BulkOperationResultDto
{
    public int TotalRequested { get; set; }
    public int SuccessCount { get; set; }
    public int FailedCount { get; set; }
    public int SkippedCount { get; set; }
    
    public List<BulkOperationItemDto> Results { get; set; } = new();
    
    public DateTime ProcessedAt { get; set; }
}

/// <summary>
/// Result for each item in bulk operation
/// </summary>
public class BulkOperationItemDto
{
    public int PlayerId { get; set; }
    public string? PlayerName { get; set; }
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public string Status { get; set; } = string.Empty;  // "Success", "Failed", "Skipped"
}

