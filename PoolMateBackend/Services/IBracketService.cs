using PoolMate.Api.Dtos.Tournament;

namespace PoolMate.Api.Services
{
    public interface IBracketService
    {
        Task<BracketPreviewDto> PreviewAsync(int tournamentId, CancellationToken ct);
        Task CreateAsync(int tournamentId, CreateBracketRequest? request, CancellationToken ct);
        Task<BracketDto> GetAsync(int tournamentId, CancellationToken ct);
        Task<BracketDto> GetFilteredAsync(int tournamentId, BracketFilterRequest filter, CancellationToken ct);
        Task<MatchDto> GetMatchAsync(int matchId, CancellationToken ct);
        Task<MatchDto> UpdateMatchAsync(int matchId, UpdateMatchRequest request, CancellationToken ct);
        Task<MatchDto> CorrectMatchResultAsync(int matchId, CorrectMatchResultRequest request, CancellationToken ct);
        Task<StageCompletionResultDto> CompleteStageAsync(int tournamentId, int stageNo, CompleteStageRequest request, CancellationToken ct);
        Task<IReadOnlyList<TournamentPlayerStatsDto>> GetPlayerStatsAsync(int tournamentId, CancellationToken ct);
        Task<IReadOnlyList<string>> GetBracketDebugViewAsync(int tournamentId, CancellationToken ct);
        Task<TournamentStatusSummaryDto> GetTournamentStatusAsync(int tournamentId, CancellationToken ct);
        Task<MatchScoreUpdateResponse> UpdateLiveScoreAsync(int matchId, UpdateLiveScoreRequest request, ScoringContext actor, CancellationToken ct);
        Task<MatchScoreUpdateResponse> CompleteMatchAsync(int matchId, CompleteMatchRequest request, ScoringContext actor, CancellationToken ct);
        Task ResetAsync(int tournamentId, CancellationToken ct);
        Task ForceCompleteMatchAsync(int matchId, CancellationToken ct);

    }
}




