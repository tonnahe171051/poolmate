using PoolMate.Api.Dtos.Admin.Users;
using PoolMate.Api.Dtos.Auth;

namespace PoolMate.Api.Services;

public interface IAdminUserService
{
    /// Lấy danh sách users với phân trang và filter
    Task<Response> GetUsersAsync(AdminUserFilterDto filter, CancellationToken ct);
    
    /// Lấy chi tiết 1 user cụ thể
    Task<Response> GetUserDetailAsync(string userId, CancellationToken ct);
    
    /// Deactivate user (lock account vĩnh viễn)
    Task<Response> DeactivateUserAsync(string userId, CancellationToken ct);
    
    /// Reactivate user (unlock account đã bị deactivate)
    Task<Response> ReactivateUserAsync(string userId, CancellationToken ct);
}

