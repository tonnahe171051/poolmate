using PoolMate.Api.Common;
using PoolMate.Api.Dtos.Dashboard;
using PoolMate.Api.Models;

namespace PoolMate.Api.Services;

public interface IOrganizerDashboardService
{
    Task<OrganizerDashboardStatsDto> GetStatsAsync(string userId, CancellationToken ct = default);
    
    Task<PagingList<OrganizerPlayerListDto>> GetOrganizerPlayersAsync(
        string ownerUserId, 
        string? search, 
        int pageIndex, 
        int pageSize, 
        CancellationToken ct = default);
    
    Task<PagingList<OrganizerPlayerDto>> GetMyPlayersAsync(
        string userId, 
        int? tournamentId, 
        string? search, 
        int pageIndex, 
        int pageSize, 
        CancellationToken ct = default);
    
    Task<PagingList<OrganizerTournamentDto>> GetMyTournamentsAsync(
        string userId, 
        string? search, 
        TournamentStatus? status, 
        int pageIndex, 
        int pageSize, 
        CancellationToken ct = default);
    
    Task<TournamentOverviewDto?> GetTournamentOverviewAsync(
        int tournamentId, 
        string userId, 
        CancellationToken ct = default);
}