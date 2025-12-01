using PoolMate.Api.Models; // Để dùng Enum TournamentStatus, GameType

namespace PoolMate.Api.Dtos.Dashboard;

public class OrganizerTournamentDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty; // Upcoming, InProgress...
    public string GameType { get; set; } = string.Empty; // 8-ball, 9-ball...
    
    public DateTime StartDate { get; set; }
    public DateTime CreatedAt { get; set; }
    
    // Thống kê nhanh cho từng giải
    public int PlayerCount { get; set; } 
    public int MatchCount { get; set; }
}

