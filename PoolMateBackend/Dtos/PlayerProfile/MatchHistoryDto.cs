namespace PoolMate.Api.Dtos.PlayerProfile;

public class MatchHistoryDto
{
    public int MatchId { get; set; }
    public int TournamentId { get; set; }
    public string TournamentName { get; set; } = string.Empty;
    public DateTime TournamentDate { get; set; }
    public string GameType { get; set; } = string.Empty; 
    public string StageType { get; set; } = string.Empty; 
    public string RoundName { get; set; } = string.Empty; 
    public string BracketSide { get; set; } = string.Empty; 
    public string OpponentName { get; set; } = string.Empty;
    public string Result { get; set; } = string.Empty; 
    public string Score { get; set; } = string.Empty; 
    public int RaceTo { get; set; }
    public DateTime? MatchDate { get; set; }
}