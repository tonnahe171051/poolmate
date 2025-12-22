using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using PoolMate.Api.Data;
using PoolMate.Api.Dtos.Tournament;
using PoolMate.Api.Models;

namespace PoolMate.Api.Services
{
    internal sealed record StageEvaluation(
        TournamentStage Stage,
        List<int> SurvivorTpIds,
        int ActualBracketSize,
        int? TargetAdvance,
        bool CanComplete,
        bool TournamentCompletionAvailable);

    internal static class BracketComputationHelper
    {
        internal static MatchDto MapToMatchDto(Match match, StageEvaluation stageEval, bool tournamentCompletionAvailable)
        {
            bool isBye = (match.Player1TpId == null && match.Player2TpId != null) ||
                         (match.Player1TpId != null && match.Player2TpId == null);

            string? scheduledDisplay = null;
            if (match.ScheduledUtc.HasValue)
            {
                scheduledDisplay = match.ScheduledUtc.Value.ToString("MMM dd, HH:mm\\h");
            }

            return new MatchDto
            {
                Id = match.Id,
                RoundNo = match.RoundNo,
                PositionInRound = match.PositionInRound,
                Bracket = match.Bracket,
                Status = match.Status,
                Player1 = match.Player1Tp != null ? new PlayerDto
                {
                    TpId = match.Player1Tp.Id,
                    Name = match.Player1Tp.DisplayName,
                    Seed = match.Player1Tp.Seed,
                    Country = match.Player1Tp.Country,
                    FargoRating = match.Player1Tp.SkillLevel
                } : null,
                Player2 = match.Player2Tp != null ? new PlayerDto
                {
                    TpId = match.Player2Tp.Id,
                    Name = match.Player2Tp.DisplayName,
                    Seed = match.Player2Tp.Seed,
                    Country = match.Player2Tp.Country,
                    FargoRating = match.Player2Tp.SkillLevel
                } : null,
                Winner = match.WinnerTp != null ? new PlayerDto
                {
                    TpId = match.WinnerTp.Id,
                    Name = match.WinnerTp.DisplayName,
                    Seed = match.WinnerTp.Seed,
                    Country = match.WinnerTp.Country,
                    FargoRating = match.WinnerTp.SkillLevel
                } : null,
                Player1SourceType = match.Player1SourceType,
                Player1SourceMatchId = match.Player1SourceMatchId,
                Player2SourceType = match.Player2SourceType,
                Player2SourceMatchId = match.Player2SourceMatchId,
                ScheduledUtc = match.ScheduledUtc,
                ScheduledDisplay = scheduledDisplay,
                TableId = match.TableId,
                TableLabel = match.Table?.Label,
                ScoreP1 = match.ScoreP1,
                ScoreP2 = match.ScoreP2,
                RaceTo = match.RaceTo,
                NextWinnerMatchId = match.NextWinnerMatchId,
                NextLoserMatchId = match.NextLoserMatchId,
                IsBye = isBye,
                StageId = match.StageId,
                StageNo = stageEval.Stage.StageNo,
                StageCompletionAvailable = stageEval.CanComplete && stageEval.Stage.Status != StageStatus.Completed,
                TournamentCompletionAvailable = tournamentCompletionAvailable,
                RowVersion = Convert.ToBase64String(match.RowVersion)
            };
        }

        internal static async Task<StageEvaluation> EvaluateStageAsync(ApplicationDbContext db, int stageId, CancellationToken ct)
        {
            var stage = await db.TournamentStages
                .Include(s => s.Tournament)
                    .ThenInclude(t => t.Stages)
                .FirstOrDefaultAsync(s => s.Id == stageId, ct)
                ?? throw new KeyNotFoundException("Stage not found.");

            var matches = await db.Matches
                .Include(m => m.Player1Tp)
                .Include(m => m.Player2Tp)
                .Include(m => m.WinnerTp)
                .Include(m => m.Table)
                .Where(m => m.StageId == stageId)
                .OrderBy(m => m.Bracket)
                .ThenBy(m => m.RoundNo)
                .ThenBy(m => m.PositionInRound)
                .ToListAsync(ct);

            return EvaluateStageState(stage.Tournament, stage, matches);
        }

        internal static StageEvaluation EvaluateStageState(Tournament tournament, TournamentStage stage, List<Match> matches)
        {
            var matchLookup = matches.ToDictionary(m => m.Id);
            var survivorSet = new HashSet<int>();
            var survivors = new List<int>();

            void AddSurvivor(int tpId)
            {
                if (survivorSet.Add(tpId))
                {
                    survivors.Add(tpId);
                }
            }

            foreach (var match in matches)
            {
                if (match.Status != MatchStatus.Completed)
                {
                    if (match.Player1TpId.HasValue) AddSurvivor(match.Player1TpId.Value);
                    if (match.Player2TpId.HasValue) AddSurvivor(match.Player2TpId.Value);
                    continue;
                }

                if (stage.AdvanceCount.HasValue && match.WinnerTpId.HasValue)
                {
                    var hasNext = match.NextWinnerMatchId.HasValue && matchLookup.ContainsKey(match.NextWinnerMatchId.Value);
                    if (!hasNext)
                    {
                        AddSurvivor(match.WinnerTpId.Value);
                    }
                }
            }

            var actualBracketSize = matches.Any() ? CalculateBracketSize(matches) : 0;

            int? targetAdvance = null;
            if (stage.AdvanceCount.HasValue)
            {
                var maxAdvance = Math.Max(1, actualBracketSize > 0 ? actualBracketSize / 2 : stage.AdvanceCount.Value);
                targetAdvance = Math.Min(stage.AdvanceCount.Value, maxAdvance);
            }

            bool hasActivePlayers = matches.Any(m => m.Status != MatchStatus.Completed && (m.Player1TpId.HasValue || m.Player2TpId.HasValue));

            bool canComplete = stage.Status != StageStatus.Completed && (
                targetAdvance.HasValue
                    ? survivors.Count == targetAdvance.Value && targetAdvance.Value > 0
                    : !hasActivePlayers);

            var maxStageNo = tournament.Stages.Max(s => s.StageNo);
            bool tournamentCompletionAvailable = canComplete && stage.StageNo == maxStageNo && (!tournament.IsMultiStage || stage.StageNo > 1 || stage.AdvanceCount is null);

            return new StageEvaluation(stage, survivors, actualBracketSize, targetAdvance, canComplete, tournamentCompletionAvailable);
        }

        private static int CalculateBracketSize(List<Match> matches)
        {
            var winnersMatches = matches.Where(m => m.Bracket == BracketSide.Winners || m.Bracket == BracketSide.Knockout).ToList();
            if (!winnersMatches.Any()) return 0;

            var firstRound = winnersMatches.Where(m => m.RoundNo == 1).ToList();
            return firstRound.Count * 2;
        }
    }
}
