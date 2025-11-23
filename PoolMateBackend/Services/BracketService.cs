using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PoolMate.Api.Common;
using PoolMate.Api.Data;
using PoolMate.Api.Dtos.Tournament;
using PoolMate.Api.Hubs;
using PoolMate.Api.Models;

namespace PoolMate.Api.Services
{
    public class BracketService : IBracketService
    {
        private readonly ApplicationDbContext _db;
        private readonly IMatchLockService _lockService;
        private readonly IHubContext<TournamentHub> _hubContext;
        private readonly ILogger<BracketService> _logger;
        private readonly Random _rng = new();

        public BracketService(
            ApplicationDbContext db,
            IMatchLockService lockService,
            IHubContext<TournamentHub> hubContext,
            ILogger<BracketService> logger)
        {
            _db = db;
            _lockService = lockService;
            _hubContext = hubContext;
            _logger = logger;
        }

        //Preview
        public async Task<BracketPreviewDto> PreviewAsync(int tournamentId, CancellationToken ct)
        {
            var t = await _db.Tournaments
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == tournamentId, ct)
                ?? throw new KeyNotFoundException("Tournament not found");

            var players = await _db.TournamentPlayers
                .Where(x => x.TournamentId == tournamentId)
                .Select(x => new PlayerSeed
                {
                    TpId = x.Id,
                    Name = x.DisplayName,
                    Seed = x.Seed,
                    Country = x.Country,
                    FargoRating = x.SkillLevel
                })
                .ToListAsync(ct);

            if (players.Count < 2)
                throw new InvalidOperationException("Bracket preview requires at least two players in the tournament.");

            if (t.IsMultiStage && t.AdvanceToStage2Count.HasValue)
            {
                var requiredPlayers = Math.Max(t.AdvanceToStage2Count.Value + 1, 5);
                if (players.Count < requiredPlayers)
                    throw new InvalidOperationException($"Multi-stage bracket requires at least {requiredPlayers} players for advance count {t.AdvanceToStage2Count.Value}.");
            }

            var optimalSize = GetOptimalBracketSize(players.Count);

            var finalSize = optimalSize;
            if (t.BracketSizeEstimate.HasValue && t.BracketSizeEstimate.Value >= optimalSize)
            {
                if (t.BracketSizeEstimate.Value <= optimalSize * 2)
                    finalSize = t.BracketSizeEstimate.Value;
            }

            var stage1Ordering = t.IsMultiStage ? t.Stage1Ordering : t.BracketOrdering;

            var stage1 = BuildStagePreview(
                stageNo: 1,
                type: t.BracketType,
                ordering: stage1Ordering,
                size: finalSize,
                players: players
            );

            StagePreviewDto? stage2 = null;
            if (t.IsMultiStage)
            {
                if (t.AdvanceToStage2Count is null || t.AdvanceToStage2Count <= 0)
                    throw new InvalidOperationException("AdvanceToStage2Count must be set for multi-stage.");

                if (t.AdvanceToStage2Count.Value < 4)
                    throw new InvalidOperationException("AdvanceToStage2Count must be at least 4 for multi-stage tournaments.");

                if ((t.AdvanceToStage2Count.Value & (t.AdvanceToStage2Count.Value - 1)) != 0)
                    throw new InvalidOperationException("AdvanceToStage2Count must be a power of two (4,8,16,...).");

                stage2 = BuildStagePreview(
                    stageNo: 2,
                    type: BracketType.SingleElimination,
                    ordering: t.Stage2Ordering,
                    size: t.AdvanceToStage2Count.Value,
                    players: null
                );
            }

            return new BracketPreviewDto
            {
                TournamentId = tournamentId,
                IsMultiStage = t.IsMultiStage,
                Stage1 = stage1,
                Stage2 = stage2
            };
        }

