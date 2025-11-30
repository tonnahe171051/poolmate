namespace PoolMate.Api.Dtos.Admin.Player;

public class PlayerFilterDto
{
    public string? SearchName { get; set; }
    
    public string? SearchEmail { get; set; }
    
    public string? SearchPhone { get; set; }
    public string? SearchTournament { get; set; }
    
    public string? Country { get; set; }
    
    public string? City { get; set; }
    
    public int? MinSkillLevel { get; set; }
    
    public int? MaxSkillLevel { get; set; }
    
    public DateTime? CreatedFrom { get; set; }
    
    public DateTime? CreatedTo { get; set; }
    
    public DateTime? LastTournamentFrom { get; set; }
    
    public DateTime? LastTournamentTo { get; set; }
    
    public bool? HasEmail { get; set; }
    public bool? HasPhone { get; set; }
    public bool? HasSkillLevel { get; set; }
    
    public int PageIndex { get; set; } = 1;
    
    public int PageSize { get; set; } = 10;
    
    public string SortBy { get; set; } = "CreatedAt";
    
    public string SortOrder { get; set; } = "desc";
}

