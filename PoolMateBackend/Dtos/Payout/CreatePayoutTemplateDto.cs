namespace PoolMate.Api.Dtos.Payout;
using System.ComponentModel.DataAnnotations;

public class CreatePayoutTemplateDto
{
    [Required(ErrorMessage = "Template name is required")]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;
    [Range(1, 10000, ErrorMessage = "Min Players must be at least 1")]
    public int MinPlayers { get; set; }
    [Range(1, 10000, ErrorMessage = "Max Players must be at least 1")]
    public int MaxPlayers { get; set; }
    [Required]
    [MinLength(1, ErrorMessage = "At least one rank distribution is required")]
    public List<RankPercentDto> Distribution { get; set; } = new();
}