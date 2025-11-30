using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using PoolMate.Api.Models;

namespace PoolMate.Api.Data.Seeds
{
    /// <summary>
    /// Seed data cho Post (bài đăng trên hệ thống)
    /// </summary>
    public static class PostSeed
    {
        public static async Task SeedAsync(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            // Kiểm tra nếu đã có post thì không seed nữa
            if (await context.Posts.AnyAsync())
            {
                return;
            }

            // Lấy các user để làm tác giả
            var users = await userManager.Users.Take(5).ToListAsync();
            if (!users.Any())
            {
                return; // Phải seed User trước
            }

            var posts = new List<Post>();
            var postContents = new[]
            {
                "🎱 Just finished an amazing tournament at Billiard Club Saigon! Thanks to all participants!",
                "Looking forward to the upcoming 9-ball championship next week. Who's ready? 💪",
                "Great match today! Practice makes perfect. See you all at the next event!",
                "🏆 Congratulations to all winners from yesterday's tournament. Amazing performances!",
                "Pool tip of the day: Focus on your stance and grip. Consistency is key!",
                "Excited to announce a new tournament series starting next month! Stay tuned for details.",
                "Thank you to our sponsors for making these events possible! 🙏",
                "Another successful event at Hanoi Pool Arena. The community is growing strong!",
                "Who's your favorite pool player? Drop their name in the comments! 🎯",
                "Remember: It's not about the equipment, it's about the skill and dedication!"
            };

            for (int i = 0; i < postContents.Length; i++)
            {
                var user = users[i % users.Count];
                var post = new Post
                {
                    Id = Guid.NewGuid(),
                    Content = postContents[i],
                    UserId = user.Id,
                    CreatedAt = DateTime.UtcNow.AddDays(-Random.Shared.Next(1, 30)),
                    UpdatedAt = DateTime.UtcNow.AddDays(-Random.Shared.Next(1, 30)),
                    IsActive = true
                };
                posts.Add(post);
            }

            await context.Posts.AddRangeAsync(posts);
            await context.SaveChangesAsync();
        }
    }
}

