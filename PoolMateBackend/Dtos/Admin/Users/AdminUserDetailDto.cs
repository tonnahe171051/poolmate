namespace PoolMate.Api.Dtos.Admin.Users;

public class AdminUserDetailDto
{
    public string Id { get; set; } = string.Empty;
    public string? UserName { get; set; }
    public string? Email { get; set; }
    public bool EmailConfirmed { get; set; }
    public string? PhoneNumber { get; set; }
    public bool PhoneNumberConfirmed { get; set; }
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? Nickname { get; set; }
    public string? Country { get; set; }
    public string? City { get; set; }
    public string? AvatarUrl { get; set; }
    public DateTime CreatedAt { get; set; }
    public List<string> Roles { get; set; } = new();
    public bool LockoutEnabled { get; set; }
    public DateTimeOffset? LockoutEnd { get; set; }
    public bool IsLockedOut { get; set; }
    public int AccessFailedCount { get; set; }
    public bool TwoFactorEnabled { get; set; }
    public int? LinkedPlayerId { get; set; }
    public string? LinkedPlayerName { get; set; }
    public UserActivityStatsDto ActivityStats { get; set; } = new();
    public UserRelatedDataDto RelatedData { get; set; } = new();
}

public class UserActivityStatsDto
{
    public int FailedLoginAttempts { get; set; }
    public DateTime? LastActivityDate { get; set; }
    public int DaysSinceLastActivity { get; set; }
    public bool IsActive { get; set; }
}

public class UserRelatedDataDto
{
    public int ClaimedPlayersCount { get; set; }
    public int TournamentsJoinedCount { get; set; }
    public int PostsCreatedCount { get; set; }
    public int VenuesCreatedCount { get; set; }
    public int TournamentsOrganizedCount { get; set; }
}