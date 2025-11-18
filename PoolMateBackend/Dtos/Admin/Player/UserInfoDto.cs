namespace PoolMate.Api.Dtos.Admin.Player;

/// <summary>
/// DTO cho thông tin User đã link với Player
/// </summary>
public class UserInfoDto
{
    public string UserId { get; set; } = default!;
    public string Email { get; set; } = default!;
    public string? FullName { get; set; }
    public string? Nickname { get; set; }
}

