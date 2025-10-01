using PoolMate.Api.Dtos.Venue;
using PoolMate.Api.Models;

namespace PoolMate.Api.Services
{
    public interface IVenueService
    {
        Task<IEnumerable<object>> SearchAsync(string? query, string? city, string? country, int take, CancellationToken ct);
        Task<int?> CreateAsync(string? userId, CreateVenueRequest m, CancellationToken ct);
        Task<Venue?> GetAsync(int id, CancellationToken ct);
    }
}
