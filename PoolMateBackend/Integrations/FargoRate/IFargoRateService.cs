using PoolMate.Api.Integrations.FargoRate.Models;

namespace PoolMate.Api.Integrations.FargoRate
{
    public interface IFargoRateService
    {
        Task<List<PlayerFargoSearchResult>> BatchSearchPlayersAsync(List<BatchSearchRequest> requests);
        Task<int> ApplyFargoRatingsAsync(int tournamentId, List<ApplyFargoRatingRequest> requests);
    }
}
