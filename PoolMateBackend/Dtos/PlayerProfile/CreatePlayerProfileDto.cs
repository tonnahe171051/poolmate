using System.ComponentModel.DataAnnotations;

namespace PoolMate.Api.Dtos.PlayerProfile;


public class CreatePlayerProfileDto
{

    [Required(ErrorMessage = "Full name is required")]
    [MaxLength(200, ErrorMessage = "Full name cannot exceed 200 characters")]
    public string FullName { get; set; } = default!;
    

    [MaxLength(100, ErrorMessage = "Nickname cannot exceed 100 characters")]
    public string? Nickname { get; set; }
    

    [EmailAddress(ErrorMessage = "Invalid email format")]
    public string? Email { get; set; }
    

    [Phone(ErrorMessage = "Invalid phone number format")]
    public string? Phone { get; set; }
    

    [MaxLength(2, ErrorMessage = "Country code must be 2 characters (ISO format)")]
    [MinLength(2, ErrorMessage = "Country code must be 2 characters (ISO format)")]
    public string? Country { get; set; }
    

    [MaxLength(100, ErrorMessage = "City name cannot exceed 100 characters")]
    public string? City { get; set; }
    

    [Range(0, 100, ErrorMessage = "Skill level must be between 0 and 100")]
    public int? SkillLevel { get; set; }
}

public class CreatePlayerProfileResponseDto
{
    public int PlayerId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string? Email { get; set; }
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
