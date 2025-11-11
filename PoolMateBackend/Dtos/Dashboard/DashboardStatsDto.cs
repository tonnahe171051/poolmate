namespace PoolMate.Api.Dtos.Dashboard;

public class DashboardStatsDto
{
    public OverviewStats Overview { get; set; } = new();
    public DateTime Timestamp { get; set; }
}

public class OverviewStats
{
    public int TotalUsers { get; set; }
    public double TotalUsersChange { get; set; }
    
    public int TotalTournaments { get; set; }
    public double TotalTournamentsChange { get; set; }
    
    public int ActiveTournaments { get; set; }
    public double ActiveTournamentsChange { get; set; }
    
    public int TotalVenues { get; set; }
    public double TotalVenuesChange { get; set; }
    
    public decimal MonthlyRevenue { get; set; }
    public double MonthlyRevenueChange { get; set; }
    
    public double AvgPlayersPerTournament { get; set; }
}

