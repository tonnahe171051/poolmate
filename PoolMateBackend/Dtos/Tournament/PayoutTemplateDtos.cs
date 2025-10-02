namespace PoolMate.Api.Dtos.Tournament
{
    // template trả cho FE
    public record PayoutTemplateDto(
        int Id,
        string Name,
        int MinPlayers,
        int MaxPlayers,
        int Places,
        List<RankPercent> Percents
    );
}
