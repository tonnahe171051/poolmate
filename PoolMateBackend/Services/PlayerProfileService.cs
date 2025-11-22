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
        string userId,
        ApplicationUser user, 
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(userId)) return null;
        var exists = await _db.Players.AsNoTracking().AnyAsync(p => p.UserId == userId, ct);
        if (exists)
        {
            throw new InvalidOperationException("User already has a player profile.");
        }
        
        string fullNameMap = $"{user.FirstName} {user.LastName}".Trim();
        if (string.IsNullOrWhiteSpace(fullNameMap)) 
        {
            fullNameMap = user.UserName ?? "Unknown Player"; 
        }
        var newPlayer = new Player
        {
            UserId = userId,
            CreatedAt = DateTime.UtcNow,
            Email = user.Email,
            FullName = fullNameMap,
            Nickname = user.Nickname,     
            Phone = user.PhoneNumber,     
            Country = user.Country,       
            City = user.City,             
            SkillLevel = null 
        };

        _db.Players.Add(newPlayer);
        await _db.SaveChangesAsync(ct);
        return new CreatePlayerProfileResponseDto
        {
            FullName = newPlayer.FullName,
            Nickname = newPlayer.Nickname,
            Email = newPlayer.Email,
            Phone = newPlayer.Phone,
            Country = newPlayer.Country,
            City = newPlayer.City,
            CreatedAt = newPlayer.CreatedAt,
            Message = "Player profile created automatically from account info"
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