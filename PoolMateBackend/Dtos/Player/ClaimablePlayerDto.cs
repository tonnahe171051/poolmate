namespace PoolMate.Api.Dtos.Player;

/// <summary>
/// DTO cho danh sách Players mà user có thể claim
/// </summary>
public class ClaimablePlayerDto
{
    public int Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? Country { get; set; }
    public string? City { get; set; }
    public int? SkillLevel { get; set; }
    public DateTime CreatedAt { get; set; }
    
    /// <summary>
    /// Player đã được claim chưa
    /// </summary>
    public bool IsClaimed { get; set; }
    
    /// <summary>
    /// Email có match với user hiện tại không
    /// </summary>
    public bool EmailMatches { get; set; }
}

