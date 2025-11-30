using Microsoft.EntityFrameworkCore;
using PoolMate.Api.Models;

namespace PoolMate.Api.Data.Seeds
{
    /// <summary>
    /// Seed data cho TournamentPlayer (người chơi đăng ký giải)
    /// </summary>
    public static class TournamentPlayerSeed
    {
        public static async Task SeedAsync(ApplicationDbContext context)
        {
            // Kiểm tra nếu đã có tournament player thì không seed nữa
            if (await context.TournamentPlayers.AnyAsync())
            {
                return;
            }

            // Lấy tournaments
            var tournaments = await context.Tournaments
                .OrderBy(t => t.Id)
                .ToListAsync();
            
            if (!tournaments.Any())
            {
                return; // Phải seed Tournament trước
            }

            // Lấy players
            var players = await context.Players
                .OrderBy(p => p.Id)
                .ToListAsync();
            
            if (!players.Any())
            {
                return; // Phải seed Player trước
            }

            var tournamentPlayers = new List<TournamentPlayer>();

            foreach (var tournament in tournaments)
            {
                // Số lượng players cho mỗi tournament
                int playerCount = tournament.BracketSizeEstimate ?? 8;
                playerCount = Math.Min(playerCount, players.Count); // Không vượt quá số player có sẵn

                // Lấy random players cho tournament này
                var selectedPlayers = players
                    .OrderBy(_ => Random.Shared.Next())
                    .Take(playerCount)
                    .ToList();

                for (int i = 0; i < selectedPlayers.Count; i++)
                {
                    var player = selectedPlayers[i];
                    
                    // Xác định status dựa trên tournament status
                    var status = tournament.Status switch
                    {
                        TournamentStatus.Upcoming => i < playerCount / 2 
                            ? TournamentPlayerStatus.Confirmed 
                            : TournamentPlayerStatus.Unconfirmed,
                        _ => TournamentPlayerStatus.Confirmed // InProgress hoặc Completed thì đều confirmed
                    };

                    var tournamentPlayer = new TournamentPlayer
                    {
                        TournamentId = tournament.Id,
                        PlayerId = player.Id,
                        Seed = tournament.BracketOrdering == BracketOrdering.Seeded ? i + 1 : null,
                        Status = status,
                        
                        // Snapshot data từ Player
                        DisplayName = player.FullName,
                        Nickname = player.Nickname,
                        Email = player.Email,
                        Phone = player.Phone,
                        Country = player.Country,
                        City = player.City,
                        SkillLevel = player.SkillLevel
                    };

                    tournamentPlayers.Add(tournamentPlayer);
                }
            }

            await context.TournamentPlayers.AddRangeAsync(tournamentPlayers);
            await context.SaveChangesAsync();
        }
    }
}

