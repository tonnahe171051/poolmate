namespace PoolMate.Api.Dtos.PlayerProfile;


public class PlayerListDto
{
    public int Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty; 
    public string? Nickname { get; set; }
    public string? Country { get; set; }
    public string? City { get; set; }
    public int? SkillLevel { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class PlayerListFilterDto
{
    public int PageIndex { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    
    public bool? HasSkillLevel { get; set; }

    public string? SearchTerm { get; set; }
    
    public string? Country { get; set; }
}

