namespace PoolMate.Api.Dtos.Admin.Users;


public class AdminUserDetailDto
{
    // Basic Info
    public string Id { get; set; } = string.Empty;
    public string? UserName { get; set; }
    public string? Email { get; set; }
    public bool EmailConfirmed { get; set; }
    public string? PhoneNumber { get; set; }
    public bool PhoneNumberConfirmed { get; set; }
    
    // Profile info
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? Nickname { get; set; }
    public string? Country { get; set; }
    public string? City { get; set; }
    
    // Avatar
    public string? AvatarUrl { get; set; }
    public string? AvatarPublicId { get; set; }
    
    // System info
    public DateTime CreatedAt { get; set; }
    public List<string> Roles { get; set; } = new();
    
    // Security
    public bool LockoutEnabled { get; set; }
    public DateTimeOffset? LockoutEnd { get; set; }
    public int AccessFailedCount { get; set; }
    public bool TwoFactorEnabled { get; set; }
    
    // Activity Statistics
    public UserActivityStatsDto ActivityStats { get; set; } = new();
    
    // Related Data Counts
    public UserRelatedDataDto RelatedData { get; set; } = new();
}

/// <summary>
/// Thống kê hoạt động của user
/// </summary>
public class UserActivityStatsDto
{
    public int TotalLogins { get; set; }  // Estimate based on data
    public int FailedLoginAttempts { get; set; }  // From AccessFailedCount
    public DateTime? LastActivityDate { get; set; }
    public int DaysSinceLastActivity { get; set; }
    public bool IsActive { get; set; }  // Active trong 30 ngày qua
}

/// <summary>
/// Dữ liệu liên quan đến user
/// </summary>
public class UserRelatedDataDto
{
    public int ClaimedPlayersCount { get; set; }
    public int TournamentsJoinedCount { get; set; }
    public int PostsCreatedCount { get; set; }
    public int VenuesCreatedCount { get; set; }  // Nếu là organizer
    public int TournamentsOrganizedCount { get; set; }  // Nếu là organizer
}

