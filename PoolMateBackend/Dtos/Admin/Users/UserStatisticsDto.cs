namespace PoolMate.Api.Dtos.Admin.Users;

public class UserStatisticsDto
{
    public int TotalUsers { get; set; }
    public int ActiveUsers { get; set; }      
    public int LockedUsers { get; set; }      
    public int DeactivatedUsers { get; set; } 
    public int EmailConfirmedUsers { get; set; }
    public int EmailUnconfirmedUsers { get; set; }
    public double EmailConfirmationRate { get; set; }
    public int UsersCreatedLast7Days { get; set; }
    public int UsersCreatedLast30Days { get; set; }
    public int UsersCreatedToday { get; set; }
    public List<RoleDistributionDto> RoleDistribution { get; set; } = new();
    public List<GeographicDistributionDto> TopCountries { get; set; } = new();
    public List<UserGrowthTrendDto> MonthlyGrowth { get; set; } = new();
}
public class RoleDistributionDto
{
    public string RoleName { get; set; } = string.Empty;
    public int Count { get; set; }
    public double Percentage { get; set; }
}
public class GeographicDistributionDto
{
    public string Location { get; set; } = string.Empty; 
    public int Count { get; set; }
    public double Percentage { get; set; }
}
public class UserGrowthTrendDto
{
    public int Year { get; set; }
    public int Month { get; set; }
    public string MonthName { get; set; } = string.Empty; 
    public int NewUsers { get; set; }   
    public int TotalUsers { get; set; } 
}

