namespace PoolMate.Api.Dtos.Admin.Player;

/// <summary>
/// DTO cho danh sách players chưa claim (chưa link với User)
/// </summary>
public class UnclaimedPlayerDto
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
    
    // Tournament info
    public int TournamentsCount { get; set; }
    public DateTime? LastTournamentDate { get; set; }
    
    // Potential matches (users với cùng email)
    public List<PotentialUserMatchDto> PotentialMatches { get; set; } = new();
}

/// <summary>
/// Thông tin User có khả năng match với Player (based on email)
/// </summary>
public class PotentialUserMatchDto
{
    public string UserId { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? UserName { get; set; }
    public string? FullName { get; set; }
    public DateTime CreatedAt { get; set; }
}

