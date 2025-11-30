namespace PoolMate.Api.Dtos.Admin.Payout;

public class PayoutSimulationRequestDto
{
    public decimal TotalPrizePool { get; set; }
    public int? TemplateId { get; set; }
    public List<RankPercentDto>? CustomDistribution { get; set; }
}

public class PayoutSimulationResultDto
{
    public decimal TotalPrize { get; set; }
    public List<PayoutBreakdownItemDto> Breakdown { get; set; } = new();
}

public class PayoutBreakdownItemDto
{
    public int Rank { get; set; }
    public double Percent { get; set; }
    public decimal Amount { get; set; } 
}