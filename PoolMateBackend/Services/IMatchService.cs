using PoolMate.Api.Dtos.Tournament;
using PoolMate.Api.Models;

namespace PoolMate.Api.Services
{
    public interface IMatchService
    {
        Task<MatchDto> GetMatchAsync(int matchId, CancellationToken ct);
        Task<MatchDto> UpdateMatchAsync(int matchId, UpdateMatchRequest request, CancellationToken ct);
        Task<MatchDto> CorrectMatchResultAsync(int matchId, CorrectMatchResultRequest request, CancellationToken ct);
        Task<MatchScoreUpdateResponse> UpdateLiveScoreAsync(int matchId, UpdateLiveScoreRequest request, ScoringContext actor, CancellationToken ct);
        Task<MatchScoreUpdateResponse> CompleteMatchAsync(int matchId, CompleteMatchRequest request, ScoringContext actor, CancellationToken ct);
        Task ProcessAutoAdvancementsAsync(int tournamentId, CancellationToken ct);
    }
}
