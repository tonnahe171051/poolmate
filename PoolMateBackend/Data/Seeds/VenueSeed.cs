using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using PoolMate.Api.Models;

namespace PoolMate.Api.Data.Seeds
{
    /// <summary>
    /// Seed data cho Venue
    /// </summary>
    public static class VenueSeed
    {
        public static async Task SeedAsync(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            // Kiểm tra nếu đã có venue thì không seed nữa
            if (await context.Venues.AnyAsync())
            {
                return;
            }

            // Lấy organizer để làm CreatedBy
            var organizer = await userManager.FindByEmailAsync("john.organizer@poolmate.com");
            if (organizer == null)
            {
                return; // Phải seed User trước
            }

            var venues = new[]
            {
                new Venue
                {
                    Name = "Billiard Club Saigon",
                    Address = "123 Nguyen Hue Street",
                    City = "Ho Chi Minh",
                    Country = "VN",
                    CreatedByUserId = organizer.Id,
                    CreatedAt = DateTime.UtcNow.AddDays(-100)
                },
                new Venue
                {
                    Name = "Hanoi Pool Arena",
                    Address = "456 Ba Trieu Street",
                    City = "Hanoi",
                    Country = "VN",
                    CreatedByUserId = organizer.Id,
                    CreatedAt = DateTime.UtcNow.AddDays(-90)
                },
                new Venue
                {
                    Name = "Da Nang Billiard Center",
                    Address = "789 Tran Phu Street",
                    City = "Da Nang",
                    Country = "VN",
                    CreatedByUserId = organizer.Id,
                    CreatedAt = DateTime.UtcNow.AddDays(-80)
                },
                new Venue
                {
                    Name = "Can Tho Pool House",
                    Address = "321 Mau Than Street",
                    City = "Can Tho",
                    Country = "VN",
                    CreatedByUserId = organizer.Id,
                    CreatedAt = DateTime.UtcNow.AddDays(-70)
                },
                new Venue
                {
                    Name = "Nha Trang Billiard Palace",
                    Address = "654 Tran Phu Boulevard",
                    City = "Nha Trang",
                    Country = "VN",
                    CreatedByUserId = organizer.Id,
                    CreatedAt = DateTime.UtcNow.AddDays(-60)
                }
            };

            await context.Venues.AddRangeAsync(venues);
            await context.SaveChangesAsync();
        }
    }
}

