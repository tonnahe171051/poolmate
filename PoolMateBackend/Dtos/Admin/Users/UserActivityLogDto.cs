namespace PoolMate.Api.Dtos.Admin.Users;

public class UserActivityLogDto
{
    public string UserId { get; set; } = string.Empty;
    public string? UserName { get; set; }
    public string? Email { get; set; }
    public UserActivitySummaryDto ActivitySummary { get; set; } = new();
    public List<ActivityEntryDto> RecentActivities { get; set; } = new();
}

public class UserActivitySummaryDto
{
    public int TotalPlayers { get; set; }
    public int TotalTournaments { get; set; }
    public int TotalPosts { get; set; }
    public DateTime AccountCreatedAt { get; set; }
    public DateTime? LastActivityAt { get; set; } 
    public int FailedLoginAttempts { get; set; }
    public DateTime? LockoutEnd { get; set; } 
    public bool IsLockedOut { get; set; } 
}

public class ActivityEntryDto
{
    public string ActivityType { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateTime ActivityDate { get; set; }
    public string? RelatedEntityId { get; set; }
    public string? RelatedEntityName { get; set; }
}