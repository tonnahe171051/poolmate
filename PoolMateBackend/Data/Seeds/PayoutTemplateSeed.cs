using Microsoft.EntityFrameworkCore;
using PoolMate.Api.Models;
using System.Text.Json;

namespace PoolMate.Api.Data.Seeds
{
    /// <summary>
    /// Seed data cho PayoutTemplate (các mẫu chia giải)
    /// </summary>
    public static class PayoutTemplateSeed
    {
        public static async Task SeedAsync(ApplicationDbContext context)
        {
            // Kiểm tra nếu đã có template thì không seed nữa
            if (await context.PayoutTemplates.AnyAsync())
            {
                return;
            }

            var templates = new[]
            {
                // 4-8 players: Top 2
                new PayoutTemplate
                {
                    Name = "Top 2 places (4-8 players)",
                    MinPlayers = 4,
                    MaxPlayers = 8,
                    Places = 2,
                    PercentJson = JsonSerializer.Serialize(new[]
                    {
                        new { rank = 1, percent = 70 },
                        new { rank = 2, percent = 30 }
                    })
                },
                
                // 9-16 players: Top 3
                new PayoutTemplate
                {
                    Name = "Top 3 places (9-16 players)",
                    MinPlayers = 9,
                    MaxPlayers = 16,
                    Places = 3,
                    PercentJson = JsonSerializer.Serialize(new[]
                    {
                        new { rank = 1, percent = 50 },
                        new { rank = 2, percent = 30 },
                        new { rank = 3, percent = 20 }
                    })
                },
                
                // 17-24 players: Top 4
                new PayoutTemplate
                {
                    Name = "Top 4 places (17-24 players)",
                    MinPlayers = 17,
                    MaxPlayers = 24,
                    Places = 4,
                    PercentJson = JsonSerializer.Serialize(new[]
                    {
                        new { rank = 1, percent = 45 },
                        new { rank = 2, percent = 25 },
                        new { rank = 3, percent = 18 },
                        new { rank = 4, percent = 12 }
                    })
                },
                
                // 25-32 players: Top 5
                new PayoutTemplate
                {
                    Name = "Top 5 places (25-32 players)",
                    MinPlayers = 25,
                    MaxPlayers = 32,
                    Places = 5,
                    PercentJson = JsonSerializer.Serialize(new[]
                    {
                        new { rank = 1, percent = 40 },
                        new { rank = 2, percent = 25 },
                        new { rank = 3, percent = 15 },
                        new { rank = 4, percent = 12 },
                        new { rank = 5, percent = 8 }
                    })
                },
                
                // 33-64 players: Top 8
                new PayoutTemplate
                {
                    Name = "Top 8 places (33-64 players)",
                    MinPlayers = 33,
                    MaxPlayers = 64,
                    Places = 8,
                    PercentJson = JsonSerializer.Serialize(new[]
                    {
                        new { rank = 1, percent = 35 },
                        new { rank = 2, percent = 20 },
                        new { rank = 3, percent = 12 },
                        new { rank = 4, percent = 10 },
                        new { rank = 5, percent = 8 },
                        new { rank = 6, percent = 6 },
                        new { rank = 7, percent = 5 },
                        new { rank = 8, percent = 4 }
                    })
                }
            };

            await context.PayoutTemplates.AddRangeAsync(templates);
            await context.SaveChangesAsync();
        }
    }
}

