using PoolMate.Api.Dtos.Admin.Users;
using PoolMate.Api.Dtos.Auth;

namespace PoolMate.Api.Services;

public interface IAdminUserService
{
    /// Lấy danh sách users với phân trang và filter
    Task<Response> GetUsersAsync(AdminUserFilterDto filter, CancellationToken ct);
    
    /// Lấy chi tiết 1 user cụ thể
    Task<Response> GetUserDetailAsync(string userId, CancellationToken ct);
    
    /// Xóa user 
    Task<Response> DeleteUserAsync(string userId, CancellationToken ct);
}

