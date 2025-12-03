namespace PoolMate.Api.Dtos.Payout;

public class PayoutTemplateDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int MinPlayers { get; set; }
    public int MaxPlayers { get; set; }
    public int Places { get; set; }
    public List<RankPercentDto> Distribution { get; set; } = new();
}