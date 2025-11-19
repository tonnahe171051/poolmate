namespace PoolMate.Api.Dtos.Dashboard;


public class OrganizerDashboardStatsDto
{

    public int ActiveTournaments { get; set; }
    

    public int UpcomingTournaments { get; set; }
    

    public int TotalParticipants { get; set; }
    

    public decimal TotalRevenue { get; set; }
    


    public decimal NetProfit { get; set; }

    public DateTime Timestamp { get; set; }
}

