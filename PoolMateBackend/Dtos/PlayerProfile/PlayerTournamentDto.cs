namespace PoolMate.Api.Dtos.PlayerProfile;

public class PlayerTournamentDto
{

    public int TournamentId { get; set; }
    public string TournamentName { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public string Status { get; set; } = string.Empty; // Upcoming, InProgress, Completed
    
    // Thông tin chuyên môn
    public string GameType { get; set; } = string.Empty; // 8-ball, 9-ball, 10-ball
    public string BracketType { get; set; } = string.Empty; // Single, Double Elimination
    
    // Địa điểm
    public string? VenueName { get; set; }
    
    // Thông tin cá nhân của Player tại giải này
    public string RegistrationStatus { get; set; } = string.Empty; // Confirmed, Unconfirmed
    public int? Seed { get; set; } // Hạt giống số mấy
    public int? SkillLevelSnapshot { get; set; } // Skill lúc đánh giải
}

