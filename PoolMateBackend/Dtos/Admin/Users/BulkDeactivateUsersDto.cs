namespace PoolMate.Api.Dtos.Admin.Users;

/// <summary>
/// DTO for Bulk Deactivate Request
/// </summary>
public class BulkDeactivateUsersDto
{
    public List<string> UserIds { get; set; } = new();
    public string? Reason { get; set; }
    public bool Force { get; set; } = false;  // Force deactivate protected accounts
}

/// <summary>
/// DTO for Bulk Reactivate Request
/// </summary>
public class BulkReactivateUsersDto
{
    public List<string> UserIds { get; set; } = new();
    public string? Reason { get; set; }
}

/// <summary>
/// Response for Bulk Deactivate
/// </summary>
public class BulkDeactivateResultDto
{
    public int TotalRequested { get; set; }
    public int SuccessCount { get; set; }
    public int FailedCount { get; set; }
    public int SkippedCount { get; set; }  // Protected accounts
    
    public List<BulkOperationItemResultDto> Results { get; set; } = new();
    
    public DateTime ProcessedAt { get; set; }
}

/// <summary>
/// Response for Bulk Reactivate (same structure as Deactivate)
/// </summary>
public class BulkReactivateResultDto
{
    public int TotalRequested { get; set; }
    public int SuccessCount { get; set; }
    public int FailedCount { get; set; }
    public int SkippedCount { get; set; }  // Already active users
    
    public List<BulkOperationItemResultDto> Results { get; set; } = new();
    
    public DateTime ProcessedAt { get; set; }
}

/// <summary>
/// Result for each user in bulk operation
/// </summary>
public class BulkOperationItemResultDto
{
    public string UserId { get; set; } = string.Empty;
    public string? UserName { get; set; }
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public string Status { get; set; } = string.Empty;  // "Success", "Failed", "Skipped"
}

