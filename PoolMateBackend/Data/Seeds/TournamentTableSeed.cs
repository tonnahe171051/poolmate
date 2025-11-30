using Microsoft.EntityFrameworkCore;
using PoolMate.Api.Models;

namespace PoolMate.Api.Data.Seeds
{
    /// <summary>
    /// Seed data cho TournamentTable (bàn chơi trong giải)
    /// </summary>
    public static class TournamentTableSeed
    {
        public static async Task SeedAsync(ApplicationDbContext context)
        {
            // Kiểm tra nếu đã có table thì không seed nữa
            if (await context.TournamentTables.AnyAsync())
            {
                return;
            }

            // Lấy tournaments
            var tournaments = await context.Tournaments.ToListAsync();
            if (!tournaments.Any())
            {
                return; // Phải seed Tournament trước
            }

            var tables = new List<TournamentTable>();

            // Tạo tables cho từng tournament
            foreach (var tournament in tournaments)
            {
                // Tournament lớn có nhiều bàn hơn
                int tableCount = tournament.BracketSizeEstimate switch
                {
                    <= 8 => 2,
                    <= 16 => 4,
                    <= 32 => 6,
                    _ => 8
                };

                for (int i = 1; i <= tableCount; i++)
                {
                    var table = new TournamentTable
                    {
                        TournamentId = tournament.Id,
                        Label = $"Table {i}",
                        Manufacturer = GetRandomManufacturer(),
                        SizeFoot = GetRandomTableSize(),
                        Status = TableStatus.Open,
                        IsStreaming = i == 1 // Chỉ bàn 1 có streaming
                    };

                    if (table.IsStreaming)
                    {
                        table.LiveStreamUrl = $"https://youtube.com/live/tournament-{tournament.Id}-table-{i}";
                    }

                    tables.Add(table);
                }
            }

            await context.TournamentTables.AddRangeAsync(tables);
            await context.SaveChangesAsync();
        }

        private static string GetRandomManufacturer()
        {
            var manufacturers = new[] { "Diamond", "Brunswick", "Olhausen", "Predator", "Valley" };
            return manufacturers[Random.Shared.Next(manufacturers.Length)];
        }

        private static decimal GetRandomTableSize()
        {
            var sizes = new[] { 7.0m, 8.0m, 9.0m };
            return sizes[Random.Shared.Next(sizes.Length)];
        }
    }
}

