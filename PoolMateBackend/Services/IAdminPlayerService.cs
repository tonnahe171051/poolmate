using PoolMate.Api.Common;
using PoolMate.Api.Dtos.Admin.Player;
using PoolMate.Api.Dtos.Auth;
using PoolMate.Api.Dtos.Player;

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
    

    /// Get danh sách Players chưa claim (chưa link với User) với pagination

    Task<PagingList<UnclaimedPlayerDto>> GetUnclaimedPlayersAsync(PlayerFilterDto filter, CancellationToken ct = default);
    

    /// Link Player với ApplicationUser

    Task<bool> LinkPlayerToUserAsync(int playerId, string userId, CancellationToken ct = default);
    

    /// Unlink Player khỏi ApplicationUser

    Task<bool> UnlinkPlayerFromUserAsync(int playerId, CancellationToken ct = default);
    

    /// Get tất cả Players của 1 User

    Task<List<PlayerListDto>> GetPlayersByUserIdAsync(string userId, CancellationToken ct = default);
    

    /// Check Player đã link với User nào chưa

    Task<UserInfoDto?> GetLinkedUserAsync(int playerId, CancellationToken ct = default);
    

    /// Bulk link multiple players to users

    Task<BulkOperationResultDto> BulkLinkPlayersAsync(BulkLinkPlayersDto request, CancellationToken ct = default);
    

    /// Bulk unlink multiple players from users

    Task<BulkOperationResultDto> BulkUnlinkPlayersAsync(BulkUnlinkPlayersDto request, CancellationToken ct = default);
    
    // ===== User Self-Claim APIs =====
    /// User tự claim Player profile (validate email match)

    Task<ClaimPlayerResponse?> ClaimPlayerAsync(int playerId, string userId, bool updateUserProfile = false, CancellationToken ct = default);
    

    /// Get danh sách Players mà user có thể claim (based on email)

    Task<List<ClaimablePlayerDto>> GetClaimablePlayersAsync(string userEmail, CancellationToken ct = default);
    

    /// Get danh sách Players mà user đã claim

    Task<List<PlayerListDto>> GetMyPlayersAsync(string userId, CancellationToken ct = default);

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
