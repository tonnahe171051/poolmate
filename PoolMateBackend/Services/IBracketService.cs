using PoolMate.Api.Dtos.Tournament;

namespace PoolMate.Api.Services
{
    public interface IBracketService
    {
        Task<BracketPreviewDto> PreviewAsync(int tournamentId, CancellationToken ct);
        Task CreateAsync(int tournamentId, CreateBracketRequest? request, CancellationToken ct);
        Task<BracketDto> GetAsync(int tournamentId, CancellationToken ct);
        Task<BracketDto> GetFilteredAsync(int tournamentId, BracketFilterRequest filter, CancellationToken ct);
        Task<MatchDto> UpdateMatchAsync(int matchId, UpdateMatchRequest request, CancellationToken ct);
        Task<MatchDto> CorrectMatchResultAsync(int matchId, CorrectMatchResultRequest request, CancellationToken ct);

    }
}




