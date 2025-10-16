using PoolMate.Api.Dtos.Tournament;

namespace PoolMate.Api.Services
{
    public interface IBracketService
    {
        Task<BracketPreviewDto> PreviewAsync(int tournamentId, CancellationToken ct);
        Task CreateAsync(int tournamentId, CancellationToken ct);
    }
}
