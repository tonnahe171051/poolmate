namespace PoolMate.Api.Dtos.Admin.Player;

public class PlayerDetailDto
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
    public string? LinkedUserId { get; set; }
    public string? LinkedUserEmail { get; set; }
    public string? LinkedUserAvatar { get; set; }
    public List<string> DataIssues { get; set; } = new();
    public TournamentStatsDto TournamentStats { get; set; } = new();
    public List<PlayerTournamentHistoryDto> RecentTournaments { get; set; } = new();
}


public class TournamentStatsDto
{
    public int TotalTournaments { get; set; }
    public int CompletedTournaments { get; set; }
    public int ActiveTournaments { get; set; }
    public DateTime? FirstTournamentDate { get; set; }
    public DateTime? LastTournamentDate { get; set; }
}

public class PlayerTournamentHistoryDto
{
    public int TournamentId { get; set; }
    public string TournamentName { get; set; } = string.Empty;
    public DateTime TournamentDate { get; set; }
    public string? TournamentStatus { get; set; }
    public int? Seed { get; set; }
    public string PlayerStatus { get; set; } = string.Empty;
}

