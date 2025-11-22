using System.ComponentModel.DataAnnotations;

namespace PoolMate.Api.Dtos.PlayerProfile;


public class CreatePlayerProfileResponseDto
{
    public string FullName { get; set; } = string.Empty;
    public string? Nickname { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? Country { get; set; }
    public string? City { get; set; }
    public string Message { get; set; } = "Player profile created successfully";
    public DateTime CreatedAt { get; set; }
}

public class PlayerProfileDetailDto
{
    public int Id { get; set; }
    public string FullName { get; set; } = default!;
    public string? Nickname { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? Country { get; set; }
    public string? City { get; set; }
    public int? SkillLevel { get; set; }
    public DateTime CreatedAt { get; set; }
}
