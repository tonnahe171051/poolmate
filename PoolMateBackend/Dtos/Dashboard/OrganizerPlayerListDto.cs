namespace PoolMate.Api.Dtos.Dashboard;

public class OrganizerPlayerListDto
{
    public int TournamentPlayerId { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public int? SkillLevel { get; set; }
    
    // Thông tin bổ sung để biết họ đá giải nào
    public int TournamentId { get; set; }
    public string TournamentName { get; set; } = string.Empty; 
    public DateTime JoinedDate { get; set; }
}