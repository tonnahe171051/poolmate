using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PoolMate.Api.Data;
using PoolMate.Api.Dtos.Auth;
using PoolMate.Api.Integrations.Email;
using PoolMate.Api.Models;
using System.Security.Claims;
using System.Text.Encodings.Web;

namespace PoolMateBackend.Tests.IntegrationTests.Base;

/// <summary>
/// Mock email sender for integration tests - captures sent emails for verification.
/// </summary>
public class MockEmailSender : IEmailSender
{
    public List<(string To, string Subject, string Body)> SentEmails { get; } = new();

    public Task SendAsync(string to, string subject, string body, CancellationToken ct = default)
    {
        SentEmails.Add((to, subject, body));
        return Task.CompletedTask;
    }
}

/// <summary>
/// Test authentication handler that validates users based on X-Test-User-Id header.
/// </summary>
public class TestAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public const string AuthScheme = "TestScheme";
    public const string TestUserIdHeader = "X-Test-User-Id";

    public TestAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : base(options, logger, encoder)
    {
    }

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.ContainsKey(TestUserIdHeader))
        {
            return AuthenticateResult.NoResult();
        }

        var userId = Request.Headers[TestUserIdHeader].ToString();
        if (string.IsNullOrEmpty(userId))
        {
            return AuthenticateResult.Fail("Invalid user ID");
        }

        // Get user from database to build proper claims
        var userManager = Context.RequestServices.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await userManager.FindByIdAsync(userId);

        if (user == null)
        {
            return AuthenticateResult.Fail("User not found");
        }

        // Check lockout
        if (user.LockoutEnd.HasValue && user.LockoutEnd.Value > DateTimeOffset.UtcNow)
        {
            return AuthenticateResult.Fail("User is locked out");
        }

        var roles = await userManager.GetRolesAsync(user);

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId),
            new(ClaimTypes.Name, user.UserName ?? string.Empty),
            new(ClaimTypes.Email, user.Email ?? string.Empty),
        };

        foreach (var role in roles)
        {
            claims.Add(new Claim(ClaimTypes.Role, role));
        }

        var identity = new ClaimsIdentity(claims, AuthScheme);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, AuthScheme);

        return AuthenticateResult.Success(ticket);
    }
}

/// <summary>
/// Custom WebApplicationFactory for integration testing.
/// Replaces SQL Server with InMemory database and seeds test data.
/// </summary>
public class PoolMateWebApplicationFactory : WebApplicationFactory<Program>
{
    // Test user IDs - use these constants in your tests
    public const string AdminUserId = "admin-user-id";
    public const string OrganizerUserId = "organizer-user-id";
    public const string TestVenueId = "1";
    public const string TestPayoutTemplateId = "1";

    // Test user credentials (for login tests)
    public const string AdminPassword = "Admin@123456";
    public const string OrganizerPassword = "Organizer@123456";

    private readonly string _databaseName = Guid.NewGuid().ToString();

    /// <summary>
    /// Gets the mock email sender for verifying sent emails in tests.
    /// </summary>
    public MockEmailSender MockEmailSender { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            // Remove the existing DbContext registration
            var descriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(DbContextOptions<ApplicationDbContext>));
            
            if (descriptor != null)
            {
                services.Remove(descriptor);
            }

