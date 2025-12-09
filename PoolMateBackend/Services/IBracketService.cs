﻿using PoolMate.Api.Dtos.Tournament;

namespace PoolMate.Api.Services
{
    public interface IBracketService
    {
        Task<BracketPreviewDto> PreviewAsync(int tournamentId, CancellationToken ct);
        Task<StageDto> PreviewStageAsync(int tournamentId, int stageNo, CancellationToken ct);
        Task CreateAsync(int tournamentId, CreateBracketRequest? request, CancellationToken ct);
        Task<BracketDto> GetAsync(int tournamentId, CancellationToken ct);
        Task<BracketDto> GetFilteredAsync(int tournamentId, BracketFilterRequest filter, CancellationToken ct);
        Task<BracketDto> GetWinnersSideAsync(int tournamentId, CancellationToken ct);
        Task<BracketDto> GetLosersSideAsync(int tournamentId, CancellationToken ct);
        Task<StageCompletionResultDto> CompleteStageAsync(int tournamentId, int stageNo, CompleteStageRequest request, CancellationToken ct);
        Task<IReadOnlyList<TournamentPlayerStatsDto>> GetPlayerStatsAsync(int tournamentId, CancellationToken ct);
        Task<IReadOnlyList<string>> GetBracketDebugViewAsync(int tournamentId, CancellationToken ct);
        Task<TournamentStatusSummaryDto> GetTournamentStatusAsync(int tournamentId, CancellationToken ct);
        Task ResetAsync(int tournamentId, CancellationToken ct);

    }
}




