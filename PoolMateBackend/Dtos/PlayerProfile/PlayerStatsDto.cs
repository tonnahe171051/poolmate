namespace PoolMate.Api.Dtos.PlayerProfile;

public class PlayerStatsDto
{
    public int TotalMatches { get; set; }
    public int TotalWins { get; set; }
    public int TotalLosses { get; set; }
    public double WinRate { get; set; }
    public int TotalTournaments { get; set; }
    public List<string> RecentForm { get; set; } = new();
    public List<GameTypeStatsDto> StatsByGameType { get; set; } = new();
}

public class GameTypeStatsDto
{
    public string GameType { get; set; } = string.Empty;
    public int Wins { get; set; }
    public int Losses { get; set; }
    public double WinRate { get; set; }
}