            // Remove any other ApplicationDbContext related registrations
            var dbContextDescriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(ApplicationDbContext));
            
            if (dbContextDescriptor != null)
            {
                services.Remove(dbContextDescriptor);
            }

            // Replace email sender with mock
            var emailDescriptor = services.SingleOrDefault(d => d.ServiceType == typeof(IEmailSender));
            if (emailDescriptor != null)
            {
                services.Remove(emailDescriptor);
            }
            services.AddSingleton<IEmailSender>(MockEmailSender);

            // Add InMemory Database with unique name per instance
            services.AddDbContext<ApplicationDbContext>(options =>
            {
                options.UseInMemoryDatabase(_databaseName);
            });

            // Add test authentication scheme
            services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = TestAuthHandler.AuthScheme;
                options.DefaultChallengeScheme = TestAuthHandler.AuthScheme;
            })
            .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.AuthScheme, _ => { });

            // Build service provider and seed data
            var sp = services.BuildServiceProvider();

            using var scope = sp.CreateScope();
            var scopedServices = scope.ServiceProvider;
            var db = scopedServices.GetRequiredService<ApplicationDbContext>();

            // Ensure database is created
            db.Database.EnsureCreated();

            // Seed test data (users will be seeded with passwords via UserManager)
            SeedTestData(db);

            // Seed users with passwords using UserManager
            SeedUsersWithPasswords(scopedServices).GetAwaiter().GetResult();
        });

        builder.UseEnvironment("Development");
    }

    /// <summary>
    /// Seeds the test database with required data for integration tests.
    /// Note: Users are seeded separately via UserManager to set passwords properly.
    /// </summary>
    private static void SeedTestData(ApplicationDbContext context)
    {
        // ===== 2. Seed Venue =====
        if (!context.Venues.Any())
        {
            var venue = new Venue
            {
                Id = 1,
                Name = "Test Pool Hall",
                Address = "123 Test Street",
                City = "Ho Chi Minh",
                Country = "VN",
                CreatedByUserId = OrganizerUserId,
                CreatedAt = DateTime.UtcNow
            };

            context.Venues.Add(venue);
        }

        // ===== 3. Seed PayoutTemplate (100% total) =====
        if (!context.PayoutTemplates.Any())
        {
            var payoutTemplate = new PayoutTemplate
            {
                Id = 1,
                Name = "Standard Top 4 Payout",
                MinPlayers = 8,
                MaxPlayers = 16,
                Places = 4,
                // JSON: 1st = 50%, 2nd = 25%, 3rd = 15%, 4th = 10% = 100%
                PercentJson = "[{\"rank\":1,\"percent\":50},{\"rank\":2,\"percent\":25},{\"rank\":3,\"percent\":15},{\"rank\":4,\"percent\":10}]",
                OwnerUserId = OrganizerUserId
            };

            context.PayoutTemplates.Add(payoutTemplate);
        }

        // ===== 4. Seed Players (10 dummy players) =====
        if (!context.Players.Any())
        {
            var players = new List<Player>();
            for (int i = 1; i <= 10; i++)
            {
                players.Add(new Player
                {
                    Id = i,
                    FullName = $"Test Player {i}",
                    Slug = $"test-player-{i}",
                    Nickname = $"Player{i}",
                    Email = $"player{i}@poolmate.test",
                    Phone = $"090000000{i}",
                    Country = "VN",
                    City = i % 2 == 0 ? "Ho Chi Minh" : "Ha Noi",
                    SkillLevel = (i % 10) + 1, // Skill level 1-10
                    CreatedAt = DateTime.UtcNow.AddDays(-i)
                });
            }

            context.Players.AddRange(players);
        }

        // Save all changes
        context.SaveChanges();
    }

    /// <summary>
    /// Seeds users with passwords using UserManager (Identity).
    /// </summary>
    private static async Task SeedUsersWithPasswords(IServiceProvider services)
    {
        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
        var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();

        // Ensure roles exist
        string[] roles = { UserRoles.ADMIN, UserRoles.PLAYER, UserRoles.ORGANIZER };
        foreach (var role in roles)
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                await roleManager.CreateAsync(new IdentityRole(role));
            }
        }

        // Seed Admin User
        if (await userManager.FindByIdAsync(AdminUserId) == null)
        {
            var adminUser = new ApplicationUser
            {
                Id = AdminUserId,
                UserName = "admin@poolmate.test",
                Email = "admin@poolmate.test",
                EmailConfirmed = true,
                FirstName = "Admin",
                LastName = "User",
                Nickname = "AdminNick",
                City = "Ho Chi Minh",
                Country = "VN",
                CreatedAt = DateTime.UtcNow
            };

            var result = await userManager.CreateAsync(adminUser, AdminPassword);
            if (result.Succeeded)
            {
                await userManager.AddToRoleAsync(adminUser, UserRoles.ADMIN);
            }
        }

        // Seed Organizer User
        if (await userManager.FindByIdAsync(OrganizerUserId) == null)
        {
            var organizerUser = new ApplicationUser
            {
                Id = OrganizerUserId,
                UserName = "organizer@poolmate.test",
                Email = "organizer@poolmate.test",
                EmailConfirmed = true,
                FirstName = "Organizer",
                LastName = "User",
                Nickname = "OrganizerNick",
                City = "Ha Noi",
                Country = "VN",
                CreatedAt = DateTime.UtcNow
            };

            var result = await userManager.CreateAsync(organizerUser, OrganizerPassword);
            if (result.Succeeded)
            {
                await userManager.AddToRoleAsync(organizerUser, UserRoles.ORGANIZER);
            }
        }
    }

    /// <summary>
    /// Creates a new scope for accessing services.
    /// </summary>
    public IServiceScope CreateScope()
    {
        return Services.CreateScope();
    }

    /// <summary>
    /// Gets a fresh database context from a new scope.
    /// </summary>
    public ApplicationDbContext GetDbContext()
    {
        var scope = CreateScope();
        return scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    }
}

