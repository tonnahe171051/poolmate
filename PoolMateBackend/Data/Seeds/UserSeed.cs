using Microsoft.AspNetCore.Identity;
using PoolMate.Api.Models;

namespace PoolMate.Api.Data.Seeds
{
    public static class UserSeed
    {
        public static async Task SeedAsync(IServiceProvider serviceProvider)
        {
            var userManager = serviceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var roleManager = serviceProvider.GetRequiredService<RoleManager<IdentityRole>>();

            await SeedRolesAsync(roleManager);
            await SeedAdminUserAsync(userManager);
            await SeedOrganizerUsersAsync(userManager);
            await SeedPlayerUsersAsync(userManager);
        }

        private static async Task SeedRolesAsync(RoleManager<IdentityRole> roleManager)
        {
            string[] roleNames = { "Admin", "Organizer", "Player" };
            foreach (var roleName in roleNames)
            {
                if (!await roleManager.RoleExistsAsync(roleName))
                {
                    await roleManager.CreateAsync(new IdentityRole(roleName));
                }
            }
        }

        private static async Task SeedAdminUserAsync(UserManager<ApplicationUser> userManager)
        {
            var adminUser = await userManager.FindByEmailAsync("admin@poolmate.com");
            if (adminUser == null)
            {
                adminUser = new ApplicationUser
                {
                    UserName = "admin@poolmate.com",
                    Email = "admin@poolmate.com",
                    EmailConfirmed = true,
                    FirstName = "Admin",
                    LastName = "System",
                    Nickname = "SuperAdmin",
                    City = "Ho Chi Minh",
                    Country = "VN",
                    PhoneNumber = "+84901234567",
                    CreatedAt = DateTime.UtcNow
                };

                var result = await userManager.CreateAsync(adminUser, "Admin@123456");
                if (result.Succeeded)
                {
                    await userManager.AddToRoleAsync(adminUser, "Admin");
                }
            }
        }

        private static async Task SeedOrganizerUsersAsync(UserManager<ApplicationUser> userManager)
        {
            var organizers = new[]
            {
                new
                {
                    Email = "john.organizer@poolmate.com",
                    FirstName = "John",
                    LastName = "Organizer",
                    Nickname = "JohnO",
                    City = "Hanoi",
                    Phone = "+84912345678",
                    DaysAgo = 90
                },
                new
                {
                    Email = "sarah.events@poolmate.com",
                    FirstName = "Sarah",
                    LastName = "Events",
                    Nickname = "SarahE",
                    City = "Da Nang",
                    Phone = "+84923456789",
                    DaysAgo = 60
                }
            };

            foreach (var org in organizers)
            {
                var existingUser = await userManager.FindByEmailAsync(org.Email);
                if (existingUser == null)
                {
                    var user = new ApplicationUser
                    {
                        UserName = org.Email,
                        Email = org.Email,
                        EmailConfirmed = true,
                        FirstName = org.FirstName,
                        LastName = org.LastName,
                        Nickname = org.Nickname,
                        City = org.City,
                        Country = "VN",
                        PhoneNumber = org.Phone,
                        CreatedAt = DateTime.UtcNow.AddDays(-org.DaysAgo)
                    };

                    var result = await userManager.CreateAsync(user, "Organizer@123");
                    if (result.Succeeded)
                    {
                        await userManager.AddToRolesAsync(user, new[] { "Organizer", "Player" });
                    }
                }
            }
        }

        private static async Task SeedPlayerUsersAsync(UserManager<ApplicationUser> userManager)
        {
            var players = new[]
            {
                new { Email = "mike.player@poolmate.com", FirstName = "Mike", LastName = "Johnson", Nickname = "MikeJ", City = "Ho Chi Minh", Country = "VN", Phone = "+84934567890" },
                new { Email = "emily.pool@poolmate.com", FirstName = "Emily", LastName = "Chen", Nickname = "EmilyC", City = "Hanoi", Country = "VN", Phone = "+84945678901" },
                new { Email = "david.shark@poolmate.com", FirstName = "David", LastName = "Williams", Nickname = "TheShark", City = "Da Nang", Country = "VN", Phone = "+84956789012" },
                new { Email = "lisa.nine@poolmate.com", FirstName = "Lisa", LastName = "Martinez", Nickname = "Lisa9", City = "Can Tho", Country = "VN", Phone = "+84967890123" },
                new { Email = "robert.eight@poolmate.com", FirstName = "Robert", LastName = "Taylor", Nickname = "Rob8", City = "Hai Phong", Country = "VN", Phone = "+84978901234" },
                new { Email = "jennifer.cue@poolmate.com", FirstName = "Jennifer", LastName = "Anderson", Nickname = "JenCue", City = "Nha Trang", Country = "VN", Phone = "+84989012345" },
                new { Email = "james.pool@poolmate.com", FirstName = "James", LastName = "Thomas", Nickname = "JamesT", City = "Hue", Country = "VN", Phone = "+84990123456" },
                new { Email = "mary.break@poolmate.com", FirstName = "Mary", LastName = "Jackson", Nickname = "MaryB", City = "Vung Tau", Country = "VN", Phone = "+84901234568" },
                new { Email = "michael.rack@poolmate.com", FirstName = "Michael", LastName = "White", Nickname = "MikeRack", City = "Bien Hoa", Country = "VN", Phone = "+84912345679" },
                new { Email = "patricia.ball@poolmate.com", FirstName = "Patricia", LastName = "Harris", Nickname = "PatBall", City = "Long Xuyen", Country = "VN", Phone = "+84923456780" }
            };

            foreach (var playerData in players)
            {
                var player = await userManager.FindByEmailAsync(playerData.Email);
                if (player == null)
                {
                    player = new ApplicationUser
                    {
                        UserName = playerData.Email,
                        Email = playerData.Email,
                        EmailConfirmed = true,
                        FirstName = playerData.FirstName,
                        LastName = playerData.LastName,
                        Nickname = playerData.Nickname,
                        City = playerData.City,
                        Country = playerData.Country,
                        PhoneNumber = playerData.Phone,
                        CreatedAt = DateTime.UtcNow.AddDays(-Random.Shared.Next(30, 180))
                    };

                    var result = await userManager.CreateAsync(player, "Player@123");
                    if (result.Succeeded)
                    {
                        await userManager.AddToRoleAsync(player, "Player");
                    }
                }
            }
        }
    }
}

