using PoolMate.Api.Common;
using PoolMate.Api.Dtos.Admin.Player;
using PoolMate.Api.Dtos.Auth;

namespace PoolMate.Api.Services;

public interface IAdminPlayerService
{
    // ===== Admin APIs =====
    
    /// Get danh sách Players với filter, search, sort và pagination
    Task<PagingList<PlayerListDto>> GetPlayersAsync(PlayerFilterDto filter, CancellationToken ct = default);
    

    /// Get chi tiết Player theo ID
    Task<PlayerDetailDto?> GetPlayerDetailAsync(int playerId, CancellationToken ct = default);
    

    /// Get thống kê tổng quan về Players
    Task<PlayerStatisticsDto> GetPlayerStatisticsAsync(CancellationToken ct = default);

    // ===== Data Quality APIs =====


    /// Get báo cáo chất lượng dữ liệu người chơi
    Task<DataQualityReportDto> GetDataQualityReportAsync(CancellationToken ct = default);


    /// Get danh sách người chơi có vấn đề về dữ liệu
    Task<PlayersWithIssuesDto> GetPlayersWithIssuesAsync(string issueType, CancellationToken ct = default);


    /// Validate dữ liệu người chơi
    Task<ValidationResultDto> ValidatePlayerDataAsync(ValidatePlayerDto request);


    /// Export players to CSV/Excel with filters applied
    Task<Response> ExportPlayersAsync(PlayerFilterDto filter, bool includeTournamentHistory, string format, CancellationToken ct);
}