        public async Task CreateAsync(int tournamentId, CreateBracketRequest? request, CancellationToken ct)
        {
            try
            {
                var t = await _db.Tournaments
                    .Include(x => x.Stages)
                    .FirstOrDefaultAsync(x => x.Id == tournamentId, ct)
                    ?? throw new KeyNotFoundException("Tournament not found");

                var anyMatches = await _db.Matches.AnyAsync(m => m.TournamentId == tournamentId, ct);
                if (anyMatches) throw new InvalidOperationException("Bracket already created.");

                List<PlayerSeed?> slots;

                var stage1Ordering = t.IsMultiStage ? t.Stage1Ordering : t.BracketOrdering;


                if (request?.Type == BracketCreationType.Manual && request.ManualAssignments != null)
                {
                    // Manual bracket creation
                    await ValidateManualAssignments(tournamentId, request.ManualAssignments, ct);
                    slots = await ConvertAssignmentsToSlots(tournamentId, request.ManualAssignments, ct);
                }
                else
                {
                    // Automatic bracket creation (existing logic)
                    var players = await _db.TournamentPlayers
                        .Where(x => x.TournamentId == tournamentId)
                        .Select(x => new PlayerSeed { TpId = x.Id, Name = x.DisplayName, Seed = x.Seed })
                        .ToListAsync(ct);

                    if (players.Count < 2)
                        throw new InvalidOperationException("At least two players are required to create a bracket.");

                    if (t.IsMultiStage && t.AdvanceToStage2Count.HasValue)
                    {
                        var requiredPlayers = Math.Max(t.AdvanceToStage2Count.Value + 1, 5);
                        if (players.Count < requiredPlayers)
                            throw new InvalidOperationException($"Multi-stage bracket requires at least {requiredPlayers} players for advance count {t.AdvanceToStage2Count.Value}.");
                    }

                    var optimalSize = GetOptimalBracketSize(players.Count);
                    var finalSize = optimalSize;
                    if (t.BracketSizeEstimate.HasValue && t.BracketSizeEstimate.Value >= optimalSize)
                    {
                        if (t.BracketSizeEstimate.Value <= optimalSize * 2)
                            finalSize = t.BracketSizeEstimate.Value;
                    }

                    slots = MakeSlots(players, finalSize, stage1Ordering);
                }
                // Create Stage 1
                var s1 = new TournamentStage
                {
                    TournamentId = t.Id,
                    StageNo = 1,
                    Type = t.BracketType,
                    Status = StageStatus.NotStarted,
                    Ordering = stage1Ordering,
                    AdvanceCount = t.IsMultiStage ? t.AdvanceToStage2Count : null
                };
                _db.TournamentStages.Add(s1);
                await _db.SaveChangesAsync(ct);

                BuildAndPersistSingleOrDouble(
                    tournamentId: t.Id,
                    stage: s1,
                    type: t.BracketType,
                    slots: slots,
                    ct: ct);

                await ProcessAutoAdvancements(t.Id, ct);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"OUTER ERROR in CreateAsync: {ex.Message}");
                throw;
            }
        }

        public async Task<BracketDto> GetAsync(int tournamentId, CancellationToken ct)
        {
            await ProcessAutoAdvancements(tournamentId, ct);

            var tournament = await _db.Tournaments
                .Include(x => x.Stages)
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == tournamentId, ct)
                ?? throw new KeyNotFoundException("Tournament not found");

            var matches = await _db.Matches
                .Include(x => x.Player1Tp)
                .Include(x => x.Player2Tp)
                .Include(x => x.WinnerTp)
                .Include(x => x.Table)
                .Where(x => x.TournamentId == tournamentId)
                .OrderBy(x => x.StageId)
                .ThenBy(x => x.Bracket)
                .ThenBy(x => x.RoundNo)
                .ThenBy(x => x.PositionInRound)
                .AsNoTracking()
                .ToListAsync(ct);

            if (!matches.Any())
                throw new InvalidOperationException("Bracket not created yet.");

            var stages = new List<StageDto>();

            foreach (var stage in tournament.Stages.OrderBy(s => s.StageNo))
            {
                var stageMatches = matches.Where(m => m.StageId == stage.Id).ToList();
                var stageEval = EvaluateStageState(tournament, stage, stageMatches);

                var stageDto = new StageDto
                {
                    StageNo = stage.StageNo,
                    Type = stage.Type,
                    Ordering = stage.Ordering,
                    BracketSize = stageEval.ActualBracketSize,
                    Status = stage.Status,
                    CompletedAt = stage.CompletedAt,
                    AdvanceCount = stage.AdvanceCount,
                    CanComplete = stageEval.CanComplete
                };

                // Group by BracketSide
                var bracketGroups = stageMatches.GroupBy(m => m.Bracket);

                foreach (var bracketGroup in bracketGroups.OrderBy(g => g.Key))
                {
                    var bracketDto = new BracketSideDto
                    {
                        BracketSide = bracketGroup.Key
                    };

                    // Group by RoundNo
                    var roundGroups = bracketGroup.GroupBy(m => m.RoundNo).OrderBy(g => g.Key);

                    foreach (var roundGroup in roundGroups)
                    {
                        var roundDto = new RoundDto
                        {
                            RoundNo = roundGroup.Key
                        };

                        foreach (var match in roundGroup.OrderBy(m => m.PositionInRound))
                        {
                            roundDto.Matches.Add(MapToMatchDto(match, stageEval, stageEval.TournamentCompletionAvailable));
                        }

                        bracketDto.Rounds.Add(roundDto);
                    }

                    stageDto.Brackets.Add(bracketDto);
                }

                stages.Add(stageDto);
            }

            return new BracketDto
            {
                TournamentId = tournamentId,
                IsMultiStage = tournament.IsMultiStage,
                Stages = stages
            };
        }

        public async Task<BracketDto> GetFilteredAsync(int tournamentId, BracketFilterRequest filter, CancellationToken ct)
        {
            // Get full bracket first
            var fullBracket = await GetAsync(tournamentId, ct);

            // Apply filter
            return ApplyBracketFilter(fullBracket, filter);
        }

        public async Task<MatchDto> GetMatchAsync(int matchId, CancellationToken ct)
        {
            var match = await LoadMatchForScoringAsync(matchId, ct);
            var stageEval = await EvaluateStageAsync(match.StageId, ct);
            return MapToMatchDto(match, stageEval, stageEval.TournamentCompletionAvailable);
        }

        private BracketDto ApplyBracketFilter(BracketDto bracket, BracketFilterRequest filter)
        {
            var filteredBracket = new BracketDto
            {
                TournamentId = bracket.TournamentId,
                IsMultiStage = bracket.IsMultiStage,
                Stages = new List<StageDto>()
            };

            foreach (var stage in bracket.Stages)
            {
                var filteredStage = new StageDto
                {
                    StageNo = stage.StageNo,
                    Type = stage.Type,
                    Ordering = stage.Ordering,
                    BracketSize = stage.BracketSize,
                    Brackets = new List<BracketSideDto>()
                };

                foreach (var bracketSide in stage.Brackets)
                {
                    // Apply bracket side filter
                    if (ShouldIncludeBracketSide(bracketSide.BracketSide, filter))
                    {
                        var filteredBracketSide = new BracketSideDto
                        {
                            BracketSide = bracketSide.BracketSide,
                            Rounds = new List<RoundDto>()
                        };

                        foreach (var round in bracketSide.Rounds)
                        {
                            // Apply round filter
                            if (ShouldIncludeRound(round, bracketSide.BracketSide, filter))
                            {
                                filteredBracketSide.Rounds.Add(round);
                            }
                        }

                        if (filteredBracketSide.Rounds.Any())
                        {
                            filteredStage.Brackets.Add(filteredBracketSide);
                        }
                    }
                }

                if (filteredStage.Brackets.Any())
                {
                    filteredBracket.Stages.Add(filteredStage);
                }
            }

            return filteredBracket;
        }

        private bool ShouldIncludeBracketSide(BracketSide bracketSide, BracketFilterRequest filter)
        {
            return filter.FilterType switch
            {
                BracketFilterType.ShowAll => true,
                BracketFilterType.DrawRound => true,
                BracketFilterType.WinnerSide => bracketSide == BracketSide.Winners || bracketSide == BracketSide.Knockout,
                BracketFilterType.LoserSide => bracketSide == BracketSide.Losers,
                BracketFilterType.Finals => bracketSide == BracketSide.Finals,
                _ => true
            };
        }

        private bool ShouldIncludeRound(RoundDto round, BracketSide bracketSide, BracketFilterRequest filter)
        {
            return filter.FilterType switch
            {
                BracketFilterType.ShowAll => true,
                BracketFilterType.DrawRound => round.RoundNo == 1,
                BracketFilterType.WinnerSide => true,
                BracketFilterType.LoserSide => true,
                BracketFilterType.Finals => true,
                _ => true
            };
        }

        private static int CalculateBracketSize(List<Match> matches)
        {
            var winnersMatches = matches.Where(m => m.Bracket == BracketSide.Winners || m.Bracket == BracketSide.Knockout).ToList();
            if (!winnersMatches.Any()) return 0;

            var firstRound = winnersMatches.Where(m => m.RoundNo == 1).ToList();
            return firstRound.Count * 2;
        }

        private MatchDto MapToMatchDto(Match match, StageEvaluation stageEval, bool tournamentCompletionAvailable)
        {
            bool isBye = (match.Player1TpId == null && match.Player2TpId != null) ||
                         (match.Player1TpId != null && match.Player2TpId == null);

            // ✅ Format scheduled time
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

                // ✅ Enhanced player data with country
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

        private async Task ProcessAutoAdvancements(int tournamentId, CancellationToken ct)
        {
            var matches = await _db.Matches
                .Where(m => m.TournamentId == tournamentId)
                .Include(m => m.Player1Tp)
                .Include(m => m.Player2Tp)
                .ToListAsync(ct);

            if (!matches.Any())
                return;

            var matchDict = matches.ToDictionary(m => m.Id, m => m);
            var queue = new Queue<Match>(matches.Where(m => m.Status == MatchStatus.NotStarted));
            var changed = false;

            while (queue.Count > 0)
            {
                var match = queue.Dequeue();

                if (TryCompleteMatchByBye(match, matchDict))
                {
                    changed = true;

                    if (match.NextWinnerMatchId.HasValue &&
                        matchDict.TryGetValue(match.NextWinnerMatchId.Value, out var nextWinnerMatch))
                    {
                        queue.Enqueue(nextWinnerMatch);
                    }

                    if (match.NextLoserMatchId.HasValue &&
                        matchDict.TryGetValue(match.NextLoserMatchId.Value, out var nextLoserMatch))
                    {
                        queue.Enqueue(nextLoserMatch);
                    }
                }
            }

            if (changed)
                await _db.SaveChangesAsync(ct);
        }

        public async Task<MatchDto> UpdateMatchAsync(int matchId, UpdateMatchRequest request, CancellationToken ct)
        {
            if (request is null)
                throw new ArgumentNullException(nameof(request));

            var match = await LoadMatchAggregateAsync(matchId, ct);

            if (match.Stage.Status == StageStatus.Completed)
                throw new InvalidOperationException("Stage has been completed and cannot be modified.");

            if (match.Status == MatchStatus.Completed)
                throw new InvalidOperationException("Match already completed. Use correct-result workflow.");

            ValidateScoreInput(match, request.ScoreP1, request.ScoreP2, request.RaceTo);

            if (request.RaceTo.HasValue)
            {
                if (request.RaceTo.Value <= 0)
                    throw new InvalidOperationException("Race-to value must be positive.");
                match.RaceTo = request.RaceTo;
            }

            if (request.ScheduledUtc.HasValue)
                match.ScheduledUtc = request.ScheduledUtc;

            var previousStatus = match.Status;
            var previousTableId = match.TableId;

            if (request.TableId.HasValue)
            {
                if (match.TableId != request.TableId.Value)
                {
                    var table = await ValidateTableSelectionAsync(match, request.TableId.Value, ct);
                    match.TableId = table.Id;
                    match.Table = table;
                }
            }
            else if (match.TableId.HasValue)
            {
                match.TableId = null;
                match.Table = null;
            }

            match.ScoreP1 = request.ScoreP1;
            match.ScoreP2 = request.ScoreP2;

            int? winnerTpId = request.WinnerTpId;
            if (winnerTpId.HasValue)
            {
                EnsureWinnerBelongsToMatch(match, winnerTpId.Value);
            }
            else
            {
                winnerTpId = InferWinnerFromScores(match);
            }

            var hasProgress = HasProgress(match.ScoreP1, match.ScoreP2, match.TableId);

            if ((winnerTpId.HasValue || hasProgress) &&
                match.Player1TpId.HasValue &&
                match.Player2TpId.HasValue &&
                match.Player1TpId == match.Player2TpId)
            {
                throw new InvalidOperationException("Cannot progress match because the same player occupies both slots.");
            }

            match.WinnerTpId = winnerTpId;
            match.WinnerTp = winnerTpId switch
            {
                not null and var id when id == match.Player1TpId => match.Player1Tp,
                not null and var id when id == match.Player2TpId => match.Player2Tp,
                _ => null
            };

            if (winnerTpId.HasValue)
            {
                match.Status = MatchStatus.Completed;
            }
            else if (hasProgress)
            {
                match.Status = MatchStatus.InProgress;
            }
            else
            {
                match.Status = MatchStatus.NotStarted;
            }

            await UpdateTableUsageAsync(match, previousTableId, previousStatus, ct);

            Dictionary<int, Match>? matchDict = null;
            if (match.Status == MatchStatus.Completed)
            {
                matchDict = await LoadTournamentMatchesDictionaryAsync(match.TournamentId, ct);
                PropagateResult(match, matchDict);
            }

            await _db.SaveChangesAsync(ct);

            if (matchDict is not null)
                await ProcessAutoAdvancements(match.TournamentId, ct);

            var stageEval = await EvaluateStageAsync(match.StageId, ct);
            var dto = MapToMatchDto(match, stageEval, stageEval.TournamentCompletionAvailable);

            // Broadcast realtime updates
            await PublishRealtimeUpdates(match, dto, bracketChanged: match.Status == MatchStatus.Completed, ct);

            return dto;
        }

        public async Task<MatchDto> CorrectMatchResultAsync(int matchId, CorrectMatchResultRequest request, CancellationToken ct)
        {
            if (request is null)
                throw new ArgumentNullException(nameof(request));

            var match = await LoadMatchAggregateAsync(matchId, ct);

            if (match.Stage.Status == StageStatus.Completed)
                throw new InvalidOperationException("Stage has been completed and cannot be modified.");

            if (match.WinnerTpId is null)
                throw new InvalidOperationException("Match has no recorded result to correct.");

            EnsureWinnerBelongsToMatch(match, request.WinnerTpId);
            ValidateScoreInput(match, request.ScoreP1, request.ScoreP2, request.RaceTo);

            if (request.RaceTo.HasValue)
            {
                if (request.RaceTo.Value <= 0)
                    throw new InvalidOperationException("Race-to value must be positive.");
                match.RaceTo = request.RaceTo;
            }

            var previousStatus = match.Status;
            var previousTableId = match.TableId;

            var matchDict = await LoadTournamentMatchesDictionaryAsync(match.TournamentId, ct);
            var dependencyMap = BuildDependencyMap(matchDict);

            var processed = new HashSet<int>();
            var tableSnapshot = new Dictionary<int, (int? TableId, MatchStatus PreviousStatus)>();

            RewindCascade(match.Id, dependencyMap, processed, tableSnapshot);

            match.ScoreP1 = request.ScoreP1;
            match.ScoreP2 = request.ScoreP2;
            match.WinnerTpId = request.WinnerTpId;
            match.WinnerTp = request.WinnerTpId == match.Player1TpId ? match.Player1Tp :
                              request.WinnerTpId == match.Player2TpId ? match.Player2Tp : null;
            match.Status = MatchStatus.Completed;

            foreach (var entry in tableSnapshot)
            {
                await UpdateTableUsageAsync(matchDict[entry.Key], entry.Value.TableId, entry.Value.PreviousStatus, ct);
            }

            await UpdateTableUsageAsync(match, previousTableId, previousStatus, ct);

            PropagateResult(match, matchDict);

            await _db.SaveChangesAsync(ct);
            await ProcessAutoAdvancements(match.TournamentId, ct);

            var stageEval = await EvaluateStageAsync(match.StageId, ct);
            var dto = MapToMatchDto(match, stageEval, stageEval.TournamentCompletionAvailable);

            // Broadcast realtime updates
            await PublishRealtimeUpdates(match, dto, bracketChanged: true, ct);

            return dto;
        }

        public async Task<StageCompletionResultDto> CompleteStageAsync(int tournamentId, int stageNo, CompleteStageRequest request, CancellationToken ct)
        {
            var tournament = await _db.Tournaments
                .Include(t => t.Stages)
                .FirstOrDefaultAsync(t => t.Id == tournamentId, ct)
                ?? throw new KeyNotFoundException("Tournament not found.");

            var stage = tournament.Stages.FirstOrDefault(s => s.StageNo == stageNo)
                ?? throw new InvalidOperationException("Stage not found.");

            if (stage.Status == StageStatus.Completed)
                throw new InvalidOperationException("Stage already completed.");

            var stageMatches = await _db.Matches
                .Include(m => m.Player1Tp)
                .Include(m => m.Player2Tp)
                .Include(m => m.WinnerTp)
                .Where(m => m.StageId == stage.Id)
                .OrderBy(m => m.Bracket)
                .ThenBy(m => m.RoundNo)
                .ThenBy(m => m.PositionInRound)
                .ToListAsync(ct);

            var stageEval = EvaluateStageState(tournament, stage, stageMatches);

            if (!stageEval.CanComplete)
                throw new InvalidOperationException("Stage cannot be completed yet.");

            var now = DateTime.UtcNow;

            TournamentStage? createdStage = null;
            bool createdStage2 = false;
            int? stage2Id = null;

            if (tournament.IsMultiStage && stage.StageNo == 1 && stage.AdvanceCount.HasValue)
            {
                if (stageEval.TargetAdvance is null || stageEval.SurvivorTpIds.Count != stageEval.TargetAdvance.Value)
                    throw new InvalidOperationException("Stage survivors do not match the configured advance count.");

                var survivors = stageEval.SurvivorTpIds;
                var stage2Ordering = tournament.Stage2Ordering;
                var stage2Size = stageEval.TargetAdvance.Value;

                if (stage2Size < 4)
                    throw new InvalidOperationException("Stage 2 requires at least four advancing players.");

                if ((stage2Size & (stage2Size - 1)) != 0)
                    throw new InvalidOperationException("Stage 2 bracket size must be a power of two.");

                var existingStage2 = tournament.Stages.FirstOrDefault(s => s.StageNo == 2);
                if (existingStage2 is not null)
                {
                    stage2Id = existingStage2.Id;

                    var existingStage2MatchCount = await _db.Matches.CountAsync(m => m.StageId == existingStage2.Id, ct);
                    if (existingStage2MatchCount == 0)
                    {
                        var playerSeeds = await GetPlayerSeedsAsync(survivors, ct);

                        List<PlayerSeed?> slots;
                        if (request?.Stage2?.Type == BracketCreationType.Manual && request.Stage2.ManualAssignments is not null)
                        {
                            slots = await ConvertAssignmentsToSlotsForStage2(tournamentId, request.Stage2.ManualAssignments, survivors, ct);
                        }
                        else
                        {
                            slots = MakeSlots(playerSeeds, stage2Size, stage2Ordering);
                        }

                        if (slots.Any(s => s is null))
                            throw new InvalidOperationException("Stage 2 requires complete seeding.");

                        BuildAndPersistSingleOrDouble(tournament.Id, existingStage2, BracketType.SingleElimination, slots, ct);
                        existingStage2.UpdatedAt = now;
                        createdStage2 = true;
                    }
                }
                else
                {
                    var playerSeeds = await GetPlayerSeedsAsync(survivors, ct);

                    List<PlayerSeed?> slots;
                    if (request?.Stage2?.Type == BracketCreationType.Manual && request.Stage2.ManualAssignments is not null)
                    {
                        slots = await ConvertAssignmentsToSlotsForStage2(tournamentId, request.Stage2.ManualAssignments, survivors, ct);
                    }
                    else
                    {
                        slots = MakeSlots(playerSeeds, stage2Size, stage2Ordering);
                    }

                    if (slots.Any(s => s is null))
                        throw new InvalidOperationException("Stage 2 requires complete seeding.");

                    createdStage = new TournamentStage
                    {
                        TournamentId = tournament.Id,
                        StageNo = stage.StageNo + 1,
                        Type = BracketType.SingleElimination,
                        Status = StageStatus.NotStarted,
                        Ordering = stage2Ordering,
                        AdvanceCount = null,
                        CreatedAt = now,
                        UpdatedAt = now
                    };

                    _db.TournamentStages.Add(createdStage);
                    await _db.SaveChangesAsync(ct);

                    BuildAndPersistSingleOrDouble(tournament.Id, createdStage, BracketType.SingleElimination, slots, ct);
                    createdStage2 = true;
                    stage2Id = createdStage.Id;
                    tournament.Stages.Add(createdStage);
                    createdStage.Tournament = tournament;
                }
            }

            bool tournamentCompleted = false;
            stage.Status = StageStatus.Completed;
            stage.CompletedAt = now;
            stage.UpdatedAt = now;
            tournament.UpdatedAt = now;

            if (stageEval.TournamentCompletionAvailable)
            {
                tournament.Status = TournamentStatus.Completed;
                tournament.EndUtc = now;
                tournamentCompleted = true;
            }

            await _db.SaveChangesAsync(ct);

            if (createdStage2)
            {
                await ProcessAutoAdvancements(tournament.Id, ct);
            }

            return new StageCompletionResultDto
            {
                StageNo = stage.StageNo,
                Status = stage.Status,
                CompletedAt = stage.CompletedAt,
                CreatedStage2 = createdStage2,
                Stage2Id = stage2Id,
                TournamentCompleted = tournamentCompleted
            };
        }

        public async Task<TournamentStatusSummaryDto> GetTournamentStatusAsync(int tournamentId, CancellationToken ct)
        {
            await ProcessAutoAdvancements(tournamentId, ct);

            var tournament = await _db.Tournaments
                .Include(t => t.Tables)
                .Include(t => t.Stages)
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.Id == tournamentId, ct)
                ?? throw new KeyNotFoundException("Tournament not found.");

            var matches = await _db.Matches
                .Where(m => m.TournamentId == tournamentId)
                .Include(m => m.Stage)
                .Include(m => m.Player1Tp)
                .Include(m => m.Player2Tp)
                .Include(m => m.WinnerTp)
                .AsNoTracking()
                .ToListAsync(ct);

            var summary = new TournamentStatusSummaryDto
            {
                TournamentId = tournament.Id,
                Status = tournament.Status,
                IsStarted = tournament.IsStarted,
                StartUtc = tournament.StartUtc,
                EndUtc = tournament.EndUtc
            };

            if (tournament.IsStarted)
            {
                var effectiveEnd = tournament.EndUtc ?? DateTime.UtcNow;
                if (effectiveEnd > tournament.StartUtc)
                    summary.Runtime = effectiveEnd - tournament.StartUtc;
            }

            var matchesTotal = matches.Count;
            var matchesCompleted = matches.Count(m => m.Status == MatchStatus.Completed);
            var matchesInProgress = matches.Count(m => m.Status == MatchStatus.InProgress);
            var matchesNotStarted = matches.Count(m => m.Status == MatchStatus.NotStarted && !m.ScheduledUtc.HasValue);
            var matchesScheduled = matches.Count(m => m.Status == MatchStatus.NotStarted && m.ScheduledUtc.HasValue);

            summary.Matches.Total = matchesTotal;
            summary.Matches.Completed = matchesCompleted;
            summary.Matches.InProgress = matchesInProgress;
            summary.Matches.NotStarted = matchesNotStarted;
            summary.Matches.Scheduled = matchesScheduled;
            summary.Matches.WinnersSide = matches.Count(m => m.Bracket == BracketSide.Winners);
            summary.Matches.LosersSide = matches.Count(m => m.Bracket == BracketSide.Losers);
            summary.Matches.KnockoutSide = matches.Count(m => m.Bracket == BracketSide.Knockout);
            summary.Matches.FinalsSide = matches.Count(m => m.Bracket == BracketSide.Finals);

            summary.CompletionPercent = matchesTotal == 0
                ? 0
                : Math.Round((double)matchesCompleted / matchesTotal * 100, 2);

            var tables = tournament.Tables.ToList();
            summary.Tables.Total = tables.Count;
            summary.Tables.Open = tables.Count(t => t.Status == TableStatus.Open);
            summary.Tables.InUse = tables.Count(t => t.Status == TableStatus.InUse);
            summary.Tables.Closed = tables.Count(t => t.Status == TableStatus.Closed);

            var activeMatchesWithTables = matches
                .Where(m => m.TableId.HasValue && m.Status != MatchStatus.Completed)
                .ToList();

            summary.Tables.AssignedTables = activeMatchesWithTables
                .Select(m => m.TableId!.Value)
                .Distinct()
                .Count();
            summary.Tables.MatchesOnWinnersSide = activeMatchesWithTables.Count(m => m.Bracket == BracketSide.Winners);
            summary.Tables.MatchesOnLosersSide = activeMatchesWithTables.Count(m => m.Bracket == BracketSide.Losers);
            summary.Tables.MatchesOnKnockoutSide = activeMatchesWithTables.Count(m => m.Bracket == BracketSide.Knockout);
            summary.Tables.MatchesOnFinalsSide = activeMatchesWithTables.Count(m => m.Bracket == BracketSide.Finals);

            var playerStats = (await GetPlayerStatsAsync(tournamentId, ct)).ToList();
            var statsLookup = playerStats.ToDictionary(p => p.TournamentPlayerId, p => p);
            summary.ActivePlayers = playerStats.Count(p => !p.IsEliminated);
            summary.EliminatedPlayers = playerStats.Count - summary.ActivePlayers;

            var placementMatches = matches.Select(ToPlacementMatch).ToList();
            var (championDto, runnerUpDto, additionalPlacements) = ApplyPlacements(placementMatches, statsLookup);

            summary.Champion = championDto;
            summary.RunnerUp = runnerUpDto;
            if (additionalPlacements.Count > 0)
                summary.AdditionalPlacements.AddRange(additionalPlacements);

            return summary;
        }

        public async Task<MatchScoreUpdateResponse> UpdateLiveScoreAsync(int matchId, UpdateLiveScoreRequest request, ScoringContext actor, CancellationToken ct)
        {
            if (request is null)
                throw new ArgumentNullException(nameof(request));
            if (actor is null)
                throw new ArgumentNullException(nameof(actor));

            var lockResult = _lockService.AcquireOrRefresh(matchId, actor.ActorId, request.LockId);
            if (!lockResult.Granted)
                throw new MatchLockedException(lockResult.LockId, lockResult.ExpiresAt);

            try
            {
                var match = await LoadMatchForScoringAsync(matchId, ct);
                ValidateScoringAccess(match, actor, ensureNotCompleted: true);
                ApplyRowVersion(match, request.RowVersion);
                ValidateScoreInput(match, request.ScoreP1, request.ScoreP2, request.RaceTo);

                if (request.RaceTo.HasValue)
                    match.RaceTo = request.RaceTo;

                var previousStatus = match.Status;
                var previousTableId = match.TableId;

                if (request.ScoreP1.HasValue)
                    match.ScoreP1 = request.ScoreP1;
                if (request.ScoreP2.HasValue)
                    match.ScoreP2 = request.ScoreP2;

                if (!HasProgress(match.ScoreP1, match.ScoreP2, match.TableId))
                {
                    match.Status = MatchStatus.NotStarted;
                    match.WinnerTpId = null;
                    match.WinnerTp = null;
                }
                else if (match.Status != MatchStatus.Completed)
                {
                    match.Status = MatchStatus.InProgress;
                    match.WinnerTpId = null;
                    match.WinnerTp = null;
                }

                await UpdateTableUsageAsync(match, previousTableId, previousStatus, ct);
                await SaveChangesWithConcurrencyAsync(match, ct);

                var stageEval = await EvaluateStageAsync(match.StageId, ct);
                var dto = MapToMatchDto(match, stageEval, stageEval.TournamentCompletionAvailable);

                // Broadcast bracket update on score changes to keep bracket view in sync
                await PublishRealtimeUpdates(match, dto, bracketChanged: true, ct);

                return new MatchScoreUpdateResponse
                {
                    Match = dto,
                    LockId = lockResult.LockId,
                    LockExpiresAt = lockResult.ExpiresAt,
                    IsCompleted = match.Status == MatchStatus.Completed
                };
            }
            catch (ConcurrencyConflictException)
            {
                _lockService.Release(matchId, lockResult.LockId, actor.ActorId);
                throw;
            }
            catch
            {
                _lockService.Release(matchId, lockResult.LockId, actor.ActorId);
                throw;
            }
        }

        public async Task<MatchScoreUpdateResponse> CompleteMatchAsync(int matchId, CompleteMatchRequest request, ScoringContext actor, CancellationToken ct)
        {
            if (request is null)
                throw new ArgumentNullException(nameof(request));
            if (actor is null)
                throw new ArgumentNullException(nameof(actor));

            var lockResult = _lockService.AcquireOrRefresh(matchId, actor.ActorId, request.LockId);
            if (!lockResult.Granted)
                throw new MatchLockedException(lockResult.LockId, lockResult.ExpiresAt);

            try
            {
                var match = await LoadMatchForScoringAsync(matchId, ct);
                ValidateScoringAccess(match, actor, ensureNotCompleted: false);

                ApplyRowVersion(match, request.RowVersion);
                ValidateScoreInput(match, request.ScoreP1, request.ScoreP2, request.RaceTo);

                if (request.ScoreP1.HasValue)
                    match.ScoreP1 = request.ScoreP1;
                if (request.ScoreP2.HasValue)
                    match.ScoreP2 = request.ScoreP2;
                if (request.RaceTo.HasValue)
                    match.RaceTo = request.RaceTo;

                if (!match.Player1TpId.HasValue || !match.Player2TpId.HasValue)
                    throw new InvalidOperationException("Both players must be assigned before completing the match.");

                var raceTo = match.RaceTo ?? throw new InvalidOperationException("Race-to value must be set before completing the match.");

                if (!match.ScoreP1.HasValue || !match.ScoreP2.HasValue)
                    throw new InvalidOperationException("Both scores must be provided before completing the match.");

                var winnerTpId = DetermineWinnerFromScores(
                    match.ScoreP1.Value,
                    match.ScoreP2.Value,
                    match.Player1TpId.Value,
                    match.Player2TpId.Value,
                    raceTo);

                var previousStatus = match.Status;
                var previousTableId = match.TableId;

                match.WinnerTpId = winnerTpId;
                match.WinnerTp = winnerTpId == match.Player1TpId ? match.Player1Tp : match.Player2Tp;
                match.Status = MatchStatus.Completed;

                await UpdateTableUsageAsync(match, previousTableId, previousStatus, ct);

                var matchDict = await LoadTournamentMatchesDictionaryAsync(match.TournamentId, ct);
                PropagateResult(match, matchDict);

                await SaveChangesWithConcurrencyAsync(match, ct);
                await ProcessAutoAdvancements(match.TournamentId, ct);

                var stageEval = await EvaluateStageAsync(match.StageId, ct);
                var dto = MapToMatchDto(match, stageEval, stageEval.TournamentCompletionAvailable);

                await PublishRealtimeUpdates(match, dto, bracketChanged: true, ct);

                return new MatchScoreUpdateResponse
                {
                    Match = dto,
                    LockId = lockResult.LockId,
                    LockExpiresAt = lockResult.ExpiresAt,
                    IsCompleted = true
                };
            }
            catch (ConcurrencyConflictException)
            {
                throw;
            }
            finally
            {
                _lockService.Release(matchId, lockResult.LockId, actor.ActorId);
            }
        }

        public async Task ResetAsync(int tournamentId, CancellationToken ct)
        {
            var tournament = await _db.Tournaments
                .Include(t => t.Stages)
                .Include(t => t.Matches)
                .FirstOrDefaultAsync(t => t.Id == tournamentId, ct)
                ?? throw new KeyNotFoundException("Tournament not found.");

            var canReset = (!tournament.IsStarted && tournament.Status == TournamentStatus.Upcoming) ||
                           tournament.Status == TournamentStatus.Completed;

            if (!canReset)
                throw new InvalidOperationException("Bracket can only be reset before the tournament starts or after it has completed.");

            if (tournament.Matches.Count == 0 && tournament.Stages.Count == 0)
                return;

            _db.Matches.RemoveRange(tournament.Matches);
            _db.TournamentStages.RemoveRange(tournament.Stages);

            tournament.IsStarted = false;
            tournament.Status = TournamentStatus.Upcoming;
            tournament.EndUtc = null;
            tournament.UpdatedAt = DateTime.UtcNow;

            await _db.SaveChangesAsync(ct);
        }

        public async Task<IReadOnlyList<TournamentPlayerStatsDto>> GetPlayerStatsAsync(int tournamentId, CancellationToken ct)
        {
            var players = await _db.TournamentPlayers
                .Where(tp => tp.TournamentId == tournamentId)
                .Select(tp => new
                {
                    tp.Id,
                    tp.DisplayName,
                    tp.Seed
                })
                .ToListAsync(ct);

            if (players.Count == 0)
                return Array.Empty<TournamentPlayerStatsDto>();

            var stats = players.ToDictionary(
                p => p.Id,
                p => new TournamentPlayerStatsDto
                {
                    TournamentPlayerId = p.Id,
                    DisplayName = p.DisplayName,
                    Seed = p.Seed,
                    MatchesPlayed = 0,
                    Wins = 0,
                    Losses = 0,
                    RacksWon = 0,
                    RacksLost = 0,
                    LastStageNo = null,
                    IsEliminated = true
                });

            var matches = await _db.Matches
                .Where(m => m.TournamentId == tournamentId)
                .Select(m => new
                {
                    m.Id,
                    m.RoundNo,
                    StageNo = m.Stage.StageNo,
                    m.Bracket,
                    m.Player1TpId,
                    m.Player2TpId,
                    m.WinnerTpId,
                    m.ScoreP1,
                    m.ScoreP2,
                    m.Status,
                    m.NextWinnerMatchId,
                    m.NextLoserMatchId
                })
                .ToListAsync(ct);

            var activePlayers = new HashSet<int>();

            foreach (var match in matches)
            {
                var isCompleted = match.Status == MatchStatus.Completed;
                var hasBothPlayers = match.Player1TpId.HasValue && match.Player2TpId.HasValue;

                if (match.Player1TpId.HasValue && stats.TryGetValue(match.Player1TpId.Value, out var p1))
                {
                    if (!p1.LastStageNo.HasValue || match.StageNo > p1.LastStageNo.Value)
                        p1.LastStageNo = match.StageNo;

                    if (isCompleted && hasBothPlayers)
                    {
                        p1.MatchesPlayed++;
                        if (match.ScoreP1.HasValue) p1.RacksWon += match.ScoreP1.Value;
                        if (match.ScoreP2.HasValue) p1.RacksLost += match.ScoreP2.Value;

                        if (match.WinnerTpId == match.Player1TpId)
                            p1.Wins++;
                        else if (match.WinnerTpId == match.Player2TpId)
                            p1.Losses++;
                    }

                    if (!isCompleted)
                        activePlayers.Add(match.Player1TpId.Value);
                }

                if (match.Player2TpId.HasValue && stats.TryGetValue(match.Player2TpId.Value, out var p2))
                {
                    if (!p2.LastStageNo.HasValue || match.StageNo > p2.LastStageNo.Value)
                        p2.LastStageNo = match.StageNo;

                    if (isCompleted && hasBothPlayers)
                    {
                        p2.MatchesPlayed++;
                        if (match.ScoreP2.HasValue) p2.RacksWon += match.ScoreP2.Value;
                        if (match.ScoreP1.HasValue) p2.RacksLost += match.ScoreP1.Value;

                        if (match.WinnerTpId == match.Player2TpId)
                            p2.Wins++;
                        else if (match.WinnerTpId == match.Player1TpId)
                            p2.Losses++;
                    }

                    if (!isCompleted)
                        activePlayers.Add(match.Player2TpId.Value);
                }
            }

            foreach (var id in activePlayers)
            {
                if (stats.TryGetValue(id, out var stat))
                    stat.IsEliminated = false;
            }

            var champions = matches
                .Where(m => m.Status == MatchStatus.Completed && m.WinnerTpId.HasValue)
                .Where(m => m.NextWinnerMatchId == null && m.NextLoserMatchId == null)
                .Where(m => m.Bracket == BracketSide.Knockout || m.Bracket == BracketSide.Finals)
                .Select(m => m.WinnerTpId!.Value)
                .Distinct();

            foreach (var championId in champions)
            {
                if (stats.TryGetValue(championId, out var stat))
                    stat.IsEliminated = false;
            }

            var placementMatches = matches
                .Select(m => new PlacementMatch(
                    m.Id,
                    m.StageNo,
                    m.Bracket,
                    m.RoundNo,
                    m.Player1TpId,
                    m.Player2TpId,
                    m.WinnerTpId,
                    m.Status,
                    m.NextWinnerMatchId,
                    m.NextLoserMatchId))
                .ToList();

            _ = ApplyPlacements(placementMatches, stats);

            return stats.Values
                .OrderBy(s => s.PlacementRank ?? int.MaxValue)
                .ThenBy(s => s.Seed ?? int.MaxValue)
                .ThenBy(s => s.DisplayName)
                .ToList();
        }

        public async Task<IReadOnlyList<string>> GetBracketDebugViewAsync(int tournamentId, CancellationToken ct)
        {
            var bracket = await GetAsync(tournamentId, ct);
            var lines = new List<string>
            {
                $"Tournament {bracket.TournamentId} | MultiStage: {bracket.IsMultiStage}"
            };

            foreach (var stage in bracket.Stages.OrderBy(s => s.StageNo))
            {
                var stageHeader = $"Stage {stage.StageNo} [{stage.Type}] Status: {stage.Status} Size: {stage.BracketSize}";
                if (stage.AdvanceCount.HasValue)
                    stageHeader += $" | Advance: {stage.AdvanceCount.Value}";
                if (stage.CompletedAt.HasValue)
                    stageHeader += $" | CompletedAt: {stage.CompletedAt:yyyy-MM-dd HH:mm}";
                if (stage.CanComplete)
                    stageHeader += " | ReadyToComplete";

                lines.Add(stageHeader);

                foreach (var bracketSide in stage.Brackets.OrderBy(b => b.BracketSide))
                {
                    lines.Add($"  [{bracketSide.BracketSide}]");

                    foreach (var round in bracketSide.Rounds.OrderBy(r => r.RoundNo))
                    {
                        lines.Add($"    Round {round.RoundNo}");

                        foreach (var match in round.Matches.OrderBy(m => m.PositionInRound))
                        {
                            lines.Add(FormatDebugLine(match));
                        }
                    }
                }
            }

            return lines;
        }

        private async Task<Match> LoadMatchAggregateAsync(int matchId, CancellationToken ct)
        {
            var match = await _db.Matches
                .Include(m => m.Player1Tp)
                .Include(m => m.Player2Tp)
                .Include(m => m.WinnerTp)
                .Include(m => m.Table)
                .Include(m => m.Stage)
                .FirstOrDefaultAsync(m => m.Id == matchId, ct);

            return match ?? throw new KeyNotFoundException("Match not found.");
        }

        private static void EnsureWinnerBelongsToMatch(Match match, int winnerTpId)
        {
            if (match.Player1TpId != winnerTpId && match.Player2TpId != winnerTpId)
                throw new InvalidOperationException("Winner must be one of the players in the match.");
        }

        private static void ValidateScoreInput(Match match, int? scoreP1, int? scoreP2, int? nextRaceTo)
        {
            if (scoreP1.HasValue && scoreP1.Value < 0)
                throw new InvalidOperationException("Scores must be non-negative.");
            if (scoreP2.HasValue && scoreP2.Value < 0)
                throw new InvalidOperationException("Scores must be non-negative.");

            if (scoreP1.HasValue && match.Player1TpId is null)
                throw new InvalidOperationException("Cannot set score for an empty player slot.");
            if (scoreP2.HasValue && match.Player2TpId is null)
                throw new InvalidOperationException("Cannot set score for an empty player slot.");

            var raceTo = nextRaceTo ?? match.RaceTo;
            if (raceTo.HasValue)
            {
                if (scoreP1.HasValue && scoreP1.Value > raceTo.Value)
                    throw new InvalidOperationException("Score cannot exceed the race-to target.");
                if (scoreP2.HasValue && scoreP2.Value > raceTo.Value)
                    throw new InvalidOperationException("Score cannot exceed the race-to target.");
            }
        }

        private static bool HasProgress(int? scoreP1, int? scoreP2, int? tableId)
            => scoreP1.HasValue || scoreP2.HasValue || tableId.HasValue;

        private static int? InferWinnerFromScores(Match match)
        {
            if (!match.RaceTo.HasValue)
                return null;

            var race = match.RaceTo.Value;
            var p1Wins = match.Player1TpId.HasValue && match.ScoreP1 == race;
            var p2Wins = match.Player2TpId.HasValue && match.ScoreP2 == race;

            if (p1Wins == p2Wins)
                return null;

            return p1Wins ? match.Player1TpId : match.Player2TpId;
        }

        private async Task<Dictionary<int, Match>> LoadTournamentMatchesDictionaryAsync(int tournamentId, CancellationToken ct)
        {
            var matches = await _db.Matches
                .Where(m => m.TournamentId == tournamentId)
                .Include(m => m.Player1Tp)
                .Include(m => m.Player2Tp)
                .Include(m => m.WinnerTp)
                .ToListAsync(ct);

            return matches.ToDictionary(m => m.Id);
        }

        private void PropagateResult(Match match, Dictionary<int, Match> matchDict)
        {
            if (match.WinnerTpId is null)
                return;

            if (match.NextWinnerMatchId.HasValue && matchDict.TryGetValue(match.NextWinnerMatchId.Value, out var winnerMatch))
            {
                AssignPlayerToSlot(winnerMatch, MatchSlotSourceType.WinnerOf, match.Id, match.WinnerTpId.Value);
            }

            var loser = DetermineLoser(match);
            if (loser.HasValue && match.NextLoserMatchId.HasValue && matchDict.TryGetValue(match.NextLoserMatchId.Value, out var loserMatch))
            {
                AssignPlayerToSlot(loserMatch, MatchSlotSourceType.LoserOf, match.Id, loser.Value);
            }
        }

        private static int? DetermineLoser(Match match)
        {
            if (!match.WinnerTpId.HasValue)
                return null;

            if (match.Player1TpId.HasValue && match.WinnerTpId == match.Player1TpId)
                return match.Player2TpId;

            if (match.Player2TpId.HasValue && match.WinnerTpId == match.Player2TpId)
                return match.Player1TpId;

            return null;
        }

        private static TournamentPlacementDto CreatePlacementDto(
            int playerId,
            string placementLabel,
            int placementRank,
            Dictionary<int, TournamentPlayerStatsDto> statsLookup)
        {
            if (!statsLookup.TryGetValue(playerId, out var stats))
                throw new InvalidOperationException($"Unable to locate player {playerId} for placement '{placementLabel}'.");

            stats.PlacementRank = placementRank;
            stats.PlacementLabel = placementLabel;

            return new TournamentPlacementDto
            {
                Placement = placementLabel,
                PlacementRank = placementRank,
                TournamentPlayerId = stats.TournamentPlayerId,
                DisplayName = stats.DisplayName,
                Seed = stats.Seed,
                MatchesPlayed = stats.MatchesPlayed,
                Wins = stats.Wins,
                Losses = stats.Losses
            };
        }

        private static void AssignRemainingPlacements(
            Dictionary<int, TournamentPlayerStatsDto> statsLookup,
            HashSet<int> placedPlayerIds,
            ref int nextRank)
        {
            var remaining = statsLookup.Values
                .Where(stat => !placedPlayerIds.Contains(stat.TournamentPlayerId))
                .OrderBy(stat => stat.IsEliminated ? 1 : 0)
                .ThenByDescending(stat => stat.LastStageNo ?? 0)
                .ThenByDescending(stat => stat.Wins)
                .ThenBy(stat => stat.Losses)
                .ThenByDescending(stat => stat.RacksWon - stat.RacksLost)
                .ThenBy(stat => stat.DisplayName)
                .ToList();

            foreach (var stat in remaining)
            {
                var label = BuildPlacementLabel(stat, nextRank);
                stat.PlacementRank = nextRank;
                stat.PlacementLabel = label;
                placedPlayerIds.Add(stat.TournamentPlayerId);
                nextRank++;
            }
        }

        private static string BuildPlacementLabel(TournamentPlayerStatsDto stat, int rank)
        {
            if (!stat.IsEliminated)
                return $"In Play (Rank {rank})";

            if (stat.LastStageNo.HasValue && stat.LastStageNo.Value > 0)
                return $"Eliminated in Stage {stat.LastStageNo.Value} (Rank {rank})";

            return $"Eliminated (Rank {rank})";
        }

        private async Task<Match> LoadMatchForScoringAsync(int matchId, CancellationToken ct)
        {
            var match = await _db.Matches
                .Include(m => m.Player1Tp)
                .Include(m => m.Player2Tp)
                .Include(m => m.WinnerTp)
                .Include(m => m.Table)
                .Include(m => m.Stage)
                .FirstOrDefaultAsync(m => m.Id == matchId, ct);

            return match ?? throw new KeyNotFoundException("Match not found.");
        }

        private static void ValidateScoringAccess(Match match, ScoringContext actor, bool ensureNotCompleted)
        {
            if (ensureNotCompleted && match.Status == MatchStatus.Completed)
                throw new InvalidOperationException("Match already completed.");

            if (match.Stage.Status == StageStatus.Completed)
                throw new InvalidOperationException("Stage already completed.");

            if (actor.IsTableClient)
            {
                if (actor.TableId is null || !match.TableId.HasValue || actor.TableId.Value != match.TableId.Value)
                    throw new InvalidOperationException("Table token does not match the assigned table.");

                if (actor.TournamentId is null || actor.TournamentId.Value != match.TournamentId)
                    throw new InvalidOperationException("Table token does not match the tournament.");
            }
        }

        private void ApplyRowVersion(Match match, string rowVersionBase64)
        {
            if (string.IsNullOrWhiteSpace(rowVersionBase64))
                throw new InvalidOperationException("Row version is required.");

            byte[] rowVersion;
            try
            {
                rowVersion = Convert.FromBase64String(rowVersionBase64);
            }
            catch (FormatException)
            {
                throw new InvalidOperationException("Row version format is invalid.");
            }

            _db.Entry(match).Property(m => m.RowVersion).OriginalValue = rowVersion;
        }

        private async Task SaveChangesWithConcurrencyAsync(Match match, CancellationToken ct)
        {
            try
            {
                await _db.SaveChangesAsync(ct);
            }
            catch (DbUpdateConcurrencyException)
            {
                var latest = await GetMatchSnapshotAsync(match.Id, ct);
                throw new ConcurrencyConflictException(latest);
            }
        }

        private async Task<MatchDto> GetMatchSnapshotAsync(int matchId, CancellationToken ct)
        {
            var match = await _db.Matches
                .Include(m => m.Player1Tp)
                .Include(m => m.Player2Tp)
                .Include(m => m.WinnerTp)
                .Include(m => m.Table)
                .AsNoTracking()
                .FirstOrDefaultAsync(m => m.Id == matchId, ct)
                ?? throw new KeyNotFoundException("Match not found.");

            var stageEval = await EvaluateStageAsync(match.StageId, ct);
            return MapToMatchDto(match, stageEval, stageEval.TournamentCompletionAvailable);
        }

        private async Task PublishRealtimeUpdates(Match match, MatchDto dto, bool bracketChanged, CancellationToken ct)
        {
            try
            {
                var groupName = TournamentHub.GetGroupName(match.TournamentId);
                await _hubContext.Clients.Group(groupName).SendAsync("matchUpdated", dto, ct);

                if (match.TableId.HasValue)
                {
                    var tableStatus = await TryBuildTableStatusAsync(match.TableId.Value, ct);
                    if (tableStatus is not null)
                    {
                        await _hubContext.Clients.Group(groupName).SendAsync("tableUpdated", tableStatus, ct);
                    }
                }

                if (bracketChanged)
                {
                    await _hubContext.Clients.Group(groupName).SendAsync("bracketUpdated", new { tournamentId = match.TournamentId }, ct);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to broadcast realtime updates for match {MatchId}", match.Id);
            }
        }

        private async Task<TableStatusDto?> TryBuildTableStatusAsync(int tableId, CancellationToken ct)
        {
            var table = await _db.TournamentTables
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.Id == tableId, ct);

            if (table is null)
                return null;

            var activeMatch = await _db.Matches
                .Where(m => m.TableId == tableId && m.Status != MatchStatus.Completed)
                .OrderBy(m => m.Id)
                .Select(m => new { m.Id, m.Status })
                .FirstOrDefaultAsync(ct);

            return new TableStatusDto
            {
                TableId = table.Id,
                TournamentId = table.TournamentId,
                Status = table.Status,
                CurrentMatchId = activeMatch?.Id,
                CurrentMatchStatus = activeMatch?.Status
            };
        }

        private static int DetermineWinnerFromScores(int scoreP1, int scoreP2, int player1Id, int player2Id, int raceTo)
        {
            var p1Wins = scoreP1 == raceTo;
            var p2Wins = scoreP2 == raceTo;

            if (p1Wins == p2Wins)
                throw new InvalidOperationException("Scores do not determine a unique winner.");

            return p1Wins ? player1Id : player2Id;
        }

        private sealed record PlacementMatch(
            int Id,
            int StageNo,
            BracketSide Bracket,
            int RoundNo,
            int? Player1TpId,
            int? Player2TpId,
            int? WinnerTpId,
            MatchStatus Status,
            int? NextWinnerMatchId,
            int? NextLoserMatchId);

        private static PlacementMatch ToPlacementMatch(Match match) => new(
            match.Id,
            match.Stage.StageNo,
            match.Bracket,
            match.RoundNo,
            match.Player1TpId,
            match.Player2TpId,
            match.WinnerTpId,
            match.Status,
            match.NextWinnerMatchId,
            match.NextLoserMatchId);

        private static int? DetermineLoser(PlacementMatch match)
        {
            if (!match.WinnerTpId.HasValue)
                return null;

            if (match.Player1TpId.HasValue && match.WinnerTpId == match.Player1TpId)
                return match.Player2TpId;

            if (match.Player2TpId.HasValue && match.WinnerTpId == match.Player2TpId)
                return match.Player1TpId;

            return null;
        }

        private static (TournamentPlacementDto? Champion, TournamentPlacementDto? RunnerUp, List<TournamentPlacementDto> AdditionalPlacements)
            ApplyPlacements(IReadOnlyList<PlacementMatch> matches, Dictionary<int, TournamentPlayerStatsDto> statsLookup)
        {
            foreach (var stat in statsLookup.Values)
            {
                stat.PlacementRank = null;
                stat.PlacementLabel = null;
            }

            TournamentPlacementDto? championDto = null;
            TournamentPlacementDto? runnerUpDto = null;
            var additionalPlacements = new List<TournamentPlacementDto>();

            var placedPlayerIds = new HashSet<int>();
            var nextRank = 1;

            var finalMatch = matches
                .Where(m => m.Status == MatchStatus.Completed && m.WinnerTpId.HasValue)
                .Where(m => m.NextWinnerMatchId == null && m.NextLoserMatchId == null)
                .OrderByDescending(m => m.StageNo)
                .ThenByDescending(m => m.RoundNo)
                .ThenByDescending(m => m.Id)
                .FirstOrDefault();

            if (finalMatch is not null)
            {
                var championId = finalMatch.WinnerTpId!.Value;
                championDto = CreatePlacementDto(championId, "Champion", nextRank, statsLookup);
                placedPlayerIds.Add(championId);
                nextRank++;

                var runnerUpId = DetermineLoser(finalMatch);
                if (runnerUpId.HasValue)
                {
                    runnerUpDto = CreatePlacementDto(runnerUpId.Value, "Runner-Up", nextRank, statsLookup);
                    placedPlayerIds.Add(runnerUpId.Value);
                    nextRank++;
                }

                var feederMatches = matches
                    .Where(m => m.Id != finalMatch.Id)
                    .Where(m => m.Status == MatchStatus.Completed)
                    .Where(m => m.NextWinnerMatchId == finalMatch.Id || m.NextLoserMatchId == finalMatch.Id)
                    .ToList();

                var semifinalLosers = new List<int>();
                foreach (var match in feederMatches)
                {
                    var loserId = DetermineLoser(match);
                    if (loserId.HasValue && placedPlayerIds.Add(loserId.Value))
                    {
                        semifinalLosers.Add(loserId.Value);
                    }
                }

                if (semifinalLosers.Count == 1)
                {
                    var thirdId = semifinalLosers[0];
                    additionalPlacements.Add(CreatePlacementDto(thirdId, "Third Place", nextRank, statsLookup));
                    placedPlayerIds.Add(thirdId);
                    nextRank++;
                }
                else if (semifinalLosers.Count > 1)
                {
                    foreach (var semifinalistId in semifinalLosers)
                    {
                        additionalPlacements.Add(CreatePlacementDto(semifinalistId, "Semi-Finalist", nextRank, statsLookup));
                        placedPlayerIds.Add(semifinalistId);
                        nextRank++;
                    }
                }
            }

            AssignRemainingPlacements(statsLookup, placedPlayerIds, ref nextRank);

            return (championDto, runnerUpDto, additionalPlacements);
        }

        private static void AssignPlayerToSlot(Match targetMatch, MatchSlotSourceType sourceType, int sourceMatchId, int playerTpId)
        {
            if (targetMatch.Player1SourceType == sourceType && targetMatch.Player1SourceMatchId == sourceMatchId)
            {
                targetMatch.Player1TpId = playerTpId;
            }
            else if (targetMatch.Player2SourceType == sourceType && targetMatch.Player2SourceMatchId == sourceMatchId)
            {
                targetMatch.Player2TpId = playerTpId;
            }
            else if (sourceType == MatchSlotSourceType.LoserOf)
            {
                SetLoserSlotSource(targetMatch, sourceMatchId);
                AssignPlayerToSlot(targetMatch, sourceType, sourceMatchId, playerTpId);
                return;
            }
            else
            {
                var slotIndex = TryAllocateSlot(targetMatch, sourceType, sourceMatchId);
                if (slotIndex == 1)
                {
                    targetMatch.Player1TpId = playerTpId;
                }
                else if (slotIndex == 2)
                {
                    targetMatch.Player2TpId = playerTpId;
                }
                else
                {
                    throw new InvalidOperationException($"Match {targetMatch.Id} does not have a slot configured for {sourceType}({sourceMatchId}).");
                }
            }

            if (targetMatch.Player1TpId.HasValue &&
                targetMatch.Player2TpId.HasValue &&
                targetMatch.Player1TpId == targetMatch.Player2TpId)
            {
                throw new InvalidOperationException($"Detected duplicated player {playerTpId} in match {targetMatch.Id}.");
            }
        }

        private static int TryAllocateSlot(Match match, MatchSlotSourceType sourceType, int sourceMatchId)
        {
            if (match.Player1SourceType is null && match.Player1TpId is null)
            {
                match.Player1SourceType = sourceType;
                match.Player1SourceMatchId = sourceMatchId;
                return 1;
            }

            if (match.Player2SourceType is null && match.Player2TpId is null)
            {
                match.Player2SourceType = sourceType;
                match.Player2SourceMatchId = sourceMatchId;
                return 2;
            }

            return 0;
        }

        private static Dictionary<int, List<MatchSlotReference>> BuildDependencyMap(Dictionary<int, Match> matches)
        {
            var map = new Dictionary<int, List<MatchSlotReference>>();

            foreach (var match in matches.Values)
            {
                if (match.Player1SourceMatchId.HasValue && match.Player1SourceType is MatchSlotSourceType.WinnerOf or MatchSlotSourceType.LoserOf)
                {
                    map.TryAdd(match.Player1SourceMatchId.Value, new List<MatchSlotReference>());
                    map[match.Player1SourceMatchId.Value].Add(new MatchSlotReference(match, 1, match.Player1SourceType));
                }

                if (match.Player2SourceMatchId.HasValue && match.Player2SourceType is MatchSlotSourceType.WinnerOf or MatchSlotSourceType.LoserOf)
                {
                    map.TryAdd(match.Player2SourceMatchId.Value, new List<MatchSlotReference>());
                    map[match.Player2SourceMatchId.Value].Add(new MatchSlotReference(match, 2, match.Player2SourceType));
                }
            }

            return map;
        }

        private void RewindCascade(int sourceMatchId,
            Dictionary<int, List<MatchSlotReference>> dependencyMap,
            HashSet<int> processedMatches,
            Dictionary<int, (int? TableId, MatchStatus PreviousStatus)> tableSnapshot)
        {
            if (!dependencyMap.TryGetValue(sourceMatchId, out var dependents))
                return;

            foreach (var slotRef in dependents)
            {
                ClearSlot(slotRef.Match, slotRef.SlotIndex);

                if (processedMatches.Add(slotRef.Match.Id))
                {
                    tableSnapshot[slotRef.Match.Id] = (slotRef.Match.TableId, slotRef.Match.Status);
                    ResetMatch(slotRef.Match);
                    RewindCascade(slotRef.Match.Id, dependencyMap, processedMatches, tableSnapshot);
                }
            }
        }

        private static void ClearSlot(Match match, int slotIndex)
        {
            if (slotIndex == 1)
            {
                match.Player1TpId = null;
            }
            else
            {
                match.Player2TpId = null;
            }
        }

        private static void ResetMatch(Match match)
        {
            match.Status = MatchStatus.NotStarted;
            match.ScoreP1 = null;
            match.ScoreP2 = null;
            match.WinnerTpId = null;
            match.WinnerTp = null;
            match.TableId = null;
            match.Table = null;
        }

        private async Task<TournamentTable> ValidateTableSelectionAsync(Match match, int tableId, CancellationToken ct)
        {
            var table = await _db.TournamentTables
                .FirstOrDefaultAsync(t => t.Id == tableId, ct)
                ?? throw new InvalidOperationException("Table not found.");

            if (table.TournamentId != match.TournamentId)
                throw new InvalidOperationException("Table belongs to another tournament.");

            if (table.Status == TableStatus.Closed)
                throw new InvalidOperationException("Table is closed.");

            if (table.Status == TableStatus.InUse && match.TableId != table.Id)
            {
                var busy = await _db.Matches
                    .AnyAsync(m => m.TournamentId == match.TournamentId && m.TableId == table.Id && m.Id != match.Id && m.Status != MatchStatus.Completed, ct);
                if (busy)
                    throw new InvalidOperationException("Table is currently assigned to another match.");
            }

            return table;
        }

        private async Task UpdateTableUsageAsync(Match match, int? previousTableId, MatchStatus previousStatus, CancellationToken ct)
        {
            if (previousTableId.HasValue && (previousTableId != match.TableId || previousStatus != match.Status))
            {
                await MarkTableOpenIfIdleAsync(previousTableId.Value, match.Id, ct);
            }

            if (!match.TableId.HasValue)
                return;

            if (match.Status == MatchStatus.InProgress)
            {
                await MarkTableInUseAsync(match.TableId.Value, ct);
            }
            else if (match.Status == MatchStatus.Completed || match.Status == MatchStatus.NotStarted)
            {
                await MarkTableOpenIfIdleAsync(match.TableId.Value, match.Id, ct);
            }
        }

        private async Task MarkTableInUseAsync(int tableId, CancellationToken ct)
        {
            var table = await _db.TournamentTables.FirstOrDefaultAsync(t => t.Id == tableId, ct)
                ?? throw new InvalidOperationException("Table not found.");

            if (table.Status != TableStatus.InUse)
                table.Status = TableStatus.InUse;
        }

        private async Task MarkTableOpenIfIdleAsync(int tableId, int currentMatchId, CancellationToken ct)
        {
            var table = await _db.TournamentTables.FirstOrDefaultAsync(t => t.Id == tableId, ct);
            if (table is null)
                return;

            if (table.Status == TableStatus.Closed)
                return;

            var busy = await _db.Matches
                .AnyAsync(m => m.TableId == tableId && m.Id != currentMatchId && m.Status != MatchStatus.Completed, ct);

            if (!busy)
                table.Status = TableStatus.Open;
        }

        private sealed record MatchSlotReference(Match Match, int SlotIndex, MatchSlotSourceType? SourceType);

        private StagePreviewDto BuildStagePreview(
            int stageNo, BracketType type, BracketOrdering ordering, int size, List<PlayerSeed>? players)
        {
            var slots = MakeSlots(players, size, ordering);
            return type == BracketType.SingleElimination
                ? PreviewSingle(stageNo, ordering, slots)
                : PreviewDouble(stageNo, ordering, slots);
        }

        private List<PlayerSeed?> MakeSlots(List<PlayerSeed>? players, int size, BracketOrdering ordering)
        {
            var slots = Enumerable.Repeat<PlayerSeed?>(null, size).ToList();
            if (players is null || players.Count == 0) return slots;

            if (ordering == BracketOrdering.Random)
            {
                // Random
                var shuffled = players.ToList();
                FisherYates(shuffled);

                for (int i = 0; i < Math.Min(size, shuffled.Count); i++)
                    slots[i] = shuffled[i];
                return slots;
            }

            var seeded = players.Where(p => p.Seed.HasValue).OrderBy(p => p.Seed!.Value).ToList();
            var unseeded = players.Where(p => !p.Seed.HasValue).ToList();

            FisherYates(unseeded);

            if (seeded.Count >= 2)
            {
                //Set highest and lowest seeds in first two positions
                slots[0] = seeded[0];
                slots[1] = seeded[^1];

                int nextPosition = 2;
                for (int i = 1; i < seeded.Count - 1 && nextPosition < size; i++)
                {
                    while (nextPosition < size && slots[nextPosition] != null)
                        nextPosition++;

                    if (nextPosition < size)
                    {
                        slots[nextPosition] = seeded[i];
                        nextPosition += 2;

                        if (nextPosition >= size)
                        {
                            nextPosition = 3;
                            while (nextPosition < size && slots[nextPosition] != null)
                                nextPosition += 2;
                        }
                    }
                }
            }
            else if (seeded.Count == 1)
            {
                slots[0] = seeded[0];
            }

            int u = 0;
            for (int i = 0; i < size; i++)
                if (slots[i] is null)
                    slots[i] = (u < unseeded.Count) ? unseeded[u++] : null;

            return slots;
        }


        private StagePreviewDto PreviewSingle(int stageNo, BracketOrdering ordering, List<PlayerSeed?> slots)
        {
            var dto = new StagePreviewDto
            {
                StageNo = stageNo,
                Type = BracketType.SingleElimination,
                Ordering = ordering,
                BracketSize = slots.Count
            };

            int n = slots.Count;
            int matchesRound1 = n / 2;

            var r1 = new RoundPreviewDto { RoundNo = 1 };
            for (int i = 0; i < matchesRound1; i++)
            {
                var p1 = slots[2 * i];
                var p2 = slots[2 * i + 1];
                r1.Matches.Add(new MatchPreviewDto
                {
                    PositionInRound = i + 1,
                    P1Name = p1?.Name,
                    P1Seed = p1?.Seed,
                    P1Country = p1?.Country,
                    P2Name = p2?.Name,
                    P2Seed = p2?.Seed,
                    P2Country = p2?.Country,
                    P1FargoRating = p1?.FargoRating,
                    P2FargoRating = p2?.FargoRating
                });
            }
            dto.Rounds.Add(r1);

            int roundNo = 2;
            int m = matchesRound1 / 2;
            while (m > 0)
            {
                var r = new RoundPreviewDto { RoundNo = roundNo++ };
                for (int i = 0; i < m; i++)
                {
                    r.Matches.Add(new MatchPreviewDto
                    {
                        PositionInRound = i + 1,
                        P1Name = null,
                        P2Name = null,
                        P1Country = null,
                        P2Country = null
                    });
                }
                dto.Rounds.Add(r);
                m /= 2;
            }

            return dto;
        }


        private StagePreviewDto PreviewDouble(int stageNo, BracketOrdering ordering, List<PlayerSeed?> slots)
        {
            var dto = PreviewSingle(stageNo, ordering, slots);
            dto.Type = BracketType.DoubleElimination;
            return dto;
        }

        private void BuildAndPersistSingleOrDouble(int tournamentId, TournamentStage stage, BracketType type,
        List<PlayerSeed?> slots, CancellationToken ct)
        {
            if (type == BracketType.SingleElimination)
            {
                BuildAndPersistSingle(tournamentId, stage, slots, ct);
            }
            else
            {
                if ((slots.Count & (slots.Count - 1)) != 0)
                    throw new InvalidOperationException("Double elimination bracket requires the bracket size to be a power of two.");

                // 1. Tạo Winners bracket
                var winnersMatches = BuildWinnersBracket(tournamentId, stage, slots, ct);

                // 2. Tạo Losers bracket  
                var losersMatches = BuildLosersBracket(tournamentId, stage, slots.Count, ct);

                // 3. Tạo Finals bracket
                var finalsMatches = BuildFinalsBracket(tournamentId, stage, ct);

                // 4. Map NextWinner/NextLoser relationships
                MapDoubleEliminationFlow(winnersMatches, losersMatches, finalsMatches);

                _db.SaveChanges();
            }
        }


        private void BuildAndPersistSingle(int tournamentId, TournamentStage stage,
            List<PlayerSeed?> slots, CancellationToken ct, BracketSide bracket = BracketSide.Knockout)
        {
            var n = slots.Count;
            if (n == 0)
                throw new InvalidOperationException("Single elimination bracket requires at least two slots.");

            if ((n & 1) == 1)
                throw new InvalidOperationException("Single elimination bracket requires an even number of slots.");

            var roundNo = 1;
            var matches = n / 2;
            var cursor = 0;

            var created = new List<Match>();

            while (matches > 0)
            {
                for (int pos = 1; pos <= matches; pos++)
                {
                    PlayerSeed? p1 = null;
                    PlayerSeed? p2 = null;
                    MatchSlotSourceType? p1Source = null;
                    MatchSlotSourceType? p2Source = null;

                    if (roundNo == 1)
                    {
                        if (cursor >= slots.Count)
                            throw new InvalidOperationException("Not enough seeded slots to populate round 1.");
                        p1 = slots[cursor++];

                        if (cursor >= slots.Count)
                            throw new InvalidOperationException("Not enough seeded slots to populate round 1.");
                        p2 = slots[cursor++];

                        p1Source = MatchSlotSourceType.Seed;
                        p2Source = MatchSlotSourceType.Seed;
                    }

                    created.Add(new Match
                    {
                        TournamentId = tournamentId,
                        StageId = stage.Id,
                        Bracket = bracket,
                        RoundNo = roundNo,
                        PositionInRound = pos,
                        Player1TpId = p1?.TpId,
                        Player2TpId = p2?.TpId,
                        Player1SourceType = p1Source,
                        Player2SourceType = p2Source,
                        Status = MatchStatus.NotStarted
                    });
                }

                roundNo++;
                matches /= 2;
            }
            _db.Matches.AddRange(created);
            _db.SaveChanges(); // lấy Id để set NextWinnerMatchId

            // set next pointers (W → W) cho SE/KO (hoặc Winners của DE)
            var grouped = created.GroupBy(m => m.RoundNo).OrderBy(g => g.Key).ToList();
            for (int r = 0; r < grouped.Count - 1; r++)
            {
                var cur = grouped[r].OrderBy(x => x.PositionInRound).ToList();
                var nxt = grouped[r + 1].OrderBy(x => x.PositionInRound).ToList();
                for (int i = 0; i < cur.Count; i++)
                {
                    var nextPos = (i / 2); // 0-based
                    cur[i].NextWinnerMatchId = nxt[nextPos].Id;
                    SetSlotSource(nxt[nextPos], (i % 2 == 0) ? 1 : 2, MatchSlotSourceType.WinnerOf, cur[i].Id);
                }
            }
            _db.SaveChanges();
        }

        private static int[] GetLosersPattern(int bracketSize)
        {
            if (bracketSize < 2)
                return Array.Empty<int>();

            if ((bracketSize & (bracketSize - 1)) != 0)
                throw new InvalidOperationException("Double elimination bracket requires a power-of-two bracket size.");

            var exponent = (int)Math.Log2(bracketSize);

            if (exponent == 1)
                return new[] { 1 };

            var pattern = new List<int>();
            for (int offset = exponent - 2; offset >= 0; offset--)
            {
                var matches = 1 << offset;
                pattern.Add(matches);
                pattern.Add(matches);
            }

            return pattern.ToArray();
        }

        private static int DetermineTargetLosersRound(int winnersRoundNumber, int totalLosersRounds)
        {
            if (totalLosersRounds == 0)
                return 0;

            if (winnersRoundNumber == 1)
                return totalLosersRounds >= 1 ? 1 : 0;

            var round = 2 * (winnersRoundNumber - 1);
            return round <= totalLosersRounds ? round : 0;
        }

        private static int CalculateLosersIndex(int winnerIndex, int winnersCount, int losersCount)
        {
            if (losersCount <= 0)
                return 0;

            if (winnersCount == losersCount)
                return Math.Min(winnerIndex, losersCount - 1);

            if (winnersCount == losersCount * 2)
                return Math.Min(winnerIndex / 2, losersCount - 1);

            var proportional = (int)Math.Floor(winnerIndex * (double)losersCount / winnersCount);
            if (proportional < 0) proportional = 0;
            if (proportional >= losersCount) proportional = losersCount - 1;
            return proportional;
        }


        private List<Match> BuildWinnersBracket(int tournamentId, TournamentStage stage, List<PlayerSeed?> slots, CancellationToken ct)
        {
            int n = slots.Count;
            int roundNo = 1;
            int matches = n / 2;
            int cursor = 0;

            var created = new List<Match>();

            while (matches > 0)
            {
                for (int pos = 1; pos <= matches; pos++)
                {
                    PlayerSeed? p1 = null;
                    PlayerSeed? p2 = null;

                    if (roundNo == 1)
                    {
                        p1 = slots[cursor++];
                        p2 = slots[cursor++];
                    }

                    created.Add(new Match
                    {
                        TournamentId = tournamentId,
                        StageId = stage.Id,
                        Bracket = BracketSide.Winners,
                        RoundNo = roundNo,
                        PositionInRound = pos,
                        Player1TpId = p1?.TpId,
                        Player2TpId = p2?.TpId,
                        Player1SourceType = roundNo == 1 ? MatchSlotSourceType.Seed : null,
                        Player2SourceType = roundNo == 1 ? MatchSlotSourceType.Seed : null,
                        Status = MatchStatus.NotStarted
                    });
                }

                roundNo++;
                matches /= 2;
            }

            _db.Matches.AddRange(created);
            _db.SaveChanges();
            return created;
        }

        private List<Match> BuildLosersBracket(int tournamentId, TournamentStage stage, int bracketSize, CancellationToken ct)
        {
            var losersMatches = new List<Match>();
            var pattern = GetLosersPattern(bracketSize);
            var losersRounds = pattern.Length;

            if (losersRounds == 0)
                return losersMatches;

            for (int round = 1; round <= losersRounds; round++)
            {
                int matchesInRound = pattern[round - 1];

                for (int pos = 1; pos <= matchesInRound; pos++)
                {
                    losersMatches.Add(new Match
                    {
                        TournamentId = tournamentId,
                        StageId = stage.Id,
                        Bracket = BracketSide.Losers,
                        RoundNo = round,
                        PositionInRound = pos,
                        Status = MatchStatus.NotStarted
                    });
                }
            }

            _db.Matches.AddRange(losersMatches);
            _db.SaveChanges();
            return losersMatches;
        }

        private List<Match> BuildFinalsBracket(int tournamentId, TournamentStage stage, CancellationToken ct)
        {
            var finalsMatches = new List<Match>
            {
                new Match
                {
                    TournamentId = tournamentId,
                    StageId = stage.Id,
                    Bracket = BracketSide.Finals,
                    RoundNo = 1,
                    PositionInRound = 1,
                    Status = MatchStatus.NotStarted
                }
            };

            _db.Matches.AddRange(finalsMatches);
            _db.SaveChanges();
            return finalsMatches;
        }

        private void MapDoubleEliminationFlow(List<Match> winnersMatches, List<Match> losersMatches, List<Match> finalsMatches)
        {
            // 1. Map Winners internal flow
            MapWinnersBracketFlow(winnersMatches);

            // 2. Map Losers internal flow  
            MapLosersBracketFlow(losersMatches);

            // 3. Map Winners to Losers flow
            MapWinnersToLosersFlow(winnersMatches, losersMatches);

            // 4. Map to Finals
            MapFinalsConnections(winnersMatches, losersMatches, finalsMatches);
        }

        private void MapWinnersBracketFlow(List<Match> winnersMatches)
        {
            var grouped = winnersMatches.GroupBy(m => m.RoundNo).OrderBy(g => g.Key).ToList();

            for (int r = 0; r < grouped.Count - 1; r++)
            {
                var currentRound = grouped[r].OrderBy(x => x.PositionInRound).ToList();
                var nextRound = grouped[r + 1].OrderBy(x => x.PositionInRound).ToList();

                for (int i = 0; i < currentRound.Count; i++)
                {
                    var nextPos = i / 2;
                    currentRound[i].NextWinnerMatchId = nextRound[nextPos].Id;
                    SetSlotSource(nextRound[nextPos], (i % 2 == 0) ? 1 : 2, MatchSlotSourceType.WinnerOf, currentRound[i].Id);
                }
            }
        }

        private void MapLosersBracketFlow(List<Match> losersMatches)
        {
            var grouped = losersMatches.GroupBy(m => m.RoundNo).OrderBy(g => g.Key).ToList();

            for (int r = 0; r < grouped.Count - 1; r++)
            {
                var currentRound = grouped[r].OrderBy(x => x.PositionInRound).ToList();
                var nextRound = grouped[r + 1].OrderBy(x => x.PositionInRound).ToList();

                for (int i = 0; i < currentRound.Count; i++)
                {
                    int nextPos;
                    if (currentRound.Count == nextRound.Count)
                    {
                        nextPos = i;
                    }
                    else if (currentRound.Count == nextRound.Count * 2)
                    {
                        nextPos = i / 2;
                    }
                    else
                    {
                        nextPos = Math.Min(i / Math.Max(1, currentRound.Count / Math.Max(1, nextRound.Count)), nextRound.Count - 1);
                    }
                    if (nextPos < nextRound.Count)
                    {
                        currentRound[i].NextWinnerMatchId = nextRound[nextPos].Id;

                        int slotIndex;
                        if (currentRound.Count == nextRound.Count)
                            slotIndex = 1;
                        else
                            slotIndex = (i % 2 == 0) ? 1 : 2;

                        if (slotIndex == 1 && nextRound[nextPos].Player1SourceType is not null)
                            slotIndex = 2;

                        SetSlotSource(nextRound[nextPos], slotIndex, MatchSlotSourceType.WinnerOf, currentRound[i].Id);
                    }
                }
            }
        }

        private void MapWinnersToLosersFlow(List<Match> winnersMatches, List<Match> losersMatches)
        {
            if (winnersMatches.Count == 0 || losersMatches.Count == 0)
                return;

            var winnersGrouped = winnersMatches
                .GroupBy(m => m.RoundNo)
                .OrderBy(g => g.Key)
                .Select(g => g.OrderBy(m => m.PositionInRound).ToList())
                .ToList();

            var losersGrouped = losersMatches
                .GroupBy(m => m.RoundNo)
                .ToDictionary(
                    g => g.Key,
                    g => g.OrderBy(m => m.PositionInRound).ToList());

            var firstRoundMatches = winnersMatches.Where(m => m.RoundNo == 1).ToList();
            if (firstRoundMatches.Count == 0)
                return;

            var bracketSize = firstRoundMatches.Count * 2;
            var losersPattern = GetLosersPattern(bracketSize);
            var totalLosersRounds = losersPattern.Length;

            for (int wrIndex = 0; wrIndex < winnersGrouped.Count; wrIndex++)
            {
                var winnersRound = winnersGrouped[wrIndex];
                var winnersRoundNumber = wrIndex + 1;
                var targetLosersRound = DetermineTargetLosersRound(winnersRoundNumber, totalLosersRounds);
                if (targetLosersRound == 0)
                    continue;

                if (!losersGrouped.TryGetValue(targetLosersRound, out var losersRoundMatches) || losersRoundMatches.Count == 0)
                    continue;

                for (int i = 0; i < winnersRound.Count; i++)
                {
                    var losersIndex = CalculateLosersIndex(i, winnersRound.Count, losersRoundMatches.Count);
                    var losersMatch = losersRoundMatches[losersIndex];
                    winnersRound[i].NextLoserMatchId = losersMatch.Id;
                    SetLoserSlotSource(losersMatch, winnersRound[i].Id);
                }
            }
        }

        private void MapFinalsConnections(List<Match> winnersMatches, List<Match> losersMatches, List<Match> finalsMatches)
        {
            if (finalsMatches.Count == 0) return;

            var finalsMatch = finalsMatches[0];

            // Winners champion → Finals
            var winnersChampion = winnersMatches
                .Where(m => m.RoundNo == winnersMatches.Max(x => x.RoundNo))
                .First();
            winnersChampion.NextWinnerMatchId = finalsMatch.Id;
            SetSlotSource(finalsMatch, 1, MatchSlotSourceType.WinnerOf, winnersChampion.Id);

            // Losers champion → Finals
            var losersChampion = losersMatches
                .Where(m => m.RoundNo == losersMatches.Max(x => x.RoundNo))
                .First();
            losersChampion.NextWinnerMatchId = finalsMatch.Id;
            SetSlotSource(finalsMatch, 2, MatchSlotSourceType.WinnerOf, losersChampion.Id);
        }


        //helpers

        private static int NextPowerOfTwo(int x)
        {
            if (x < 1) return 1;
            int p = 1;
            while (p < x) p <<= 1;
            return p;
        }

        private static void FisherYates<T>(IList<T> a)
        {
            var rng = Random.Shared;
            for (int i = a.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (a[i], a[j]) = (a[j], a[i]);
            }
        }

        private static int GetOptimalBracketSize(int playerCount)
        {

            if (playerCount <= 0) return 2;
            if (playerCount <= 2) return 2;
            if (playerCount <= 4) return 4;
            if (playerCount <= 8) return 8;
            if (playerCount <= 16) return 16;
            if (playerCount <= 32) return 32;
            if (playerCount <= 64) return 64;
            if (playerCount <= 128) return 128;

            return NextPowerOfTwo(playerCount);
        }

        private async Task ValidateManualAssignments(int tournamentId, List<ManualSlotAssignment> assignments, CancellationToken ct)
        {
            var assignedPlayers = assignments
                .Where(a => a.TpId.HasValue)
                .Select(a => a.TpId!.Value)
                .ToList();

            if (assignedPlayers.Count < 2)
                throw new InvalidOperationException("Manual bracket creation requires at least two player assignments.");

            // Check for duplicate players
            if (assignedPlayers.Count != assignedPlayers.Distinct().Count())
                throw new InvalidOperationException("Cannot assign same player to multiple slots.");

            // Verify all TpIds exist and belong to tournament
            if (assignedPlayers.Any())
            {
                var validPlayers = await _db.TournamentPlayers
                    .Where(tp => tp.TournamentId == tournamentId && assignedPlayers.Contains(tp.Id))
                    .Select(tp => tp.Id)
                    .ToListAsync(ct);

                var invalidPlayers = assignedPlayers.Except(validPlayers).ToList();
                if (invalidPlayers.Any())
                    throw new InvalidOperationException($"Invalid player IDs: {string.Join(", ", invalidPlayers)}");
            }
        }

        private async Task<List<PlayerSeed?>> ConvertAssignmentsToSlots(int tournamentId, List<ManualSlotAssignment> assignments, CancellationToken ct)
        {
            // Get optimal bracket size
            var playerCount = assignments.Count(a => a.TpId.HasValue);
            var bracketSize = GetOptimalBracketSize(Math.Max(playerCount, 2));

            // Initialize slots
            var slots = Enumerable.Repeat<PlayerSeed?>(null, bracketSize).ToList();

            // Get player data
            var playerIds = assignments.Where(a => a.TpId.HasValue).Select(a => a.TpId!.Value).ToList();
            var playersDict = await _db.TournamentPlayers
                .Where(tp => tp.TournamentId == tournamentId && playerIds.Contains(tp.Id))
                .ToDictionaryAsync(tp => tp.Id, tp => new PlayerSeed
                {
                    TpId = tp.Id,
                    Name = tp.DisplayName,
                    Seed = tp.Seed,
                    Country = tp.Country,
                    FargoRating = tp.SkillLevel
                }, ct);

            // Assign players to slots
            foreach (var assignment in assignments)
            {
                if (assignment.SlotPosition >= 0 && assignment.SlotPosition < bracketSize)
                {
                    if (assignment.TpId.HasValue && playersDict.TryGetValue(assignment.TpId.Value, out var player))
                    {
                        slots[assignment.SlotPosition] = player;
                    }
                }
            }

            return slots;
        }


        private static void SetSlotSource(Match match, int slotIndex, MatchSlotSourceType sourceType, int sourceMatchId)
        {
            if (slotIndex == 1)
            {
                match.Player1SourceType = sourceType;
                match.Player1SourceMatchId = sourceMatchId;
            }
            else
            {
                match.Player2SourceType = sourceType;
                match.Player2SourceMatchId = sourceMatchId;
            }
        }

        private static void SetLoserSlotSource(Match match, int sourceMatchId)
        {
            if (match.Player1SourceType == MatchSlotSourceType.LoserOf && match.Player1SourceMatchId == sourceMatchId)
                return;
            if (match.Player2SourceType == MatchSlotSourceType.LoserOf && match.Player2SourceMatchId == sourceMatchId)
                return;

            if (match.Player1SourceType is null)
            {
                SetSlotSource(match, 1, MatchSlotSourceType.LoserOf, sourceMatchId);
                return;
            }

            if (match.Player2SourceType is null)
            {
                SetSlotSource(match, 2, MatchSlotSourceType.LoserOf, sourceMatchId);
                return;
            }

            throw new InvalidOperationException($"No available slot for loser of match {sourceMatchId} in match {match.Id}.");
        }

        private bool TryCompleteMatchByBye(Match match, Dictionary<int, Match> matchDict)
        {
            if (match.Status != MatchStatus.NotStarted)
                return false;

            var hasPlayer1 = match.Player1TpId.HasValue;
            var hasPlayer2 = match.Player2TpId.HasValue;

            if ((hasPlayer1 && hasPlayer2) || (!hasPlayer1 && !hasPlayer2))
                return false;

            var emptySlot = hasPlayer1 ? 2 : 1;
            if (ShouldAwaitOpponent(match, emptySlot, matchDict))
                return false;

            var winnerId = hasPlayer1 ? match.Player1TpId!.Value : match.Player2TpId!.Value;
            match.WinnerTpId = winnerId;
            match.WinnerTp = hasPlayer1 ? match.Player1Tp : match.Player2Tp;
            match.Status = MatchStatus.Completed;
            match.ScoreP1 = hasPlayer1 ? 0 : 0;
            match.ScoreP2 = hasPlayer2 ? 0 : 0;

            PropagateResult(match, matchDict);
            return true;
        }

        private bool ShouldAwaitOpponent(Match match, int slotIndex, Dictionary<int, Match> matchDict)
        {
            var sourceType = slotIndex == 1 ? match.Player1SourceType : match.Player2SourceType;
            var sourceMatchId = slotIndex == 1 ? match.Player1SourceMatchId : match.Player2SourceMatchId;

            if (sourceType is null or MatchSlotSourceType.Seed)
                return false;

            if (!sourceMatchId.HasValue)
                return false;

            if (!matchDict.TryGetValue(sourceMatchId.Value, out var sourceMatch))
                return true;

            if (sourceType == MatchSlotSourceType.WinnerOf)
                return true;

            if (sourceType == MatchSlotSourceType.LoserOf)
            {
                if (sourceMatch.Status != MatchStatus.Completed)
                    return true;

                var loser = DetermineLoser(sourceMatch);
                return loser.HasValue;
            }

            return false;
        }

        private StageEvaluation EvaluateStageState(Tournament tournament, TournamentStage stage, List<Match> matches)
        {
            var survivors = matches
                .Where(m => m.Status != MatchStatus.Completed)
                .SelectMany(m => new[] { m.Player1TpId, m.Player2TpId })
                .Where(id => id.HasValue)
                .Select(id => id!.Value)
                .Distinct()
                .ToList();

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

        private async Task<StageEvaluation> EvaluateStageAsync(int stageId, CancellationToken ct)
        {
            var stage = await _db.TournamentStages
                .Include(s => s.Tournament)
                    .ThenInclude(t => t.Stages)
                .FirstOrDefaultAsync(s => s.Id == stageId, ct)
                ?? throw new KeyNotFoundException("Stage not found.");

            var matches = await _db.Matches
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

        private async Task<List<PlayerSeed?>> ConvertAssignmentsToSlotsForStage2(int tournamentId, List<ManualSlotAssignment> assignments, IReadOnlyCollection<int> allowedTpIds, CancellationToken ct)
        {
            var bracketSize = allowedTpIds.Count;
            var slots = Enumerable.Repeat<PlayerSeed?>(null, bracketSize).ToList();

            if (assignments.Count > bracketSize)
                throw new InvalidOperationException("Manual assignments exceed bracket size.");

            var allowedSet = allowedTpIds.ToHashSet();
            var requestedIds = assignments
                .Where(a => a.TpId.HasValue)
                .Select(a => a.TpId!.Value)
                .ToList();

            if (requestedIds.Count != requestedIds.Distinct().Count())
                throw new InvalidOperationException("Manual assignments contain duplicate players.");

            if (requestedIds.Except(allowedSet).Any())
                throw new InvalidOperationException("Manual assignments contain invalid players.");

            var playersDict = await _db.TournamentPlayers
                .Where(tp => tp.TournamentId == tournamentId && allowedSet.Contains(tp.Id))
                .ToDictionaryAsync(tp => tp.Id, tp => new PlayerSeed
                {
                    TpId = tp.Id,
                    Name = tp.DisplayName,
                    Seed = tp.Seed,
                    Country = tp.Country,
                    FargoRating = tp.SkillLevel
                }, ct);

            foreach (var assignment in assignments)
            {
                if (assignment.SlotPosition < 0 || assignment.SlotPosition >= bracketSize)
                    throw new InvalidOperationException("Invalid slot position.");

                if (assignment.TpId.HasValue && playersDict.TryGetValue(assignment.TpId.Value, out var player))
                {
                    if (slots[assignment.SlotPosition] is not null)
                        throw new InvalidOperationException("Slot already assigned.");

                    slots[assignment.SlotPosition] = player;
                }
            }

            var remaining = allowedSet.Except(requestedIds).ToList();
            if (remaining.Any())
            {
                var orderedRemaining = remaining
                    .OrderBy(id => playersDict[id].Seed ?? int.MaxValue)
                    .ThenBy(id => playersDict[id].Name)
                    .ToList();

                var cursor = 0;
                for (int i = 0; i < slots.Count && cursor < orderedRemaining.Count; i++)
                {
                    if (slots[i] is null)
                    {
                        slots[i] = playersDict[orderedRemaining[cursor++]];
                    }
                }
            }

            if (slots.Any(s => s is null))
                throw new InvalidOperationException("Not enough players assigned to stage 2 slots.");

            return slots;
        }

        private async Task<List<PlayerSeed>> GetPlayerSeedsAsync(IEnumerable<int> tpIds, CancellationToken ct)
        {
            var idList = tpIds.ToList();
            var players = await _db.TournamentPlayers
                .Where(tp => idList.Contains(tp.Id))
                .Select(tp => new PlayerSeed
                {
                    TpId = tp.Id,
                    Name = tp.DisplayName,
                    Seed = tp.Seed,
                    Country = tp.Country,
                    FargoRating = tp.SkillLevel
                })
                .ToListAsync(ct);

            var dict = players.ToDictionary(p => p.TpId);
            return idList
                .Where(dict.ContainsKey)
                .Select(id => dict[id])
                .ToList();
        }

        private sealed record StageEvaluation(
            TournamentStage Stage,
            List<int> SurvivorTpIds,
            int ActualBracketSize,
            int? TargetAdvance,
            bool CanComplete,
            bool TournamentCompletionAvailable);

        private static string FormatDebugLine(MatchDto match)
        {
            var p1 = FormatSlotLabel(match.Player1, match.Player1SourceType, match.Player1SourceMatchId);
            var p2 = FormatSlotLabel(match.Player2, match.Player2SourceType, match.Player2SourceMatchId);
            var status = match.Status.ToString();
            var score = match.ScoreP1.HasValue && match.ScoreP2.HasValue ? $"{match.ScoreP1}-{match.ScoreP2}" : "-";
            var nextW = match.NextWinnerMatchId.HasValue ? match.NextWinnerMatchId.Value.ToString() : "-";
            var nextL = match.NextLoserMatchId.HasValue ? match.NextLoserMatchId.Value.ToString() : "-";
            var stageFlag = match.StageCompletionAvailable ? "*" : string.Empty;
            var tournamentFlag = match.TournamentCompletionAvailable ? "!" : string.Empty;

            return $"      M{match.Id:D4} Pos:{match.PositionInRound} {p1} vs {p2} | {status} | Score {score} | NextW {nextW} NextL {nextL}{stageFlag}{tournamentFlag}";
        }

        private static string FormatSlotLabel(PlayerDto? player, MatchSlotSourceType? sourceType, int? sourceMatchId)
        {
            if (player is not null)
            {
                var seedInfo = player.Seed.HasValue ? $"#{player.Seed.Value}" : string.Empty;
                return $"{player.Name}{seedInfo}";
            }

            return sourceType switch
            {
                MatchSlotSourceType.Seed => "(Seed TBD)",
                MatchSlotSourceType.WinnerOf => sourceMatchId.HasValue ? $"Winner[{sourceMatchId.Value}]" : "Winner[TBD]",
                MatchSlotSourceType.LoserOf => sourceMatchId.HasValue ? $"Loser[{sourceMatchId.Value}]" : "Loser[TBD]",
                _ => "(Empty)"
            };
        }


        private sealed record PlayerSeed
        {
            public int TpId { get; init; }
            public string Name { get; init; } = default!;
            public int? Seed { get; init; }
            public string? Country { get; init; }
            public int? FargoRating { get; init; }
        }
    }
}
