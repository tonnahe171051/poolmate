namespace PoolMate.Api.Dtos.Dashboard;

public class OrganizerDashboardStatsDto
{
    public int ActiveTournaments { get; set; }
    public int UpcomingTournaments { get; set; }
    public int CompletedTournaments { get; set; }
    public int TotalParticipants { get; set; } 

    public int TotalMatches { get; set; } 
    public double AvgPlayersPerTournament { get; set; }
    public DateTime Timestamp { get; set; }
}