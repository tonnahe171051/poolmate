namespace PoolMate.Api.Dtos.Admin.Users;

/// <summary>
/// DTO cho User Statistics - Thống kê tổng quan về users trong hệ thống
/// </summary>
public class UserStatisticsDto
{
    // Overview Statistics
    public int TotalUsers { get; set; }
    public int ActiveUsers { get; set; }  // Not locked out
    public int LockedUsers { get; set; }  // Currently locked
    public int DeactivatedUsers { get; set; }  // Permanently locked
    
    // Email & Phone Verification
    public int EmailConfirmedUsers { get; set; }
    public int EmailUnconfirmedUsers { get; set; }
    public double EmailConfirmationRate { get; set; }
    
    public int PhoneConfirmedUsers { get; set; }
    public int PhoneUnconfirmedUsers { get; set; }
    public double PhoneConfirmationRate { get; set; }
    
    // Security
    public int TwoFactorEnabledUsers { get; set; }
    public double TwoFactorAdoptionRate { get; set; }
    
    // Recent Activity
    public int UsersCreatedLast7Days { get; set; }
    public int UsersCreatedLast30Days { get; set; }
    public int UsersCreatedToday { get; set; }
    
    // Role Distribution
    public List<RoleDistributionDto> RoleDistribution { get; set; } = new();
    
    // Geographic Distribution (Top 10)
    public List<GeographicDistributionDto> TopCountries { get; set; } = new();
    public List<GeographicDistributionDto> TopCities { get; set; } = new();
    
    // Monthly Growth (Last 6 months)
    public List<UserGrowthTrendDto> MonthlyGrowth { get; set; } = new();
}

/// <summary>
/// Phân bố theo Role
/// </summary>
public class RoleDistributionDto
{
    public string RoleName { get; set; } = string.Empty;
    public int Count { get; set; }
    public double Percentage { get; set; }
}

/// <summary>
/// Phân bố theo địa lý (Country/City)
/// </summary>
public class GeographicDistributionDto
{
    public string Location { get; set; } = string.Empty;
    public int Count { get; set; }
    public double Percentage { get; set; }
}

/// <summary>
/// Xu hướng tăng trưởng theo tháng
/// </summary>
public class UserGrowthTrendDto
{
    public int Year { get; set; }
    public int Month { get; set; }
    public string MonthName { get; set; } = string.Empty;
    public int NewUsers { get; set; }
    public int TotalUsers { get; set; }
}

