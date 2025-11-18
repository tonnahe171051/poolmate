using Microsoft.EntityFrameworkCore;
using PoolMate.Api.Data;
using PoolMate.Api.Dtos.Tournament;
using PoolMate.Api.Models;

namespace PoolMate.Api.Services
{
    public class BracketService : IBracketService
    {
        private readonly ApplicationDbContext _db;
        private readonly Random _rng = new();

        public BracketService(ApplicationDbContext db) => _db = db;

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

            if (players.Count == 0)
                throw new InvalidOperationException("No players to draw.");

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

                    if (players.Count == 0)
                        throw new InvalidOperationException("No players to draw.");

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

                var stageDto = new StageDto
                {
                    StageNo = stage.StageNo,
                    Type = stage.Type,
                    Ordering = stage.Ordering,
                    BracketSize = CalculateBracketSize(stageMatches)
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
                            roundDto.Matches.Add(MapToMatchDto(match));
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

        private MatchDto MapToMatchDto(Match match)
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
                IsBye = isBye
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

            return MapToMatchDto(match);
        }

        public async Task<MatchDto> CorrectMatchResultAsync(int matchId, CorrectMatchResultRequest request, CancellationToken ct)
        {
            if (request is null)
                throw new ArgumentNullException(nameof(request));

            var match = await LoadMatchAggregateAsync(matchId, ct);

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

            return MapToMatchDto(match);
        }

        private async Task<Match> LoadMatchAggregateAsync(int matchId, CancellationToken ct)
        {
            var match = await _db.Matches
                .Include(m => m.Player1Tp)
                .Include(m => m.Player2Tp)
                .Include(m => m.WinnerTp)
                .Include(m => m.Table)
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
            int n = slots.Count;
            int roundNo = 1;
            int matches = n / 2;
            int cursor = 0;

            var created = new List<Match>();

            while (matches > 0)
            {
                for (int pos = 1; pos <= matches; pos++)
                {
                    var p1 = slots[cursor++];
                    var p2 = slots[cursor++];

                    created.Add(new Match
                    {
                        TournamentId = tournamentId,
                        StageId = stage.Id,
                        Bracket = bracket,
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

        private static int CalculateLosersMatchesInRound(int round, int bracketSize)
        {
            // Lookup table cho các bracket sizes phổ biến
            var losersPattern = bracketSize switch
            {
                2 => new int[] { },
                4 => new int[] { 1, 1 },
                8 => new int[] { 2, 2, 1, 1 },
                16 => new int[] { 4, 4, 2, 2, 1, 1 },
                32 => new int[] { 8, 8, 4, 4, 2, 2, 1, 1 },
                64 => new int[] { 16, 16, 8, 8, 4, 4, 2, 2, 1, 1 },
                _ => CalculateLosersPatternGeneric(bracketSize)
            };

            return (round <= losersPattern.Length) ? losersPattern[round - 1] : 0;
        }

        private static int[] CalculateLosersPatternGeneric(int bracketSize)
        {
            // Fallback for another bracket sizes
            int winnersRounds = (int)Math.Log2(bracketSize);
            var pattern = new List<int>();

            for (int r = 1; r <= (winnersRounds - 1) * 2; r++)
            {
                if (r % 2 == 1) // Round odd
                {
                    int winnersRoundFeeding = (r + 1) / 2;
                    pattern.Add(bracketSize / (1 << (winnersRoundFeeding + 1)));
                }
                else // Round even
                {
                    pattern.Add(bracketSize / (1 << (r / 2 + 2)));
                }
            }

            return pattern.ToArray();
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
            int winnersRounds = (int)Math.Log2(bracketSize);
            int losersRounds = (winnersRounds - 1) * 2 + 1;

            for (int round = 1; round <= losersRounds; round++)
            {
                int matchesInRound = CalculateLosersMatchesInRound(round, bracketSize);

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
            var winnersGrouped = winnersMatches.GroupBy(m => m.RoundNo).OrderBy(g => g.Key).ToList();
            var losersGrouped = losersMatches.GroupBy(m => m.RoundNo).OrderBy(g => g.Key).ToList();

            for (int wr = 0; wr < winnersGrouped.Count - 1; wr++)
            {
                var winnersRound = winnersGrouped[wr].OrderBy(x => x.PositionInRound).ToList();
                var losersRoundMatches = losersGrouped
                    .FirstOrDefault(g => g.Key == wr + 1)?
                    .OrderBy(x => x.PositionInRound)
                    .ToList();

                if (losersRoundMatches is null || losersRoundMatches.Count == 0)
                    continue;

                for (int i = 0; i < winnersRound.Count; i++)
                {
                    int losersIndex;
                    if (winnersRound.Count > losersRoundMatches.Count)
                        losersIndex = i / 2;
                    else
                        losersIndex = i;

                    if (losersIndex >= losersRoundMatches.Count)
                        break;

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
            // Check for duplicate players
            var usedPlayers = assignments
                .Where(a => a.TpId.HasValue)
                .Select(a => a.TpId!.Value)
                .ToList();

            if (usedPlayers.Count != usedPlayers.Distinct().Count())
                throw new InvalidOperationException("Cannot assign same player to multiple slots.");

            // Verify all TpIds exist and belong to tournament
            if (usedPlayers.Any())
            {
                var validPlayers = await _db.TournamentPlayers
                    .Where(tp => tp.TournamentId == tournamentId && usedPlayers.Contains(tp.Id))
                    .Select(tp => tp.Id)
                    .ToListAsync(ct);

                var invalidPlayers = usedPlayers.Except(validPlayers).ToList();
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
