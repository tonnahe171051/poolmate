using PoolMate.Api.Dtos.Admin.Users;
using PoolMate.Api.Dtos.Auth;

namespace PoolMate.Api.Services;

public interface IAdminUserService
{
    /// Lấy danh sách users với phân trang và filter
    Task<Response> GetUsersAsync(AdminUserFilterDto filter, CancellationToken ct);
    
    /// Lấy chi tiết 1 user cụ thể với full context
    Task<Response> GetUserDetailAsync(string userId, CancellationToken ct);
    
    /// Lấy thống kê tổng quan về users
    Task<Response> GetUserStatisticsAsync(CancellationToken ct);
    
    /// Lấy activity log của 1 user
    Task<Response> GetUserActivityLogAsync(string userId, CancellationToken ct);
    
    /// Deactivate user (lock account vĩnh viễn)
    Task<Response> DeactivateUserAsync(string userId, CancellationToken ct);
    
    /// Reactivate user (unlock account đã bị deactivate)
    Task<Response> ReactivateUserAsync(string userId, CancellationToken ct);
    
    /// Bulk deactivate multiple users
    Task<Response> BulkDeactivateUsersAsync(BulkDeactivateUsersDto request, CancellationToken ct);
    
    /// Bulk reactivate multiple users
    Task<Response> BulkReactivateUsersAsync(BulkReactivateUsersDto request, CancellationToken ct);
    
    /// Export users to CSV
    Task<Response> ExportUsersAsync(AdminUserFilterDto filter, CancellationToken ct);
}

