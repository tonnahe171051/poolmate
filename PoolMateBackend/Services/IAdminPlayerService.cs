using PoolMate.Api.Common;
using PoolMate.Api.Dtos.Admin.Player;
using PoolMate.Api.Dtos.Auth;

namespace PoolMate.Api.Services;

public interface IAdminPlayerService
{

    Task<PagingList<PlayerListDto>> GetPlayersAsync(PlayerFilterDto filter, CancellationToken ct = default);
    Task<PlayerDetailDto?> GetPlayerDetailAsync(int playerId, CancellationToken ct = default);
    Task<PlayerStatisticsDto> GetPlayerStatisticsAsync(CancellationToken ct = default);
    Task<DataQualityReportDto> GetDataQualityReportAsync(CancellationToken ct = default);
    Task<PlayersWithIssuesDto> GetPlayersWithIssuesAsync(
        string issueType,
        int pageIndex = 1,
        int pageSize = 20,
        CancellationToken ct = default);
    Task<ValidationResultDto> ValidatePlayerDataAsync(ValidatePlayerDto request);
    Task<Response> ExportPlayersAsync(PlayerFilterDto filter, bool includeTournamentHistory, string format,
        CancellationToken ct);
    Task<Response> MergePlayersAsync(MergePlayerRequestDto request, CancellationToken ct = default);
}