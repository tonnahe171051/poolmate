namespace PoolMate.Api.Dtos.Admin.Player;

/// <summary>
/// DTO cho danh sách players (list view)
/// </summary>
public class PlayerListDto
{
    public int Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string? Nickname { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? Country { get; set; }
    public string? City { get; set; }
    public int? SkillLevel { get; set; }
    public DateTime CreatedAt { get; set; }
}

