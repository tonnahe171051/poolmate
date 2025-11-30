using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using PoolMate.Api.Common;
using PoolMate.Api.Models;

namespace PoolMate.Api.Data.Seeds
{
    /// <summary>
    /// Seed data cho Player (profile của người chơi)
    /// QUAN TRỌNG: 1 User chỉ có 1 Player
    /// </summary>
    public static class PlayerSeed
    {
        public static async Task SeedAsync(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            // Kiểm tra nếu đã có player thì không seed nữa
            if (await context.Players.AnyAsync())
            {
                return;
            }

            // Lấy các user có role Player để tạo Player profile
            var playerUsers = await userManager.GetUsersInRoleAsync("Player");
            if (!playerUsers.Any())
            {
                return; // Phải seed User trước
            }

            // Lấy danh sách UserId đã có Player
            var existingUserIds = await context.Players
                .Where(p => p.UserId != null)
                .Select(p => p.UserId)
                .ToListAsync();

            var players = new List<Player>();
            int skillLevel = 5;

            foreach (var user in playerUsers.Take(10))
            {
                // Kiểm tra user này đã có Player chưa (1 User chỉ 1 Player)
                if (existingUserIds.Contains(user.Id))
                {
                    continue;
                }

                var fullName = $"{user.FirstName} {user.LastName}";
                var player = new Player
                {
                    FullName = fullName,
                    Slug = SlugHelper.GenerateSlug(fullName),
                    Nickname = user.Nickname,
                    Email = user.Email,
                    Phone = user.PhoneNumber,
                    Country = user.Country,
                    City = user.City,
                    UserId = user.Id,
                    SkillLevel = skillLevel,
                    CreatedAt = user.CreatedAt
                };

                players.Add(player);
                
                // Tăng skill level cho đa dạng (5-7)
                skillLevel++;
                if (skillLevel > 7) skillLevel = 5;
            }

            if (players.Any())
            {
                await context.Players.AddRangeAsync(players);
                await context.SaveChangesAsync();
            }
        }
    }
}

