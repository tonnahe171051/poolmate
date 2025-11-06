using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using PoolMate.Api.Data;
using PoolMate.Api.Models;

namespace PoolMate.Api.Services;

/// <summary>
/// Database seeder for Admin Dashboard testing
/// Seeds Users, Venues, Tournaments, and TournamentPlayers with realistic data
/// </summary>
public class DashboardDataSeeder
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILogger<DashboardDataSeeder> _logger;

    public DashboardDataSeeder(
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager,
        ILogger<DashboardDataSeeder> logger)
    {
        _context = context;
        _userManager = userManager;
        _logger = logger;
    }

    /// <summary>
    /// Main method to seed all data
    /// </summary>
    public async Task SeedAsync()
    {
        try
        {
            _logger.LogInformation("Starting database seeding for dashboard testing...");

            // Check if data already exists
            var existingUsersCount = await _context.Users.CountAsync();
            var existingVenuesCount = await _context.Venues.CountAsync();
            var existingTournamentsCount = await _context.Tournaments.CountAsync();

            _logger.LogInformation($"Current database state: {existingUsersCount} users, {existingVenuesCount} venues, {existingTournamentsCount} tournaments");

            if (existingVenuesCount > 0 || existingTournamentsCount > 0)
            {
                _logger.LogWarning("Database already contains seeded data. Skipping seed.");
                return;
            }

            // Seed in order: Users -> Venues -> Tournaments -> TournamentPlayers
            _logger.LogInformation("Step 1: Seeding Users...");
            var users = await SeedUsersAsync();
            if (users.Count == 0)
            {
                throw new Exception("Failed to create any users. Cannot proceed with seeding.");
            }
            _logger.LogInformation($"✅ Step 1 Complete: {users.Count} users created");

            _logger.LogInformation("Step 2: Seeding Venues...");
            var venues = await SeedVenuesAsync(users);
            _logger.LogInformation($"✅ Step 2 Complete: {venues.Count} venues created");

            _logger.LogInformation("Step 3: Seeding Tournaments...");
            var tournaments = await SeedTournamentsAsync(users, venues);
            _logger.LogInformation($"✅ Step 3 Complete: {tournaments.Count} tournaments created");

            _logger.LogInformation("Step 4: Seeding Tournament Players...");
            var playerCount = await SeedTournamentPlayersAsync(tournaments, users);
            _logger.LogInformation($"✅ Step 4 Complete: {playerCount} tournament players created");

            // Final verification
            var finalUsersCount = await _context.Users.CountAsync();
            var finalVenuesCount = await _context.Venues.CountAsync();
            var finalTournamentsCount = await _context.Tournaments.CountAsync();
            var finalPlayersCount = await _context.TournamentPlayers.CountAsync();

            _logger.LogInformation("✅ Database seeding completed successfully!");
            _logger.LogInformation($"Final counts: {finalUsersCount} users, {finalVenuesCount} venues, {finalTournamentsCount} tournaments, {finalPlayersCount} players");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Error during database seeding");
            throw;
        }
    }

    #region Seed Users

    private async Task<List<ApplicationUser>> SeedUsersAsync()
    {
        _logger.LogInformation("Seeding users...");

        var users = new List<ApplicationUser>();
        var now = DateTime.UtcNow;

        // Create 40 users distributed across 3 months
        var userData = new[]
        {
            // Users from 2 months ago (August 2025) - 10 users
            ("player1@test.com", "John", "Smith", "JSmith", now.AddMonths(-2).AddDays(-15)),
            ("player2@test.com", "Emma", "Johnson", "EmmaJ", now.AddMonths(-2).AddDays(-10)),
            ("player3@test.com", "Michael", "Williams", "MikeW", now.AddMonths(-2).AddDays(-8)),
            ("player4@test.com", "Sophia", "Brown", "SophB", now.AddMonths(-2).AddDays(-5)),
            ("player5@test.com", "James", "Jones", "JJ", now.AddMonths(-2).AddDays(-3)),
            ("player6@test.com", "Olivia", "Garcia", "OlivG", now.AddMonths(-2).AddDays(-2)),
            ("player7@test.com", "William", "Miller", "WillM", now.AddMonths(-2).AddDays(-1)),
            ("player8@test.com", "Ava", "Davis", "AvaD", now.AddMonths(-2)),
            ("player9@test.com", "Robert", "Rodriguez", "BobR", now.AddMonths(-2).AddDays(5)),
            ("player10@test.com", "Isabella", "Martinez", "IsaM", now.AddMonths(-2).AddDays(10)),

            // Users from last month (September 2025) - 15 users
            ("player11@test.com", "David", "Hernandez", "DaveH", now.AddMonths(-1).AddDays(-25)),
            ("player12@test.com", "Mia", "Lopez", "MiaL", now.AddMonths(-1).AddDays(-22)),
            ("player13@test.com", "Richard", "Gonzalez", "RichG", now.AddMonths(-1).AddDays(-20)),
            ("player14@test.com", "Charlotte", "Wilson", "CharW", now.AddMonths(-1).AddDays(-18)),
            ("player15@test.com", "Joseph", "Anderson", "JoeA", now.AddMonths(-1).AddDays(-15)),
            ("player16@test.com", "Amelia", "Thomas", "AmelT", now.AddMonths(-1).AddDays(-12)),
            ("player17@test.com", "Thomas", "Taylor", "TomT", now.AddMonths(-1).AddDays(-10)),
            ("player18@test.com", "Harper", "Moore", "HarpM", now.AddMonths(-1).AddDays(-8)),
            ("player19@test.com", "Christopher", "Jackson", "ChrisJ", now.AddMonths(-1).AddDays(-6)),
            ("player20@test.com", "Evelyn", "Martin", "EveM", now.AddMonths(-1).AddDays(-5)),
            ("player21@test.com", "Daniel", "Lee", "DanL", now.AddMonths(-1).AddDays(-4)),
            ("player22@test.com", "Abigail", "Perez", "AbbyP", now.AddMonths(-1).AddDays(-3)),
            ("player23@test.com", "Matthew", "Thompson", "MattT", now.AddMonths(-1).AddDays(-2)),
            ("player24@test.com", "Emily", "White", "EmilyW", now.AddMonths(-1).AddDays(-1)),
            ("player25@test.com", "Anthony", "Harris", "TonyH", now.AddMonths(-1)),

            // Users from this month (October 2025) - 15 users
            ("player26@test.com", "Elizabeth", "Sanchez", "LizS", now.AddDays(-25)),
            ("player27@test.com", "Mark", "Clark", "MarkC", now.AddDays(-22)),
            ("player28@test.com", "Sofia", "Ramirez", "SofR", now.AddDays(-20)),
            ("player29@test.com", "Donald", "Lewis", "DonL", now.AddDays(-18)),
            ("player30@test.com", "Camila", "Robinson", "CamR", now.AddDays(-15)),
            ("player31@test.com", "Steven", "Walker", "SteveW", now.AddDays(-12)),
            ("player32@test.com", "Avery", "Young", "AveryY", now.AddDays(-10)),
            ("player33@test.com", "Paul", "Allen", "PaulA", now.AddDays(-8)),
            ("player34@test.com", "Ella", "King", "EllaK", now.AddDays(-6)),
            ("player35@test.com", "Andrew", "Wright", "AndyW", now.AddDays(-5)),
            ("player36@test.com", "Scarlett", "Scott", "ScarS", now.AddDays(-4)),
            ("player37@test.com", "Joshua", "Torres", "JoshT", now.AddDays(-3)),
            ("player38@test.com", "Victoria", "Nguyen", "VicN", now.AddDays(-2)),
            ("player39@test.com", "Kenneth", "Hill", "KenH", now.AddDays(-1)),
            ("player40@test.com", "Madison", "Flores", "MadF", now),
        };

        foreach (var (email, firstName, lastName, nickname, createdAt) in userData)
        {
            var user = new ApplicationUser
            {
                UserName = email,
                Email = email,
                EmailConfirmed = true,
                FirstName = firstName,
                LastName = lastName,
                Nickname = nickname,
                City = GetRandomCity(),
                Country = "US",
                CreatedAt = createdAt,
                SecurityStamp = Guid.NewGuid().ToString()
            };

            var result = await _userManager.CreateAsync(user, "Test@123");
            if (result.Succeeded)
            {
                users.Add(user);
                _logger.LogInformation($"✅ Created user: {email}");
            }
            else
            {
                var errors = string.Join(", ", result.Errors.Select(e => e.Description));
                _logger.LogError($"❌ Failed to create user {email}: {errors}");
            }
        }

        _logger.LogInformation($"✅ Successfully created {users.Count} out of {userData.Length} users");
        return users;
    }

    #endregion

    #region Seed Venues

    private async Task<List<Venue>> SeedVenuesAsync(List<ApplicationUser> users)
    {
        _logger.LogInformation("Seeding venues...");

        var now = DateTime.UtcNow;
        var venues = new List<Venue>
        {
            // Venues from 2 months ago
            new Venue
            {
                Name = "Downtown Pool Hall",
                Address = "123 Main Street",
                City = "New York",
                Country = "US",
                CreatedByUserId = users[0].Id,
                CreatedAt = now.AddMonths(-2).AddDays(-10)
            },
            new Venue
            {
                Name = "City Billiards Club",
                Address = "456 Oak Avenue",
                City = "Los Angeles",
                Country = "US",
                CreatedByUserId = users[1].Id,
                CreatedAt = now.AddMonths(-2).AddDays(-5)
            },

            // Venues from last month
            new Venue
            {
                Name = "Riverside Pool Center",
                Address = "789 River Road",
                City = "Chicago",
                Country = "US",
                CreatedByUserId = users[2].Id,
                CreatedAt = now.AddMonths(-1).AddDays(-20)
            },
            new Venue
            {
                Name = "Metro Billiards",
                Address = "321 Metro Boulevard",
                City = "Houston",
                Country = "US",
                CreatedByUserId = users[3].Id,
                CreatedAt = now.AddMonths(-1).AddDays(-10)
            },
            new Venue
            {
                Name = "Uptown Pool Lounge",
                Address = "555 Uptown Drive",
                City = "Phoenix",
                Country = "US",
                CreatedByUserId = users[4].Id,
                CreatedAt = now.AddMonths(-1).AddDays(-5)
            },

            // Venues from this month
            new Venue
            {
                Name = "Westside Pool Lounge",
                Address = "654 West Street",
                City = "San Diego",
                Country = "US",
                CreatedByUserId = users[5].Id,
                CreatedAt = now.AddDays(-20)
            },
            new Venue
            {
                Name = "Elite Billiards Club",
                Address = "987 Elite Drive",
                City = "Dallas",
                Country = "US",
                CreatedByUserId = users[6].Id,
                CreatedAt = now.AddDays(-10)
            },
            new Venue
            {
                Name = "Championship Pool Arena",
                Address = "111 Champion Way",
                City = "Miami",
                Country = "US",
                CreatedByUserId = users[7].Id,
                CreatedAt = now.AddDays(-5)
            }
        };

        await _context.Venues.AddRangeAsync(venues);
        await _context.SaveChangesAsync();

        _logger.LogInformation($"✅ Created {venues.Count} venues");
        return venues;
    }

    #endregion

    #region Seed Tournaments

    private async Task<List<Tournament>> SeedTournamentsAsync(List<ApplicationUser> users, List<Venue> venues)
    {
        _logger.LogInformation("Seeding tournaments...");

        var now = DateTime.UtcNow;
        var tournaments = new List<Tournament>();

        // Tournaments from last month (September) - 6 tournaments
        tournaments.AddRange(new[]
        {
            // Completed tournaments
            new Tournament
            {
                Name = "September 8-Ball Championship",
                Description = "Monthly 8-ball tournament with great prizes",
                StartUtc = now.AddMonths(-1).AddDays(-20),
                EndUtc = now.AddMonths(-1).AddDays(-19),
                VenueId = venues[0].Id,
                OwnerUserId = users[0].Id,
                Status = TournamentStatus.Completed,
                PlayerType = PlayerType.Singles,
                BracketType = BracketType.DoubleElimination,
                GameType = GameType.EightBall,
                EntryFee = 50.00m,
                BracketSizeEstimate = 16,
                IsPublic = true,
                OnlineRegistrationEnabled = true,
                WinnersRaceTo = 7,
                LosersRaceTo = 5,
                FinalsRaceTo = 9,
                CreatedAt = now.AddMonths(-1).AddDays(-25),
                UpdatedAt = now.AddMonths(-1).AddDays(-19)
            },
            new Tournament
            {
                Name = "9-Ball Open September",
                Description = "Open 9-ball tournament for all skill levels",
                StartUtc = now.AddMonths(-1).AddDays(-15),
                EndUtc = now.AddMonths(-1).AddDays(-14),
                VenueId = venues[1].Id,
                OwnerUserId = users[1].Id,
                Status = TournamentStatus.Completed,
                PlayerType = PlayerType.Singles,
                BracketType = BracketType.DoubleElimination,
                GameType = GameType.NineBall,
                EntryFee = 30.00m,
                BracketSizeEstimate = 32,
                IsPublic = true,
                OnlineRegistrationEnabled = true,
                WinnersRaceTo = 9,
                LosersRaceTo = 7,
                FinalsRaceTo = 11,
                CreatedAt = now.AddMonths(-1).AddDays(-22),
                UpdatedAt = now.AddMonths(-1).AddDays(-14)
            },
            new Tournament
            {
                Name = "Labor Day Pool Masters",
                Description = "Special Labor Day weekend tournament",
                StartUtc = now.AddMonths(-1).AddDays(-10),
                EndUtc = now.AddMonths(-1).AddDays(-9),
                VenueId = venues[2].Id,
                OwnerUserId = users[2].Id,
                Status = TournamentStatus.Completed,
                PlayerType = PlayerType.Singles,
                BracketType = BracketType.SingleElimination,
                GameType = GameType.TenBall,
                EntryFee = 100.00m,
                BracketSizeEstimate = 16,
                IsPublic = true,
                OnlineRegistrationEnabled = true,
                AddedMoney = 500.00m,
                WinnersRaceTo = 9,
                FinalsRaceTo = 11,
                CreatedAt = now.AddMonths(-1).AddDays(-15),
                UpdatedAt = now.AddMonths(-1).AddDays(-9)
            },
            
            // In Progress (started last month, still ongoing)
            new Tournament
            {
                Name = "Monthly League Championship",
                Description = "End of month league championship",
                StartUtc = now.AddMonths(-1).AddDays(-2),
                VenueId = venues[3].Id,
                OwnerUserId = users[3].Id,
                Status = TournamentStatus.InProgress,
                PlayerType = PlayerType.Singles,
                BracketType = BracketType.DoubleElimination,
                GameType = GameType.NineBall,
                EntryFee = 40.00m,
                BracketSizeEstimate = 24,
                IsPublic = true,
                OnlineRegistrationEnabled = false,
                WinnersRaceTo = 7,
                LosersRaceTo = 5,
                CreatedAt = now.AddMonths(-1).AddDays(-8),
                UpdatedAt = now.AddDays(-1)
            },
            
            // Smaller tournaments
            new Tournament
            {
                Name = "Midweek Mini Tournament",
                Description = "Quick midweek tournament",
                StartUtc = now.AddMonths(-1).AddDays(-12),
                EndUtc = now.AddMonths(-1).AddDays(-12),
                VenueId = venues[4].Id,
                OwnerUserId = users[4].Id,
                Status = TournamentStatus.Completed,
                PlayerType = PlayerType.Singles,
                BracketType = BracketType.SingleElimination,
                GameType = GameType.EightBall,
                EntryFee = 20.00m,
                BracketSizeEstimate = 8,
                IsPublic = true,
                OnlineRegistrationEnabled = true,
                WinnersRaceTo = 5,
                CreatedAt = now.AddMonths(-1).AddDays(-14),
                UpdatedAt = now.AddMonths(-1).AddDays(-12)
            },
            new Tournament
            {
                Name = "Weekend Warriors",
                Description = "Saturday night special",
                StartUtc = now.AddMonths(-1).AddDays(-5),
                EndUtc = now.AddMonths(-1).AddDays(-5),
                VenueId = venues[0].Id,
                OwnerUserId = users[0].Id,
                Status = TournamentStatus.Completed,
                PlayerType = PlayerType.Singles,
                BracketType = BracketType.DoubleElimination,
                GameType = GameType.NineBall,
                EntryFee = 35.00m,
                BracketSizeEstimate = 16,
                IsPublic = true,
                OnlineRegistrationEnabled = true,
                WinnersRaceTo = 7,
                LosersRaceTo = 5,
                CreatedAt = now.AddMonths(-1).AddDays(-10),
                UpdatedAt = now.AddMonths(-1).AddDays(-5)
            }
        });

        // Tournaments from this month (October) - 9 tournaments
        tournaments.AddRange(new[]
        {
            // Active tournaments (In Progress)
            new Tournament
            {
                Name = "Halloween 8-Ball Spooktacular",
                Description = "Halloween themed 8-ball tournament with costume prizes",
                StartUtc = now.AddDays(-5),
                VenueId = venues[5].Id,
                OwnerUserId = users[5].Id,
                Status = TournamentStatus.InProgress,
                PlayerType = PlayerType.Singles,
                BracketType = BracketType.DoubleElimination,
                GameType = GameType.EightBall,
                EntryFee = 40.00m,
                BracketSizeEstimate = 32,
                IsPublic = true,
                OnlineRegistrationEnabled = true,
                AddedMoney = 300.00m,
                WinnersRaceTo = 7,
                LosersRaceTo = 5,
                FinalsRaceTo = 9,
                CreatedAt = now.AddDays(-20),
                UpdatedAt = now.AddDays(-1)
            },
            new Tournament
            {
                Name = "October Open Championship",
                Description = "Monthly open championship",
                StartUtc = now.AddDays(-3),
                VenueId = venues[6].Id,
                OwnerUserId = users[6].Id,
                Status = TournamentStatus.InProgress,
                PlayerType = PlayerType.Singles,
                BracketType = BracketType.DoubleElimination,
                GameType = GameType.NineBall,
                EntryFee = 50.00m,
                BracketSizeEstimate = 16,
                IsPublic = true,
                OnlineRegistrationEnabled = true,
                WinnersRaceTo = 9,
                LosersRaceTo = 7,
                FinalsRaceTo = 11,
                CreatedAt = now.AddDays(-15),
                UpdatedAt = now
            },
            new Tournament
            {
                Name = "Weekend Warriors October Edition",
                Description = "Fast-paced weekend tournament",
                StartUtc = now.AddDays(-2),
                VenueId = venues[0].Id,
                OwnerUserId = users[0].Id,
                Status = TournamentStatus.InProgress,
                PlayerType = PlayerType.Singles,
                BracketType = BracketType.SingleElimination,
                GameType = GameType.EightBall,
                EntryFee = 25.00m,
                BracketSizeEstimate = 16,
                IsPublic = true,
                OnlineRegistrationEnabled = true,
                WinnersRaceTo = 5,
                CreatedAt = now.AddDays(-10),
                UpdatedAt = now
            },
            new Tournament
            {
                Name = "Pro-Am Invitational",
                Description = "Mixed pro and amateur tournament",
                StartUtc = now.AddDays(-1),
                VenueId = venues[7].Id,
                OwnerUserId = users[7].Id,
                Status = TournamentStatus.InProgress,
                PlayerType = PlayerType.Singles,
                BracketType = BracketType.DoubleElimination,
                GameType = GameType.TenBall,
                EntryFee = 75.00m,
                BracketSizeEstimate = 24,
                IsPublic = true,
                OnlineRegistrationEnabled = false,
                AddedMoney = 1000.00m,
                WinnersRaceTo = 9,
                LosersRaceTo = 7,
                FinalsRaceTo = 13,
                CreatedAt = now.AddDays(-25),
                UpdatedAt = now
            },

            // Upcoming tournaments
            new Tournament
            {
                Name = "November Preview Tournament",
                Description = "Get ready for November competitions",
                StartUtc = now.AddDays(5),
                VenueId = venues[1].Id,
                OwnerUserId = users[1].Id,
                Status = TournamentStatus.Upcoming,
                PlayerType = PlayerType.Singles,
                BracketType = BracketType.DoubleElimination,
                GameType = GameType.NineBall,
                EntryFee = 60.00m,
                BracketSizeEstimate = 32,
                IsPublic = true,
                OnlineRegistrationEnabled = true,
                WinnersRaceTo = 9,
                LosersRaceTo = 7,
                CreatedAt = now.AddDays(-18),
                UpdatedAt = now.AddDays(-1)
            },
            new Tournament
            {
                Name = "Thanksgiving Warm-up",
                Description = "Warm up for Thanksgiving tournaments",
                StartUtc = now.AddDays(10),
                VenueId = venues[2].Id,
                OwnerUserId = users[2].Id,
                Status = TournamentStatus.Upcoming,
                PlayerType = PlayerType.Doubles,
                BracketType = BracketType.DoubleElimination,
                GameType = GameType.EightBall,
                EntryFee = 80.00m,
                BracketSizeEstimate = 16,
                IsPublic = true,
                OnlineRegistrationEnabled = true,
                WinnersRaceTo = 7,
                LosersRaceTo = 5,
                CreatedAt = now.AddDays(-12),
                UpdatedAt = now.AddDays(-2)
            },

            // Completed tournaments (this month)
            new Tournament
            {
                Name = "October Kickoff Tournament",
                Description = "Start of October tournament season",
                StartUtc = now.AddDays(-22),
                EndUtc = now.AddDays(-22),
                VenueId = venues[3].Id,
                OwnerUserId = users[3].Id,
                Status = TournamentStatus.Completed,
                PlayerType = PlayerType.Singles,
                BracketType = BracketType.SingleElimination,
                GameType = GameType.EightBall,
                EntryFee = 30.00m,
                BracketSizeEstimate = 16,
                IsPublic = true,
                OnlineRegistrationEnabled = true,
                WinnersRaceTo = 7,
                CreatedAt = now.AddDays(-26),
                UpdatedAt = now.AddDays(-22)
            },
            new Tournament
            {
                Name = "Midweek Madness October",
                Description = "Quick Wednesday night tournament",
                StartUtc = now.AddDays(-15),
                EndUtc = now.AddDays(-15),
                VenueId = venues[4].Id,
                OwnerUserId = users[4].Id,
                Status = TournamentStatus.Completed,
                PlayerType = PlayerType.Singles,
                BracketType = BracketType.SingleElimination,
                GameType = GameType.NineBall,
                EntryFee = 20.00m,
                BracketSizeEstimate = 8,
                IsPublic = true,
                OnlineRegistrationEnabled = true,
                WinnersRaceTo = 5,
                CreatedAt = now.AddDays(-18),
                UpdatedAt = now.AddDays(-15)
            },
            new Tournament
            {
                Name = "Columbus Day Special",
                Description = "Columbus Day holiday tournament",
                StartUtc = now.AddDays(-13),
                EndUtc = now.AddDays(-13),
                VenueId = venues[5].Id,
                OwnerUserId = users[5].Id,
                Status = TournamentStatus.Completed,
                PlayerType = PlayerType.Singles,
                BracketType = BracketType.DoubleElimination,
                GameType = GameType.TenBall,
                EntryFee = 45.00m,
                BracketSizeEstimate = 16,
                IsPublic = true,
                OnlineRegistrationEnabled = true,
                WinnersRaceTo = 7,
                LosersRaceTo = 5,
                CreatedAt = now.AddDays(-20),
                UpdatedAt = now.AddDays(-13)
            }
        });

        await _context.Tournaments.AddRangeAsync(tournaments);
        await _context.SaveChangesAsync();

        _logger.LogInformation($"✅ Created {tournaments.Count} tournaments");
        _logger.LogInformation($"   - {tournaments.Count(t => t.Status == TournamentStatus.InProgress)} In Progress");
        _logger.LogInformation($"   - {tournaments.Count(t => t.Status == TournamentStatus.Completed)} Completed");
        _logger.LogInformation($"   - {tournaments.Count(t => t.Status == TournamentStatus.Upcoming)} Upcoming");

        return tournaments;
    }

    #endregion

    #region Seed TournamentPlayers

    private async Task<int> SeedTournamentPlayersAsync(List<Tournament> tournaments, List<ApplicationUser> users)
    {
        _logger.LogInformation("Seeding tournament players...");

        var tournamentPlayers = new List<TournamentPlayer>();
        var random = new Random();

        foreach (var tournament in tournaments)
        {
            // Skip upcoming tournaments (no players yet)
            if (tournament.Status == TournamentStatus.Upcoming)
                continue;

            // Calculate how many players to add (60-90% of bracket size)
            var bracketSize = tournament.BracketSizeEstimate ?? 16;
            var playerCount = random.Next(
                (int)(bracketSize * 0.6),
                (int)(bracketSize * 0.9) + 1
            );

            // Randomly select players from our user pool
            var selectedUsers = users
                .OrderBy(_ => random.Next())
                .Take(playerCount)
                .ToList();

            foreach (var user in selectedUsers)
            {
                tournamentPlayers.Add(new TournamentPlayer
                {
                    TournamentId = tournament.Id,
                    DisplayName = $"{user.FirstName} {user.LastName}",
                    Nickname = user.Nickname,
                    Email = user.Email,
                    Country = user.Country,
                    City = user.City,
                    Status = TournamentPlayerStatus.Confirmed,
                    SkillLevel = random.Next(1, 10)
                });
            }
        }

        await _context.TournamentPlayers.AddRangeAsync(tournamentPlayers);
        await _context.SaveChangesAsync();

        _logger.LogInformation($"✅ Created {tournamentPlayers.Count} tournament player registrations");
        _logger.LogInformation($"   - Average: {tournamentPlayers.Count / Math.Max(tournaments.Count(t => t.Status != TournamentStatus.Upcoming), 1)} players per tournament");

        return tournamentPlayers.Count;
    }

    #endregion

    #region Helper Methods

    private string GetRandomCity()
    {
        var cities = new[]
        {
            "New York", "Los Angeles", "Chicago", "Houston", "Phoenix",
            "Philadelphia", "San Antonio", "San Diego", "Dallas", "San Jose",
            "Austin", "Jacksonville", "Fort Worth", "Columbus", "Charlotte",
            "San Francisco", "Indianapolis", "Seattle", "Denver", "Boston"
        };
        return cities[new Random().Next(cities.Length)];
    }

    #endregion
}

