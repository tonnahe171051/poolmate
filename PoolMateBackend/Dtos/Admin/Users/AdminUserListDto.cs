namespace PoolMate.Api.Dtos.Admin.Users;

public class AdminUserListDto
{
    public string Id { get; set; } = string.Empty;
    public string? UserName { get; set; }
    public string? Email { get; set; }
    public string? PhoneNumber { get; set; }

    // Info
    public string? FullName { get; set; }

    // public string? FirstName { get; set; } // Có thể bỏ nếu đã có FullName để list gọn hơn
    // public string? LastName { get; set; }  // Có thể bỏ
    public string? Nickname { get; set; }
    public string? Country { get; set; }
    public string? City { get; set; }
    public string? AvatarUrl { get; set; }

    // Security
    public bool EmailConfirmed { get; set; }
    public bool TwoFactorEnabled { get; set; }
    public int AccessFailedCount { get; set; }
    public DateTime CreatedAt { get; set; }

    // Lock Status
    public DateTime? LockoutEnd { get; set; }
    public bool IsLockedOut { get; set; }
    public bool LockoutEnabled { get; set; }

    public List<string> Roles { get; set; } = new();

    public int? LinkedPlayerId { get; set; } // User này đang sở hữu Player nào?
    public string? LinkedPlayerName { get; set; } // Tên VĐV tương ứng
}