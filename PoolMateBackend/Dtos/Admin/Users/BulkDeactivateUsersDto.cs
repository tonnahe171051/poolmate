namespace PoolMate.Api.Dtos.Admin.Users;

public class BulkDeactivateUsersDto
{
    public List<string> UserIds { get; set; } = new();
    public string? Reason { get; set; }
    public bool Force { get; set; } = false;
}

public class BulkReactivateUsersDto
{
    public List<string> UserIds { get; set; } = new();
    public string? Reason { get; set; }
}

public class BulkDeactivateResultDto
{
    public int TotalRequested { get; set; }
    public int SuccessCount { get; set; }
    public int FailedCount { get; set; }
    public int SkippedCount { get; set; }
    public string? Reason { get; set; }
    public List<BulkOperationItemResultDto> Results { get; set; } = new();
    public DateTime ProcessedAt { get; set; }
}

public class BulkReactivateResultDto
{
    public int TotalRequested { get; set; }
    public int SuccessCount { get; set; }
    public int FailedCount { get; set; }
    public int SkippedCount { get; set; }
    public string? Reason { get; set; }
    public List<BulkOperationItemResultDto> Results { get; set; } = new();
    public DateTime ProcessedAt { get; set; }
}

public class BulkOperationItemResultDto
{
    public string UserId { get; set; } = string.Empty;
    public string? UserName { get; set; }
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public string Status { get; set; } = string.Empty; // "Success", "Failed", "Skipped"
}