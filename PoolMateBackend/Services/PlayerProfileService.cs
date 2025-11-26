using Microsoft.EntityFrameworkCore;
using PoolMate.Api.Common;
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

    public async Task<PagingList<MatchHistoryDto>> GetMatchHistoryAsync(
        int playerId,
        int pageIndex = 1,
        int pageSize = 20,
        CancellationToken ct = default)
    {
        // 1. Query trực tiếp vào bảng Match (Không cần lấy list ID trước)
        var query = _db.Matches
            .AsNoTracking()
            .Where(m => m.Status == MatchStatus.Completed &&
                        ((m.Player1Tp != null && m.Player1Tp.PlayerId == playerId) ||
                         (m.Player2Tp != null && m.Player2Tp.PlayerId == playerId)));

        var totalCount = await query.CountAsync(ct);

        // 2. Projection (Tính toán ngay trong SQL)
        var items = await query
            .OrderByDescending(m => m.Tournament.StartUtc)
            .ThenByDescending(m => m.RoundNo)
            .Skip((pageIndex - 1) * pageSize)
            .Take(pageSize)
            .Select(m => new
            {
                Match = m,
                IsP1 = m.Player1Tp != null && m.Player1Tp.PlayerId == playerId
            })
            .Select(x => new MatchHistoryDto
            {
                MatchId = x.Match.Id,
                TournamentId = x.Match.TournamentId,
                TournamentName = x.Match.Tournament.Name,
                TournamentDate = x.Match.Tournament.StartUtc,
                GameType = x.Match.Tournament.GameType.ToString(),

                StageType = x.Match.Stage.Type.ToString(),
                BracketSide = x.Match.Bracket.ToString(),
                RoundName = "Round " + x.Match.RoundNo,
                OpponentName = x.IsP1
                    ? (x.Match.Player2Tp != null ? x.Match.Player2Tp.DisplayName : "Bye")
                    : (x.Match.Player1Tp != null ? x.Match.Player1Tp.DisplayName : "Bye"),

                // Logic tính điểm
                Score = x.IsP1
                    ? (x.Match.ScoreP1 ?? 0) + " - " + (x.Match.ScoreP2 ?? 0)
                    : (x.Match.ScoreP2 ?? 0) + " - " + (x.Match.ScoreP1 ?? 0),

                RaceTo = x.Match.RaceTo ?? 0,

                // Logic thắng thua
                Result = x.Match.WinnerTpId == (x.IsP1 ? x.Match.Player1TpId : x.Match.Player2TpId)
                    ? "Win"
                    : "Loss",

                MatchDate = x.Match.ScheduledUtc
            })
            .ToListAsync(ct);

        return PagingList<MatchHistoryDto>.Create(items, totalCount, pageIndex, pageSize);
    }
}