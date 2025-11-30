using PoolMate.Api.Dtos.Dashboard;

namespace PoolMate.Api.Services;

public interface IOrganizerDashboardService
{
    Task<OrganizerDashboardStatsDto> GetStatsAsync(string userId, CancellationToken ct = default);
    Task<List<OrganizerActivityDto>> GetRecentActivitiesAsync(string userId, int limit, CancellationToken ct = default);
}