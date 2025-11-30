using Microsoft.EntityFrameworkCore;
using PoolMate.Api.Models;

namespace PoolMate.Api.Data.Seeds
{
    /// <summary>
    /// Seed data cho TournamentStage (các stage của giải)
    /// </summary>
    public static class TournamentStageSeed
    {
        public static async Task SeedAsync(ApplicationDbContext context)
        {
            // Kiểm tra nếu đã có stage thì không seed nữa
            if (await context.TournamentStages.AnyAsync())
            {
                return;
            }

            // Lấy tournaments đã started (InProgress hoặc Completed)
            var tournaments = await context.Tournaments
                .Where(t => t.IsStarted)
                .ToListAsync();
            
            if (!tournaments.Any())
            {
                return;
            }

            var stages = new List<TournamentStage>();

            foreach (var tournament in tournaments)
            {
                if (tournament.IsMultiStage && tournament.AdvanceToStage2Count.HasValue)
                {
                    // Multi-stage tournament: tạo 2 stages
                    
                    // Stage 1 - Thường là Round Robin hoặc Group
                    var stage1Status = tournament.Status == TournamentStatus.Completed 
                        ? StageStatus.Completed 
                        : StageStatus.InProgress;
                    
                    var stage1 = new TournamentStage
                    {
                        TournamentId = tournament.Id,
                        StageNo = 1,
                        Type = BracketType.DoubleElimination, // Có thể là bất kỳ type nào
                        Status = stage1Status,
                        AdvanceCount = tournament.AdvanceToStage2Count,
                        Ordering = tournament.Stage1Ordering,
                        CreatedAt = tournament.StartUtc,
                        UpdatedAt = DateTime.UtcNow,
                        CompletedAt = stage1Status == StageStatus.Completed 
                            ? tournament.EndUtc ?? DateTime.UtcNow.AddDays(-1) 
                            : null
                    };
                    stages.Add(stage1);

                    // Stage 2 - Finals
                    var stage2Status = tournament.Status == TournamentStatus.Completed 
                        ? StageStatus.Completed 
                        : StageStatus.NotStarted;
                    
                    var stage2 = new TournamentStage
                    {
                        TournamentId = tournament.Id,
                        StageNo = 2,
                        Type = tournament.BracketType,
                        Status = stage2Status,
                        AdvanceCount = null,
                        Ordering = tournament.Stage2Ordering,
                        CreatedAt = tournament.StartUtc.AddHours(6), // Stage 2 bắt đầu sau stage 1
                        UpdatedAt = DateTime.UtcNow,
                        CompletedAt = stage2Status == StageStatus.Completed 
                            ? tournament.EndUtc 
                            : null
                    };
                    stages.Add(stage2);
                }
                else
                {
                    // Single-stage tournament: chỉ 1 stage
                    var stageStatus = tournament.Status == TournamentStatus.Completed 
                        ? StageStatus.Completed 
                        : StageStatus.InProgress;
                    
                    var stage = new TournamentStage
                    {
                        TournamentId = tournament.Id,
                        StageNo = 1,
                        Type = tournament.BracketType,
                        Status = stageStatus,
                        AdvanceCount = null,
                        Ordering = tournament.BracketOrdering,
                        CreatedAt = tournament.StartUtc,
                        UpdatedAt = DateTime.UtcNow,
                        CompletedAt = stageStatus == StageStatus.Completed 
                            ? tournament.EndUtc 
                            : null
                    };
                    stages.Add(stage);
                }
            }

            await context.TournamentStages.AddRangeAsync(stages);
            await context.SaveChangesAsync();
        }
    }
}

