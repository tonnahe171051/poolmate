namespace PoolMate.Api.Dtos.Admin.Users;


public class AdminUserDetailDto
{
    public string Id { get; set; } = string.Empty;
    public string? UserName { get; set; }
    public string? Email { get; set; }
    public bool EmailConfirmed { get; set; }
    public string? PhoneNumber { get; set; }
    
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
}

