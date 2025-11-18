using PoolMate.Api.Common;
using PoolMate.Api.Dtos.Admin.Player;
using PoolMate.Api.Dtos.Auth;
using PoolMate.Api.Dtos.Player;

namespace PoolMate.Api.Services;

public interface IAdminPlayerService
{
    // ===== Admin APIs =====
    
    /// <summary>
    /// Get danh sách Players với filter, search, sort và pagination
    /// </summary>
    Task<PagingList<PlayerListDto>> GetPlayersAsync(PlayerFilterDto filter, CancellationToken ct = default);
    
    /// <summary>
    /// Get chi tiết Player theo ID
    /// </summary>
    Task<PlayerDetailDto?> GetPlayerDetailAsync(int playerId, CancellationToken ct = default);
    
    /// <summary>
    /// Get thống kê tổng quan về Players
    /// </summary>
    Task<PlayerStatisticsDto> GetPlayerStatisticsAsync(CancellationToken ct = default);
    
    /// <summary>
    /// Get danh sách Players chưa claim (chưa link với User) với pagination
    /// </summary>
    Task<PagingList<UnclaimedPlayerDto>> GetUnclaimedPlayersAsync(PlayerFilterDto filter, CancellationToken ct = default);
    
    /// <summary>
    /// Link Player với ApplicationUser
    /// </summary>
    Task<bool> LinkPlayerToUserAsync(int playerId, string userId, CancellationToken ct = default);
    
    /// <summary>
    /// Unlink Player khỏi ApplicationUser
    /// </summary>
    Task<bool> UnlinkPlayerFromUserAsync(int playerId, CancellationToken ct = default);
    
    /// <summary>
    /// Get tất cả Players của 1 User
    /// </summary>
    Task<List<PlayerListDto>> GetPlayersByUserIdAsync(string userId, CancellationToken ct = default);
    
    /// <summary>
    /// Check Player đã link với User nào chưa
    /// </summary>
    Task<UserInfoDto?> GetLinkedUserAsync(int playerId, CancellationToken ct = default);
    
    /// <summary>
    /// Bulk link multiple players to users
    /// </summary>
    Task<BulkOperationResultDto> BulkLinkPlayersAsync(BulkLinkPlayersDto request, CancellationToken ct = default);
    
    /// <summary>
    /// Bulk unlink multiple players from users
    /// </summary>
    Task<BulkOperationResultDto> BulkUnlinkPlayersAsync(BulkUnlinkPlayersDto request, CancellationToken ct = default);
    
    // ===== User Self-Claim APIs =====
    
    /// <summary>
    /// User tự claim Player profile (validate email match)
    /// </summary>
    Task<ClaimPlayerResponse?> ClaimPlayerAsync(int playerId, string userId, bool updateUserProfile = false, CancellationToken ct = default);
    
    /// <summary>
    /// Get danh sách Players mà user có thể claim (based on email)
    /// </summary>
    Task<List<ClaimablePlayerDto>> GetClaimablePlayersAsync(string userEmail, CancellationToken ct = default);
    
    /// <summary>
    /// Get danh sách Players mà user đã claim
    /// </summary>
    Task<List<PlayerListDto>> GetMyPlayersAsync(string userId, CancellationToken ct = default);

    // ===== Data Quality APIs =====

    /// <summary>
    /// Get báo cáo chất lượng dữ liệu người chơi
    /// </summary>
    Task<DataQualityReportDto> GetDataQualityReportAsync(CancellationToken ct = default);

    /// <summary>
    /// Get danh sách người chơi có vấn đề về dữ liệu
    /// </summary>
    Task<PlayersWithIssuesDto> GetPlayersWithIssuesAsync(string issueType, CancellationToken ct = default);

    /// <summary>
    /// Validate dữ liệu người chơi
    /// </summary>
    Task<ValidationResultDto> ValidatePlayerDataAsync(ValidatePlayerDto request);

    /// <summary>
    /// Export players to CSV/Excel with filters applied
    /// </summary>
    Task<Response> ExportPlayersAsync(PlayerFilterDto filter, bool includeTournamentHistory, string format, CancellationToken ct);
}
