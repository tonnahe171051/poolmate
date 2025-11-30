using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using PoolMate.Api.Models;

namespace PoolMate.Api.Data.Seeds
{
    /// <summary>
    /// Seed data cho Tournament
    /// </summary>
    public static class TournamentSeed
    {
        public static async Task SeedAsync(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            // Kiểm tra nếu đã có tournament thì không seed nữa
            if (await context.Tournaments.AnyAsync())
            {
                return;
            }

            // Lấy organizer để làm owner
            var organizer = await userManager.FindByEmailAsync("john.organizer@poolmate.com");
            if (organizer == null)
            {
                return; // Phải seed User trước
            }

            // Lấy venues
            var venues = await context.Venues.ToListAsync();
            if (!venues.Any())
            {
                return; // Phải seed Venue trước
            }

            // Lấy payout templates
            var payoutTemplate = await context.PayoutTemplates.FirstOrDefaultAsync(pt => pt.MinPlayers <= 16 && pt.MaxPlayers >= 16);

            var tournaments = new[]
            {
                // Tournament 1: Upcoming - Chưa bắt đầu
                new Tournament
                {
                    Name = "Ho Chi Minh Open 9-Ball Championship 2024",
                    Description = "Annual 9-ball championship in Ho Chi Minh City. Open to all skill levels.",
                    StartUtc = DateTime.UtcNow.AddDays(7), // 7 ngày sau
                    VenueId = venues[0].Id,
                    OwnerUserId = organizer.Id,
                    PlayerType = PlayerType.Singles,
                    BracketType = BracketType.DoubleElimination,
                    GameType = GameType.NineBall,
                    WinnersRaceTo = 7,
                    LosersRaceTo = 5,
                    FinalsRaceTo = 9,
                    BracketOrdering = BracketOrdering.Seeded,
                    OnlineRegistrationEnabled = true,
                    IsPublic = true,
                    BracketSizeEstimate = 16,
                    EntryFee = 200000m,
                    AdminFee = 20000m,
                    AddedMoney = 1000000m,
                    PayoutMode = PayoutMode.Template,
                    PayoutTemplateId = payoutTemplate?.Id,
                    Rule = Rule.WNT,
                    BreakFormat = BreakFormat.AlternateBreak,
                    IsStarted = false,
                    Status = TournamentStatus.Upcoming,
                    IsMultiStage = false,
                    CreatedAt = DateTime.UtcNow.AddDays(-15),
                    UpdatedAt = DateTime.UtcNow.AddDays(-15)
                },

                // Tournament 2: InProgress - Đang diễn ra
                new Tournament
                {
                    Name = "Hanoi 8-Ball Pro Tour",
                    Description = "Professional 8-ball tournament for experienced players.",
                    StartUtc = DateTime.UtcNow.AddDays(-2), // Đã bắt đầu 2 ngày trước
                    VenueId = venues.Count > 1 ? venues[1].Id : venues[0].Id,
                    OwnerUserId = organizer.Id,
                    PlayerType = PlayerType.Singles,
                    BracketType = BracketType.DoubleElimination,
                    GameType = GameType.EightBall,
                    WinnersRaceTo = 9,
                    LosersRaceTo = 7,
                    FinalsRaceTo = 11,
                    BracketOrdering = BracketOrdering.Seeded,
                    OnlineRegistrationEnabled = false,
                    IsPublic = true,
                    BracketSizeEstimate = 16,
                    EntryFee = 300000m,
                    AdminFee = 30000m,
                    AddedMoney = 2000000m,
                    PayoutMode = PayoutMode.Template,
                    PayoutTemplateId = payoutTemplate?.Id,
                    Rule = Rule.WPA,
                    BreakFormat = BreakFormat.WinnerBreak,
                    IsStarted = true,
                    Status = TournamentStatus.InProgress,
                    IsMultiStage = false,
                    CreatedAt = DateTime.UtcNow.AddDays(-10),
                    UpdatedAt = DateTime.UtcNow.AddDays(-2)
                },

                // Tournament 3: Completed - Đã kết thúc
                new Tournament
                {
                    Name = "Da Nang Beach 10-Ball Classic",
                    Description = "Completed 10-ball tournament by the beach.",
                    StartUtc = DateTime.UtcNow.AddDays(-20),
                    EndUtc = DateTime.UtcNow.AddDays(-18),
                    VenueId = venues.Count > 2 ? venues[2].Id : venues[0].Id,
                    OwnerUserId = organizer.Id,
                    PlayerType = PlayerType.Singles,
                    BracketType = BracketType.SingleElimination,
                    GameType = GameType.TenBall,
                    WinnersRaceTo = 7,
                    FinalsRaceTo = 9,
                    BracketOrdering = BracketOrdering.Random,
                    OnlineRegistrationEnabled = false,
                    IsPublic = true,
                    BracketSizeEstimate = 8,
                    EntryFee = 150000m,
                    AdminFee = 15000m,
                    AddedMoney = 500000m,
                    PayoutMode = PayoutMode.Custom,
                    TotalPrize = 1500000m,
                    Rule = Rule.WNT,
                    BreakFormat = BreakFormat.AlternateBreak,
                    IsStarted = true,
                    Status = TournamentStatus.Completed,
                    IsMultiStage = false,
                    CreatedAt = DateTime.UtcNow.AddDays(-30),
                    UpdatedAt = DateTime.UtcNow.AddDays(-18)
                },

                // Tournament 4: Multi-stage - Upcoming
                new Tournament
                {
                    Name = "Can Tho Masters Multi-Stage Championship",
                    Description = "Two-stage championship: Round Robin to Double Elimination finals.",
                    StartUtc = DateTime.UtcNow.AddDays(14), // 2 tuần sau
                    VenueId = venues.Count > 3 ? venues[3].Id : venues[0].Id,
                    OwnerUserId = organizer.Id,
                    PlayerType = PlayerType.Singles,
                    BracketType = BracketType.DoubleElimination,
                    GameType = GameType.NineBall,
                    WinnersRaceTo = 7,
                    LosersRaceTo = 5,
                    FinalsRaceTo = 11,
                    BracketOrdering = BracketOrdering.Seeded,
                    OnlineRegistrationEnabled = true,
                    IsPublic = true,
                    BracketSizeEstimate = 32,
                    EntryFee = 250000m,
                    AdminFee = 25000m,
                    AddedMoney = 3000000m,
                    PayoutMode = PayoutMode.Template,
                    PayoutTemplateId = payoutTemplate?.Id,
                    Rule = Rule.WPA,
                    BreakFormat = BreakFormat.AlternateBreak,
                    IsStarted = false,
                    Status = TournamentStatus.Upcoming,
                    IsMultiStage = true,
                    AdvanceToStage2Count = 8,
                    Stage1Ordering = BracketOrdering.Random,
                    Stage2Ordering = BracketOrdering.Seeded,
                    CreatedAt = DateTime.UtcNow.AddDays(-5),
                    UpdatedAt = DateTime.UtcNow.AddDays(-5)
                }
            };

            await context.Tournaments.AddRangeAsync(tournaments);
            await context.SaveChangesAsync();
        }
    }
}

