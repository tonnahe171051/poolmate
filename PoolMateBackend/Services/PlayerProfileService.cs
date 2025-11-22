using Microsoft.EntityFrameworkCore;
using PoolMate.Api.Data;
using PoolMate.Api.Dtos.PlayerProfile;
using PoolMate.Api.Models;

namespace PoolMate.Api.Services;

public class PlayerProfileService : IPlayerProfileService
{
    private readonly ApplicationDbContext _db;

    public PlayerProfileService(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<CreatePlayerProfileResponseDto?> CreatePlayerProfileAsync(
        CreatePlayerProfileDto dto,
        string userId,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(userId)) return null;
        var exists = await _db.Players.AsNoTracking().AnyAsync(p => p.UserId == userId, ct);
        if (exists)
        {
            throw new InvalidOperationException("User already has a player profile.");
        }
        var newPlayer = new Player
        {
            UserId = userId,
            CreatedAt = DateTime.UtcNow,
            FullName = dto.FullName.Trim(),
            Nickname = string.IsNullOrWhiteSpace(dto.Nickname) ? null : dto.Nickname.Trim(),
            Email = string.IsNullOrWhiteSpace(dto.Email) ? null : dto.Email.Trim(),
            Phone = string.IsNullOrWhiteSpace(dto.Phone) ? null : dto.Phone.Trim(),
            Country = string.IsNullOrWhiteSpace(dto.Country) ? null : dto.Country.Trim().ToUpper(),
            City = string.IsNullOrWhiteSpace(dto.City) ? null : dto.City.Trim(),
            SkillLevel = dto.SkillLevel
        };

        _db.Players.Add(newPlayer);
        await _db.SaveChangesAsync(ct);
        return new CreatePlayerProfileResponseDto
        {
            PlayerId = newPlayer.Id,
            FullName = newPlayer.FullName,
            Email = newPlayer.Email,
            CreatedAt = newPlayer.CreatedAt,
            Message = "Player profile created successfully"
        };
    }

    public async Task<List<PlayerProfileDetailDto>> GetMyPlayerProfilesAsync(
        string userId,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            return new List<PlayerProfileDetailDto>();
        }

        var profiles = await _db.Players
            .AsNoTracking()
            .Where(p => p.UserId == userId)
            .OrderByDescending(p => p.CreatedAt)
            .Select(p => new PlayerProfileDetailDto
            {
                Id = p.Id,
                FullName = p.FullName,
                Nickname = p.Nickname,
                Email = p.Email,
                Phone = p.Phone,
                Country = p.Country,
                City = p.City,
                SkillLevel = p.SkillLevel,
                CreatedAt = p.CreatedAt
            })
            .ToListAsync(ct);

        return profiles;
    }

}