namespace PoolMate.Api.Dtos.Admin.Player;

/// <summary>
/// DTO cho chi tiết Player (detail view)
/// Bao gồm thông tin đầy đủ về player và các relationships
/// </summary>
public class PlayerDetailDto
{
    // Basic Info
    public int Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string? Nickname { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? Country { get; set; }
    public string? City { get; set; }
    public int? SkillLevel { get; set; }
    public DateTime CreatedAt { get; set; }
    
    // Linked User Info
    public bool HasLinkedAccount { get; set; }
    public LinkedUserDetailDto? LinkedUser { get; set; }
    
    // Tournament Statistics
    public TournamentStatsDto TournamentStats { get; set; } = new();
    
    // Recent Tournaments (top 10 gần nhất)
    public List<PlayerTournamentHistoryDto> RecentTournaments { get; set; } = new();
}

/// <summary>
/// Thông tin chi tiết User đã link với Player
/// </summary>
public class LinkedUserDetailDto
{
    public string UserId { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? UserName { get; set; }
    public string? FullName { get; set; }
    public string? Nickname { get; set; }
    public string? PhoneNumber { get; set; }
    public string? ProfilePicture { get; set; }
    public DateTime CreatedAt { get; set; }
}

/// <summary>
/// Thống kê tournament của Player
/// </summary>
public class TournamentStatsDto
{
    public int TotalTournaments { get; set; }
    public int CompletedTournaments { get; set; }
    public int ActiveTournaments { get; set; }
    public DateTime? FirstTournamentDate { get; set; }
    public DateTime? LastTournamentDate { get; set; }
}

/// <summary>
/// Lịch sử tham gia tournament của Player
/// </summary>
public class PlayerTournamentHistoryDto
{
    public int TournamentId { get; set; }
    public string TournamentName { get; set; } = string.Empty;
    public DateTime TournamentDate { get; set; }
    public string? TournamentStatus { get; set; }
    public int? Seed { get; set; }
    public string PlayerStatus { get; set; } = string.Empty;
}

