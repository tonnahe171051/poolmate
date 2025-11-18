namespace PoolMate.Api.Dtos.Admin.Users;

/// <summary>
/// DTO cho User Activity Log
/// </summary>
public class UserActivityLogDto
{
    public string UserId { get; set; } = string.Empty;
    public string? UserName { get; set; }
    public string? Email { get; set; }
    
    // Activity Summary
    public UserActivitySummaryDto ActivitySummary { get; set; } = new();
    
    // Recent Activities (các hoạt động gần đây)
    public List<ActivityEntryDto> RecentActivities { get; set; } = new();
}

/// <summary>
/// Tổng hợp activity của user
/// </summary>
public class UserActivitySummaryDto
{
    // Related data counts
    public int TotalPlayers { get; set; }  // Số players đã claim
    public int TotalTournaments { get; set; }  // Số tournaments tham gia
    public int TotalPosts { get; set; }  // Số posts đã tạo
    public int TotalVenues { get; set; }  // Số venues đã tạo (nếu là organizer)
    
    // Security events
    public int TotalLoginAttempts { get; set; }  // Từ AccessFailedCount
    public int FailedLoginAttempts { get; set; }
    public DateTime? LastLoginAt { get; set; }  // Từ LockoutEnd changes
    
    // Account status changes
    public int TimesLocked { get; set; }
    public DateTime? LastLockedAt { get; set; }
    public DateTime? LastUnlockedAt { get; set; }
    
    // Dates
    public DateTime AccountCreatedAt { get; set; }
    public DateTime? LastActivityAt { get; set; }
}

/// <summary>
/// Một activity entry
/// </summary>
public class ActivityEntryDto
{
    public string ActivityType { get; set; } = string.Empty;  // "Tournament", "Post", "Player", "Login"
    public string Description { get; set; } = string.Empty;
    public DateTime ActivityDate { get; set; }
    public string? RelatedEntityId { get; set; }  // TournamentId, PostId, etc.
    public string? RelatedEntityName { get; set; }
}

