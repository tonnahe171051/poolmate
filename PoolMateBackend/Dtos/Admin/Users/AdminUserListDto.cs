namespace PoolMate.Api.Dtos.Admin.Users;


public class AdminUserListDto
{
    public string Id { get; set; } = string.Empty;
    public string? UserName { get; set; }
    public string? Email { get; set; }
    public string? PhoneNumber { get; set; }
    public string? FullName { get; set; }
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? Nickname { get; set; }
    public string? Country { get; set; }
    public string? City { get; set; }
    public bool EmailConfirmed { get; set; }
    public DateTime CreatedAt { get; set; }
    
    // Roles
    public List<string> Roles { get; set; } = new();
    
    // Avatar
    public string? AvatarUrl { get; set; }
}

