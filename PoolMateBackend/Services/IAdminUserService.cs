using PoolMate.Api.Dtos.Admin.Users;
using PoolMate.Api.Dtos.Auth;

namespace PoolMate.Api.Services;

public interface IAdminUserService
{

    Task<Response> GetUsersAsync(AdminUserFilterDto filter, CancellationToken ct);
    Task<Response> GetUserDetailAsync(string userId, CancellationToken ct);
    Task<Response> GetUserStatisticsAsync(CancellationToken ct);
    Task<Response> GetUserActivityLogAsync(string userId, CancellationToken ct); 
    Task<Response> DeactivateUserAsync(string userId, CancellationToken ct);
    Task<Response> ReactivateUserAsync(string userId, CancellationToken ct);
    Task<Response> BulkDeactivateUsersAsync(BulkDeactivateUsersDto request, CancellationToken ct);
    Task<Response> BulkReactivateUsersAsync(BulkReactivateUsersDto request, CancellationToken ct);
    Task<Response> ExportUsersAsync(AdminUserFilterDto filter, CancellationToken ct);

}

