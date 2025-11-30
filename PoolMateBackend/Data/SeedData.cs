using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using PoolMate.Api.Data.Seeds;
using PoolMate.Api.Models;

namespace PoolMate.Api.Data
{
    /// <summary>
    /// Master seed data orchestrator - Gọi các seed riêng biệt theo thứ tự
    /// </summary>
    public static class SeedData
    {
        /// <summary>
        /// Seed chỉ Users và Roles (nhanh, dùng cho testing)
        /// </summary>
        public static async Task SeedUsersAsync(IServiceProvider serviceProvider)
        {
            await UserSeed.SeedAsync(serviceProvider);
        }

        /// <summary>
        /// Seed tất cả dữ liệu theo thứ tự phụ thuộc
        /// </summary>
        public static async Task SeedAllDataAsync(IServiceProvider serviceProvider)
        {
            using var scope = serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

            // Đảm bảo database đã được tạo
            await context.Database.MigrateAsync();

            // 1. Seed Users và Roles (phải đầu tiên vì các bảng khác phụ thuộc vào User)
            await UserSeed.SeedAsync(serviceProvider);

            // 2. Seed Venues (phụ thuộc User)
            await VenueSeed.SeedAsync(context, userManager);

            // 3. Seed Players (phụ thuộc User)
            await PlayerSeed.SeedAsync(context, userManager);

            // 4. Seed PayoutTemplates (độc lập)
            await PayoutTemplateSeed.SeedAsync(context);

            // 5. Seed Posts (phụ thuộc User)
            await PostSeed.SeedAsync(context, userManager);

            // 6. Seed Tournaments (phụ thuộc User, Venue, PayoutTemplate)
            await TournamentSeed.SeedAsync(context, userManager);

            // 7. Seed TournamentTables (phụ thuộc Tournament)
            await TournamentTableSeed.SeedAsync(context);

            // 8. Seed TournamentPlayers (phụ thuộc Tournament, Player)
            await TournamentPlayerSeed.SeedAsync(context);

            // 9. Seed TournamentStages (phụ thuộc Tournament)
            await TournamentStageSeed.SeedAsync(context);

            // 10. Seed Matches (phụ thuộc TournamentStage, TournamentPlayer, TournamentTable)
            await MatchSeed.SeedAsync(context);
        }

        /// <summary>
        /// Seed từng phần riêng biệt (cho phép seed từng bảng một)
        /// </summary>
        public static async Task SeedVenuesOnlyAsync(IServiceProvider serviceProvider)
        {
            using var scope = serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            await VenueSeed.SeedAsync(context, userManager);
        }

        public static async Task SeedPlayersOnlyAsync(IServiceProvider serviceProvider)
        {
            using var scope = serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            await PlayerSeed.SeedAsync(context, userManager);
        }

        public static async Task SeedPayoutTemplatesOnlyAsync(IServiceProvider serviceProvider)
        {
            using var scope = serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            await PayoutTemplateSeed.SeedAsync(context);
        }

        public static async Task SeedPostsOnlyAsync(IServiceProvider serviceProvider)
        {
            using var scope = serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            await PostSeed.SeedAsync(context, userManager);
        }

        public static async Task SeedTournamentsOnlyAsync(IServiceProvider serviceProvider)
        {
            using var scope = serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            await TournamentSeed.SeedAsync(context, userManager);
        }

        public static async Task SeedTournamentTablesOnlyAsync(IServiceProvider serviceProvider)
        {
            using var scope = serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            await TournamentTableSeed.SeedAsync(context);
        }

        public static async Task SeedTournamentPlayersOnlyAsync(IServiceProvider serviceProvider)
        {
            using var scope = serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            await TournamentPlayerSeed.SeedAsync(context);
        }

        public static async Task SeedTournamentStagesOnlyAsync(IServiceProvider serviceProvider)
        {
            using var scope = serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            await TournamentStageSeed.SeedAsync(context);
        }

        public static async Task SeedMatchesOnlyAsync(IServiceProvider serviceProvider)
        {
            using var scope = serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            await MatchSeed.SeedAsync(context);
        }
    }
}

