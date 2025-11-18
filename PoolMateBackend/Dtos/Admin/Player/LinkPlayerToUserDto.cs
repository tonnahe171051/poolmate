namespace PoolMate.Api.Dtos.Admin.Player;

/// <summary>
/// DTO để link Player với ApplicationUser
/// </summary>
public class LinkPlayerToUserDto
{
    public string UserId { get; set; } = default!;
}

