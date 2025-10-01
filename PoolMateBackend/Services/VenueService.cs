using Microsoft.EntityFrameworkCore;
using PoolMate.Api.Data;
using PoolMate.Api.Dtos.Venue;
using PoolMate.Api.Models;

namespace PoolMate.Api.Services;

public class VenueService : IVenueService
{
    private readonly ApplicationDbContext _db;
    public VenueService(ApplicationDbContext db) => _db = db;

    public async Task<IEnumerable<object>> SearchAsync(string? query, string? city, string? country, int take, CancellationToken ct)
    {
        var q = _db.Venues.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(query)) q = q.Where(v => v.Name.Contains(query));
        if (!string.IsNullOrWhiteSpace(city)) q = q.Where(v => v.City == city);
        if (!string.IsNullOrWhiteSpace(country)) q = q.Where(v => v.Country == country);

        return await q.OrderBy(v => v.Name)
                      .Take(Math.Clamp(take, 1, 50))
                      .Select(v => new { v.Id, v.Name, v.Address, v.City, v.Country })
                      .ToListAsync(ct);
    }

    public async Task<int?> CreateAsync(string? userId, CreateVenueRequest m, CancellationToken ct)
    {
        var v = new Venue
        {
            Name = m.Name.Trim(),
            Address = string.IsNullOrWhiteSpace(m.Address) ? null : m.Address.Trim(),
            City = string.IsNullOrWhiteSpace(m.City) ? null : m.City.Trim(),
            Country = string.IsNullOrWhiteSpace(m.Country) ? null : m.Country.Trim().ToUpperInvariant(),
            CreatedByUserId = userId,
            CreatedAt = DateTime.UtcNow
        };
        _db.Venues.Add(v);
        await _db.SaveChangesAsync(ct);
        return v.Id;
    }

    public Task<Venue?> GetAsync(int id, CancellationToken ct)
        => _db.Venues.FirstOrDefaultAsync(x => x.Id == id, ct);
}
