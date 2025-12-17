﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using PoolMate.Api.Common;
using PoolMate.Api.Data;
using PoolMate.Api.Dtos.Tournament;
using PoolMate.Api.Models;

namespace PoolMate.Api.Services
{
    public class BracketService : IBracketService
    {
        private readonly ApplicationDbContext _db;
        private readonly IMatchService _matchService;

        public BracketService(
            ApplicationDbContext db,
            IMatchService matchService)
        {
            _db = db;
            _matchService = matchService;
        }

        // Single validation helper (unified) — throws ValidationException for input validation
        private void ValidatePlayerCountForCreate(Models.Tournament t, int count)
        {
            if (count < 2)
                throw new ValidationException("At least two players are required to create a bracket.");

            if (t.IsMultiStage && t.AdvanceToStage2Count.HasValue)
            {
                var requiredPlayers = Math.Max(t.AdvanceToStage2Count.Value + 1, 5);
                if (count < requiredPlayers)
                    throw new ValidationException($"Multi-stage bracket requires at least {requiredPlayers} players assigned in stage 1 for advance count {t.AdvanceToStage2Count.Value}.");
            }
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

            var finalSize = GetOptimalBracketSize(players.Count);

            var stage1Ordering = t.IsMultiStage ? t.Stage1Ordering : t.BracketOrdering;

            var stage1 = BuildStagePreview(
                tournament: t,
                stageNo: 1,
                type: t.BracketType,
                ordering: stage1Ordering,
                size: finalSize,
                players: players,
                advanceCount: t.IsMultiStage ? t.AdvanceToStage2Count : null
            );

            // Do not preview Stage 2 for multi-stage tournaments here — Stage 2
            // is only created after Stage 1 completes and therefore cannot be
            // meaningfully previewed before Stage 1 is persisted.
            StageDto? stage2 = null;

            return new BracketPreviewDto
            {
                TournamentId = tournamentId,
                IsMultiStage = t.IsMultiStage,
                Stage1 = stage1,
                Stage2 = stage2
            };
        }

        public async Task<StageDto> PreviewStageAsync(int tournamentId, int stageNo, CancellationToken ct)
        {
            if (stageNo <= 0) throw new InvalidOperationException("Invalid stage number.");

            var tournament = await _db.Tournaments
                .Include(t => t.Stages)
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.Id == tournamentId, ct)
                ?? throw new KeyNotFoundException("Tournament not found");

            if (!tournament.IsMultiStage)
                throw new InvalidOperationException("Tournament is not multi-stage; no separate Stage 2 preview available.");

            if (stageNo == 1)
            {
                // Delegate to existing preview logic for stage 1
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

                var finalSize = GetOptimalBracketSize(players.Count);

                var ordering = tournament.IsMultiStage ? tournament.Stage1Ordering : tournament.BracketOrdering;
                return BuildStagePreview(tournament, 1, tournament.BracketType, ordering, finalSize, players, tournament.IsMultiStage ? tournament.AdvanceToStage2Count : null);
            }

            // stageNo == 2
            var stage1 = tournament.Stages.FirstOrDefault(s => s.StageNo == 1)
                ?? throw new InvalidOperationException("Stage 1 not found for tournament.");

            var stageEval = await EvaluateStageAsync(stage1.Id, ct);

            if (!stage1.AdvanceCount.HasValue)
                throw new InvalidOperationException("Stage 2 not configured for this tournament.");

            var targetAdvance = stageEval.TargetAdvance ?? stage1.AdvanceCount.Value;
            if (targetAdvance < 4)
                throw new InvalidOperationException("Stage 2 requires at least four advancing players.");

            if ((targetAdvance & (targetAdvance - 1)) != 0)
                throw new InvalidOperationException("Stage 2 bracket size must be a power of two.");

            var survivors = stageEval.SurvivorTpIds.Take(targetAdvance).ToList();
            if (!survivors.Any())
                throw new InvalidOperationException("No survivors available to preview Stage 2.");

            var playerSeeds = await GetPlayerSeedsAsync(survivors, ct);

            var ordering2 = tournament.Stage2Ordering;
            return BuildStagePreview(tournament, 2, BracketType.SingleElimination, ordering2, targetAdvance, playerSeeds, null);
        }

        

        public async Task CreateAsync(int tournamentId, CreateBracketRequest? request, CancellationToken ct)
        {
            try
            {
                var t = await _db.Tournaments
                    .Include(x => x.Stages)
                    .FirstOrDefaultAsync(x => x.Id == tournamentId, ct)
                    ?? throw new KeyNotFoundException("Tournament not found");

                // Defensive validation: multi-stage tournaments must not use Single Elimination for Stage 1
                if (t.IsMultiStage && t.BracketType == BracketType.SingleElimination)
                    throw new InvalidOperationException("Tournament configured as multi-stage cannot use Single Elimination for Stage 1.");

                var anyMatches = await _db.Matches.AnyAsync(m => m.TournamentId == tournamentId, ct);
                if (anyMatches) throw new InvalidOperationException("Bracket already created.");

                var stage1Ordering = t.IsMultiStage ? t.Stage1Ordering : t.BracketOrdering;

                var tournamentPlayers = await _db.TournamentPlayers
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

                ValidatePlayerCountForCreate(t, tournamentPlayers.Count);

                List<PlayerSeed?> slots;

                if (request?.Type == BracketCreationType.Manual && request.ManualAssignments != null)
                {
                    await ValidateManualAssignments(tournamentId, request.ManualAssignments, ct);
                    slots = await ConvertAssignmentsToSlots(tournamentId, request.ManualAssignments, ct);
                }
                else
                {
                    var finalSize = GetOptimalBracketSize(tournamentPlayers.Count);

                    if (finalSize < tournamentPlayers.Count)
                        throw new InvalidOperationException($"Bracket size {finalSize} is smaller than the number of registered players ({tournamentPlayers.Count}).");

                    slots = MakeSlots(tournamentPlayers, finalSize, stage1Ordering);
                }

                if (slots.Count < tournamentPlayers.Count)
                    throw new InvalidOperationException($"Bracket size ({slots.Count}) cannot be smaller than the number of registered players ({tournamentPlayers.Count}).");

                var assignedIds = slots
                    .Where(s => s is not null && s.TpId != default)
                    .Select(s => s!.TpId)
                    .ToList();

                if (assignedIds.Count != tournamentPlayers.Count)
                    throw new InvalidOperationException($"All {tournamentPlayers.Count} registered players must be assigned to the bracket before creation. Currently assigned {assignedIds.Count}.");

                if (assignedIds.Distinct().Count() != tournamentPlayers.Count)
                    throw new InvalidOperationException("Each player must appear exactly once in the bracket.");

                for (int i = 0; i < slots.Count / 2; i++)
                {
                    if (slots[2 * i] is null && slots[2 * i + 1] is null)
                    {
                        var err = "Bracket contains an empty first-round match. Please adjust slot assignments (null-null pair not allowed).";
                        throw new PoolMate.Api.Common.ValidationException(err, new[] { err });
                    }
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

                await _matchService.ProcessAutoAdvancementsAsync(t.Id, ct);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"OUTER ERROR in CreateAsync: {ex.Message}");
                throw;
            }
        }

        public async Task<BracketDto> GetAsync(int tournamentId, CancellationToken ct)
        {
            await _matchService.ProcessAutoAdvancementsAsync(tournamentId, ct);

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
                stages.Add(BuildStageDto(tournament, stage, stageMatches));
            }

            return new BracketDto
            {
                TournamentId = tournamentId,
                IsMultiStage = tournament.IsMultiStage,
                Stages = stages
            };
        }

        private StageDto BuildStageDto(Tournament tournament, TournamentStage stage, List<Match> stageMatches)
        {
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

            var matchLookup = stageMatches.ToDictionary(m => m.Id, m => m);

            var bracketGroups = stageMatches.GroupBy(m => m.Bracket);

            foreach (var bracketGroup in bracketGroups.OrderBy(g => g.Key))
            {
                var bracketDto = new BracketSideDto
                {
                    BracketSide = bracketGroup.Key
                };

                var roundGroups = bracketGroup.GroupBy(m => m.RoundNo).OrderBy(g => g.Key);

                foreach (var roundGroup in roundGroups)
                {
                    var roundDto = new RoundDto
                    {
                        RoundNo = roundGroup.Key
                    };

                    foreach (var match in roundGroup.OrderBy(m => m.PositionInRound))
                    {
                        var dto = MapToMatchDto(match, stageEval, stageEval.TournamentCompletionAvailable);
                        dto.ProjectedPlayer1 = ResolveProjectedPlayerDescription(match, 1, matchLookup, new HashSet<int>());
                        dto.ProjectedPlayer2 = ResolveProjectedPlayerDescription(match, 2, matchLookup, new HashSet<int>());
                        roundDto.Matches.Add(dto);
                    }

                    bracketDto.Rounds.Add(roundDto);
                }

                stageDto.Brackets.Add(bracketDto);
            }

            return stageDto;
        }

        public async Task<BracketDto> GetFilteredAsync(int tournamentId, BracketFilterRequest filter, CancellationToken ct)
        {
            // Get full bracket first
            var fullBracket = await GetAsync(tournamentId, ct);

            // Apply filter
            return ApplyBracketFilter(fullBracket, filter);
        }

        // Return only the winners-side (or knockout) brackets for each stage
        public async Task<BracketDto> GetWinnersSideAsync(int tournamentId, CancellationToken ct)
        {
            var full = await GetAsync(tournamentId, ct);
            var res = new BracketDto
            {
                TournamentId = full.TournamentId,
                IsMultiStage = full.IsMultiStage,
                Stages = new List<StageDto>()
            };

            foreach (var s in full.Stages)
            {
                var stage = new StageDto
                {
                    StageNo = s.StageNo,
                    Type = s.Type,
                    Ordering = s.Ordering,
                    BracketSize = s.BracketSize,
                    Status = s.Status,
                    CompletedAt = s.CompletedAt,
                    AdvanceCount = s.AdvanceCount,
                    CanComplete = s.CanComplete,
                    Brackets = s.Brackets.Where(b => b.BracketSide == BracketSide.Winners || b.BracketSide == BracketSide.Knockout).ToList()
                };

                if (stage.Brackets.Any()) res.Stages.Add(stage);
            }

            return res;
        }

        // Return only the losers-side brackets for each stage
        public async Task<BracketDto> GetLosersSideAsync(int tournamentId, CancellationToken ct)
        {
            var full = await GetAsync(tournamentId, ct);
            var res = new BracketDto
            {
                TournamentId = full.TournamentId,
                IsMultiStage = full.IsMultiStage,
                Stages = new List<StageDto>()
            };

            foreach (var s in full.Stages)
            {
                var stage = new StageDto
                {
                    StageNo = s.StageNo,
                    Type = s.Type,
                    Ordering = s.Ordering,
                    BracketSize = s.BracketSize,
                    Status = s.Status,
                    CompletedAt = s.CompletedAt,
                    AdvanceCount = s.AdvanceCount,
                    CanComplete = s.CanComplete,
                    Brackets = s.Brackets.Where(b => b.BracketSide == BracketSide.Losers).ToList()
                };

                if (stage.Brackets.Any()) res.Stages.Add(stage);
            }

            return res;
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


        public async Task<StageCompletionResultDto> CompleteStageAsync(int tournamentId, int stageNo, CompleteStageRequest request, CancellationToken ct)
        {
            var tournament = await _db.Tournaments
                .Include(t => t.Stages)
                .FirstOrDefaultAsync(t => t.Id == tournamentId, ct)
                ?? throw new KeyNotFoundException("Tournament not found.");

            var stage = tournament.Stages.FirstOrDefault(s => s.StageNo == stageNo)
                ?? throw new InvalidOperationException("Stage not found.");

            if (!tournament.IsStarted || tournament.Status == TournamentStatus.Upcoming)
                throw new InvalidOperationException("Tournament must be started before completing a stage.");

            if (tournament.Status == TournamentStatus.Completed)
                throw new InvalidOperationException("Tournament has already been completed.");

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
                await _matchService.ProcessAutoAdvancementsAsync(tournament.Id, ct);
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
            await _matchService.ProcessAutoAdvancementsAsync(tournamentId, ct);

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

            var countedMatches = matches.ToList();

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

            var matchesTotal = countedMatches.Count;
            var matchesCompleted = countedMatches.Count(m => m.Status == MatchStatus.Completed);
            var matchesInProgress = countedMatches.Count(m => m.Status == MatchStatus.InProgress);
            var matchesNotStarted = countedMatches.Count(m => m.Status == MatchStatus.NotStarted && !m.ScheduledUtc.HasValue);
            var matchesScheduled = countedMatches.Count(m => m.Status == MatchStatus.NotStarted && m.ScheduledUtc.HasValue);

            summary.Matches.Total = matchesTotal;
            summary.Matches.Completed = matchesCompleted;
            summary.Matches.InProgress = matchesInProgress;
            summary.Matches.NotStarted = matchesNotStarted;
            summary.Matches.Scheduled = matchesScheduled;
            summary.Matches.WinnersSide = countedMatches.Count(m => m.Bracket == BracketSide.Winners);
            summary.Matches.LosersSide = countedMatches.Count(m => m.Bracket == BracketSide.Losers);
            summary.Matches.KnockoutSide = countedMatches.Count(m => m.Bracket == BracketSide.Knockout);
            summary.Matches.FinalsSide = countedMatches.Count(m => m.Bracket == BracketSide.Finals);

            summary.CompletionPercent = matchesTotal == 0
                ? 0
                : Math.Round((double)matchesCompleted / matchesTotal * 100, 2);

            var tables = tournament.Tables.ToList();
            summary.Tables.Total = tables.Count;
            summary.Tables.Open = tables.Count(t => t.Status == TableStatus.Open);
            summary.Tables.InUse = tables.Count(t => t.Status == TableStatus.InUse);
            summary.Tables.Closed = tables.Count(t => t.Status == TableStatus.Closed);

            var activeMatchesWithTables = countedMatches
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

            var placementMatches = countedMatches.Select(ToPlacementMatch).ToList();
            var (championDto, runnerUpDto, additionalPlacements) = ApplyPlacements(placementMatches, statsLookup);

            summary.Champion = championDto;
            summary.RunnerUp = runnerUpDto;
            if (additionalPlacements.Count > 0)
                summary.AdditionalPlacements.AddRange(additionalPlacements);

            return summary;
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
            var tournamentState = await _db.Tournaments
                .Where(t => t.Id == tournamentId)
                .Select(t => new { t.IsStarted, t.IsMultiStage })
                .FirstOrDefaultAsync(ct)
                ?? throw new KeyNotFoundException("Tournament not found.");

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
                    IsEliminated = false
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
            var maxStageNo = matches.Any() ? matches.Max(m => m.StageNo) : 0;

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

            // Xác định final match của giải (highest stage)
            var finalMatchIds = matches
                .Where(m => m.StageNo == maxStageNo)
                .Where(m => m.Status == MatchStatus.Completed && m.WinnerTpId.HasValue)
                .Where(m => m.NextWinnerMatchId == null && m.NextLoserMatchId == null)
                .Select(m => m.Id)
                .ToHashSet();

            var championIds = matches
                .Where(m => finalMatchIds.Contains(m.Id))
                .Select(m => m.WinnerTpId!.Value)
                .ToHashSet();

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

            // Xác định IsEliminated
            foreach (var stat in stats.Values)
            {
                if (!tournamentState.IsStarted || stat.MatchesPlayed == 0)
                {
                    stat.IsEliminated = false;
                    continue;
                }

                var isActive = activePlayers.Contains(stat.TournamentPlayerId);
                var isChampion = championIds.Contains(stat.TournamentPlayerId);
                
                // Với multi-stage: Player phải ở highest stage để được coi là còn sống
                if (tournamentState.IsMultiStage && maxStageNo > 1)
                {
                    var playerMaxStage = stat.LastStageNo ?? 0;
                    var isInCurrentStage = playerMaxStage == maxStageNo;
                    
                    // Eliminated nếu:
                    // - Không ở stage cao nhất HOẶC
                    // - Ở stage cao nhất nhưng không active và không phải champion
                    stat.IsEliminated = !isInCurrentStage || (!isActive && !isChampion);
                }
                else
                {
                    // Single stage: Logic cũ
                    stat.IsEliminated = !(isActive || isChampion);
                }
            }

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

            // Tìm stage cao nhất trước để tránh lấy nhầm final match của stage 1
            var maxStageNo = matches.Any() ? matches.Max(m => m.StageNo) : 0;

            // Tìm final match CHỈ Ở STAGE CAO NHẤT
            var finalMatch = matches
                .Where(m => m.StageNo == maxStageNo)
                .Where(m => m.Status == MatchStatus.Completed && m.WinnerTpId.HasValue)
                .Where(m => m.NextWinnerMatchId == null && m.NextLoserMatchId == null)
                .OrderByDescending(m => m.RoundNo)
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

        private sealed record MatchSlotReference(Match Match, int SlotIndex, MatchSlotSourceType? SourceType);

        private StageDto BuildStagePreview(
            Tournament tournament,
            int stageNo,
            BracketType type,
            BracketOrdering ordering,
            int size,
            List<PlayerSeed>? players,
            int? advanceCount)
        {
            var slots = MakeSlots(players, size, ordering);

            var stage = new TournamentStage
            {
                Id = -stageNo,
                TournamentId = tournament.Id,
                StageNo = stageNo,
                Type = type,
                Ordering = ordering,
                Status = StageStatus.NotStarted,
                AdvanceCount = advanceCount
            };

            var previewTournament = new Tournament
            {
                Id = tournament.Id,
                IsMultiStage = tournament.IsMultiStage,
                BracketType = tournament.BracketType,
                Stage1Ordering = tournament.Stage1Ordering,
                Stage2Ordering = tournament.Stage2Ordering,
                AdvanceToStage2Count = tournament.AdvanceToStage2Count,
                Stages = new List<TournamentStage> { stage }
            };
            stage.Tournament = previewTournament;

            List<Match> matches = type == BracketType.SingleElimination
                ? CreatePreviewSingleMatches(tournament, stage, slots)
                : CreatePreviewDoubleMatches(tournament, stage, slots, advanceCount);

            return BuildStageDto(previewTournament, stage, matches);
        }

        private List<PlayerSeed?> MakeSlots(List<PlayerSeed>? players, int size, BracketOrdering ordering)
        {
            var slots = Enumerable.Repeat<PlayerSeed?>(null, size).ToList();
            if (players is null || players.Count == 0) return slots;

            if (ordering == BracketOrdering.Random)
            {
                var list = players.ToList();
                FisherYates(list);
                int matchCount = size / 2;
                int i = 0;
                foreach (var p in list)
                {
                    var matchIndex = i % matchCount;
                    var pos1 = matchIndex * 2;
                    var pos2 = pos1 + 1;
                    if (slots[pos1] is null)
                        slots[pos1] = p;
                    else
                        slots[pos2] = p;
                    i++;
                }

                return slots;
            }

            var seeded = players.Where(p => p.Seed.HasValue).OrderBy(p => p.Seed!.Value).ToList();
            var unseeded = players.Where(p => !p.Seed.HasValue).ToList();
            FisherYates(unseeded);

            var positions = BuildSeedPositions(size);
            int idx = 0;

            // Place seeded players in order into the seed positions
            for (; idx < seeded.Count && idx < positions.Length; idx++)
            {
                slots[positions[idx]] = seeded[idx];
            }

            // Fill remaining positions with unseeded players
            int u = 0;
            // Distribute remaining unseeded players across matches so BYEs are spread
            var remaining = positions.Skip(idx).ToList();

            // Build groups per match and compute how many seeded players each group already has.
            var groupMap = remaining
                .GroupBy(p => p / 2)
                .Select(g => new { MatchIndex = g.Key, Positions = g.ToList() })
                .OrderBy(g => g.MatchIndex)
                .ToList();

            var groupsWithSeedCount = groupMap
                .Select(g => new
                {
                    g.MatchIndex,
                    g.Positions,
                    SeededCount = g.Positions.Count(pos => slots[pos] is not null)
                })
                .ToList();

            // Order groups so that groups with fewer seeded players are filled first
            var fillOrder = groupsWithSeedCount
                .OrderBy(g => g.SeededCount)
                .ThenBy(g => g.MatchIndex)
                .ToList();

            // First pass: place at most one unseeded player per match (group), prioritizing groups with 0 seeded
            foreach (var g in fillOrder)
            {
                if (u >= unseeded.Count) break;
                foreach (var pos in g.Positions)
                {
                    if (u >= unseeded.Count) break;
                    if (slots[pos] is null)
                    {
                        slots[pos] = unseeded[u++];
                        break;
                    }
                }
            }

            // Second pass: fill remaining empty positions with remaining unseeded players in match order
            foreach (var g in groupMap)
            {
                if (u >= unseeded.Count) break;
                foreach (var pos in g.Positions)
                {
                    if (u >= unseeded.Count) break;
                    if (slots[pos] is null)
                    {
                        slots[pos] = unseeded[u++];
                    }
                }
            }

            return slots;
        }

        private List<Match> CreatePreviewSingleMatches(Tournament tournament, TournamentStage stage, List<PlayerSeed?> slots)
        {
            var matches = CreateSingleBracketMatches(tournament.Id, stage, slots, BracketSide.Knockout);
            int nextMatchId = -1;
            AssignSyntheticMatchIds(matches, ref nextMatchId);
            WireSingleEliminationMatches(matches);
            PopulatePreviewSeedSnapshots(matches, slots, tournament.Id);
            return matches;
        }

        private List<Match> CreatePreviewDoubleMatches(Tournament tournament, TournamentStage stage, List<PlayerSeed?> slots, int? advanceCount)
        {
            var bracketSize = slots.Count;
            var trimPlan = advanceCount.HasValue && advanceCount.Value > 0
                ? CalculateTrimPlan(bracketSize, advanceCount.Value)
                : null;

            var winners = CreateWinnersBracketMatches(tournament.Id, stage, slots, trimPlan?.WinnersRounds);
            var losers = trimPlan is { LosersRounds: > 0 }
                ? CreateLosersBracketMatches(tournament.Id, stage, bracketSize, trimPlan.LosersRounds)
                : CreateLosersBracketMatches(tournament.Id, stage, bracketSize, null);
            var finals = !stage.AdvanceCount.HasValue
                ? CreateFinalsBracketMatches(tournament.Id, stage)
                : new List<Match>();

            int nextMatchId = -1;
            AssignSyntheticMatchIds(winners, ref nextMatchId);
            AssignSyntheticMatchIds(losers, ref nextMatchId);
            AssignSyntheticMatchIds(finals, ref nextMatchId);

            PopulatePreviewSeedSnapshots(winners, slots, tournament.Id);

            var fullWBRounds = (int)Math.Log2(bracketSize);
            var feedRounds = trimPlan?.WinnersRounds ?? fullWBRounds;
            MapDoubleEliminationFlow(winners, losers, finals, feedRounds);

            return winners
                .Concat(losers)
                .Concat(finals)
                .ToList();
        }

        private static void AssignSyntheticMatchIds(IEnumerable<Match> matches, ref int nextMatchId)
        {
            foreach (var match in matches)
            {
                match.Id = nextMatchId--;
                if (match.RowVersion == null)
                {
                    match.RowVersion = Array.Empty<byte>();
                }
            }
        }

        private static void PopulatePreviewSeedSnapshots(IEnumerable<Match> matches, List<PlayerSeed?> slots, int tournamentId)
        {
            if (slots.Count == 0)
                return;

            var firstRoundMatches = matches
                .Where(m => (m.Bracket == BracketSide.Winners || m.Bracket == BracketSide.Knockout) && m.RoundNo == 1)
                .OrderBy(m => m.PositionInRound)
                .ToList();

            int cursor = 0;
            foreach (var match in firstRoundMatches)
            {
                var p1 = cursor < slots.Count ? slots[cursor++] : null;
                var p2 = cursor < slots.Count ? slots[cursor++] : null;

                if (p1 is not null)
                {
                    match.Player1Tp = CreateSnapshotPlayer(p1, tournamentId);
                }

                if (p2 is not null)
                {
                    match.Player2Tp = CreateSnapshotPlayer(p2, tournamentId);
                }
            }
        }

        private static TournamentPlayer CreateSnapshotPlayer(PlayerSeed seed, int tournamentId)
        {
            return new TournamentPlayer
            {
                Id = seed.TpId,
                TournamentId = tournamentId,
                DisplayName = seed.Name,
                Seed = seed.Seed,
                Country = seed.Country,
                SkillLevel = seed.FargoRating
            };
        }

        private static int[] BuildSeedPositions(int size)
        {
            if (size < 2)
                return Array.Empty<int>();
            if ((size & 1) == 1)
                throw new InvalidOperationException("Bracket size must be a power of two for seed positions.");
            if (size == 2)
                return new[] { 0, 1 };

            var half = size / 2;
            var prev = BuildSeedPositions(half);
            var res = new int[size];
            int idx = 0;
            foreach (var p in prev)
                res[idx++] = p;
            foreach (var p in prev)
                res[idx++] = size - 1 - p;
            return res;
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

                // Compute how many winners rounds we should keep.
                // Option A: keep all winners rounds except the final winners round
                // (i.e. fullDepth - 1). This yields winners progression like 8 -> 4 -> 2
                // for a 16-slot bracket (fullDepth=4 => keep=3).
                int bracketSize = slots.Count;
                var fullWBRounds = (int)Math.Log2(bracketSize);
                TrimPlan? trimPlan = stage.AdvanceCount.HasValue && stage.AdvanceCount.Value > 0
                    ? CalculateTrimPlan(bracketSize, stage.AdvanceCount.Value)
                    : null;

                var winnersBuildLimit = trimPlan?.WinnersRounds;

                // 1. Tạo Winners bracket (limited by keepWinnersRounds if present)
                var winnersMatches = BuildWinnersBracket(tournamentId, stage, slots, ct, winnersBuildLimit);

                // 2. Determine necessary losers rounds (based on winners kept)
                var losersMatches = new List<Match>();
                if (trimPlan is { LosersRounds: > 0 } planWithLosers)
                {
                    losersMatches = BuildLosersBracket(tournamentId, stage, bracketSize, ct, planWithLosers.LosersRounds);
                }
                else
                {
                    // Single-stage double elimination: build full losers bracket
                    losersMatches = BuildLosersBracket(tournamentId, stage, bracketSize, ct);
                }

                // 3. Create finals only when this is a single-stage bracket (no AdvanceCount)
                // and we kept all winners rounds (i.e., champion path exists). For multi-stage
                // Stage 1 we should NOT create the grand-final here because Stage 2 will be
                // responsible for its own finals.
                List<Match> finalsMatches = new List<Match>();
                var keptAllWinnersRounds = !stage.AdvanceCount.HasValue;
                if (keptAllWinnersRounds)
                {
                    finalsMatches = BuildFinalsBracket(tournamentId, stage, ct);
                }

                // 4. Map NextWinner/NextLoser relationships
                var feedRounds = trimPlan?.WinnersRounds ?? fullWBRounds;
                MapDoubleEliminationFlow(winnersMatches, losersMatches, finalsMatches, feedRounds);

                _db.SaveChanges();
            }
        }


        private void BuildAndPersistSingle(int tournamentId, TournamentStage stage,
            List<PlayerSeed?> slots, CancellationToken ct, BracketSide bracket = BracketSide.Knockout)
        {
            var created = CreateSingleBracketMatches(tournamentId, stage, slots, bracket);
            _db.Matches.AddRange(created);
            _db.SaveChanges();

            WireSingleEliminationMatches(created);
            _db.SaveChanges();
        }

        private List<Match> CreateSingleBracketMatches(int tournamentId, TournamentStage stage,
            List<PlayerSeed?> slots, BracketSide bracket)
        {
            var n = slots.Count;
            if (n == 0)
                throw new InvalidOperationException("Single elimination bracket requires at least two slots.");

            if ((n & 1) == 1)
                throw new InvalidOperationException("Single elimination bracket requires an even number of slots.");

            var roundNo = 1;
            var matchesPerRound = n / 2;
            var cursor = 0;

            var created = new List<Match>();

            while (matchesPerRound > 0)
            {
                for (int pos = 1; pos <= matchesPerRound; pos++)
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
                        Status = MatchStatus.NotStarted,
                    });
                }

                roundNo++;
                matchesPerRound /= 2;
            }

            return created;
        }

        private void WireSingleEliminationMatches(List<Match> created)
        {
            var grouped = created.GroupBy(m => m.RoundNo).OrderBy(g => g.Key).ToList();
            for (int r = 0; r < grouped.Count - 1; r++)
            {
                var cur = grouped[r].OrderBy(x => x.PositionInRound).ToList();
                var nxt = grouped[r + 1].OrderBy(x => x.PositionInRound).ToList();
                for (int i = 0; i < cur.Count; i++)
                {
                    var nextPos = i / 2;
                    cur[i].NextWinnerMatchId = nxt[nextPos].Id;
                    SetSlotSource(nxt[nextPos], (i % 2 == 0) ? 1 : 2, MatchSlotSourceType.WinnerOf, cur[i].Id);
                }
            }
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

        private sealed record TrimPlan(int WinnersRounds, int LosersRounds);

        private TrimPlan CalculateTrimPlan(int bracketSize, int advanceCount)
        {
            var fullWinnersRounds = (int)Math.Log2(bracketSize);
            var losersPattern = GetLosersPattern(bracketSize);

            if (advanceCount < 2 || advanceCount >= bracketSize)
                return new TrimPlan(fullWinnersRounds, losersPattern.Length);

            if ((advanceCount & (advanceCount - 1)) != 0)
                return new TrimPlan(fullWinnersRounds, losersPattern.Length);

            var winnersTarget = Math.Max(1, advanceCount / 2);
            var losersTarget = Math.Max(1, advanceCount - winnersTarget);

            var winnersRatio = (double)bracketSize / winnersTarget;
            var winnersRounds = Math.Clamp(CeilLog2(winnersRatio), 1, fullWinnersRounds);

            var losersRounds = DetermineLosersRoundsForTarget(losersPattern, losersTarget);
            var feedRequirement = DetermineTargetLosersRound(winnersRounds, losersPattern.Length);
            losersRounds = Math.Max(losersRounds, feedRequirement);

            if (losersPattern.Length == 0)
                losersRounds = 0;
            else if (losersRounds == 0)
                losersRounds = Math.Min(1, losersPattern.Length);

            return new TrimPlan(winnersRounds, losersRounds);
        }

        private static int CeilLog2(double value)
        {
            if (value <= 1d) return 0;
            return (int)Math.Ceiling(Math.Log2(value));
        }

        private static int DetermineLosersRoundsForTarget(IReadOnlyList<int> pattern, int target)
        {
            if (pattern.Count == 0 || target <= 0)
                return 0;

            for (int i = 0; i < pattern.Count; i++)
            {
                if (pattern[i] <= target)
                    return i + 1;
            }

            return pattern.Count;
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

        private List<Match> BuildWinnersBracket(int tournamentId, TournamentStage stage, List<PlayerSeed?> slots, CancellationToken ct, int? keepRounds)
        {
            var created = CreateWinnersBracketMatches(tournamentId, stage, slots, keepRounds);
            _db.Matches.AddRange(created);
            _db.SaveChanges();
            return created;
        }

        private List<Match> CreateWinnersBracketMatches(int tournamentId, TournamentStage stage, List<PlayerSeed?> slots, int? keepRounds)
        {
            int n = slots.Count;
            int roundNo = 1;
            int matches = n / 2;
            int cursor = 0;

            var created = new List<Match>();

            while (matches > 0)
            {
                if (keepRounds.HasValue && roundNo > keepRounds.Value)
                    break;
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
                        Status = MatchStatus.NotStarted,
                    });
                }

                roundNo++;
                matches /= 2;
            }

            return created;
        }

        private List<Match> BuildLosersBracket(int tournamentId, TournamentStage stage, int bracketSize, CancellationToken ct, int? maxRound = null)
        {
            var losersMatches = CreateLosersBracketMatches(tournamentId, stage, bracketSize, maxRound);
            _db.Matches.AddRange(losersMatches);
            _db.SaveChanges();
            return losersMatches;
        }

        private List<Match> CreateLosersBracketMatches(int tournamentId, TournamentStage stage, int bracketSize, int? maxRound)
        {
            var losersMatches = new List<Match>();
            var pattern = GetLosersPattern(bracketSize);
            var losersRounds = pattern.Length;

            if (losersRounds == 0)
                return losersMatches;

            for (int round = 1; round <= losersRounds; round++)
            {
                if (maxRound.HasValue && round > maxRound.Value)
                    break;
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
                        Status = MatchStatus.NotStarted,
                    });
                }
            }

            return losersMatches;
        }

        private List<Match> BuildFinalsBracket(int tournamentId, TournamentStage stage, CancellationToken ct)
        {
            var finalsMatches = CreateFinalsBracketMatches(tournamentId, stage);
            _db.Matches.AddRange(finalsMatches);
            _db.SaveChanges();
            return finalsMatches;
        }

        private static List<Match> CreateFinalsBracketMatches(int tournamentId, TournamentStage stage)
        {
            return new List<Match>
            {
                new Match
                {
                    TournamentId = tournamentId,
                    StageId = stage.Id,
                    Bracket = BracketSide.Finals,
                    RoundNo = 1,
                    PositionInRound = 1,
                    Status = MatchStatus.NotStarted,
                }
            };
        }

        private void MapDoubleEliminationFlow(List<Match> winnersMatches, List<Match> losersMatches, List<Match> finalsMatches, int winnersFeedRounds)
        {
            // 1. Map Winners internal flow
            MapWinnersBracketFlow(winnersMatches);

            // 2. Map Winners to Losers flow (do this before mapping losers internal flow)
            // so that loser-slot sources are allocated first. This avoids a situation
            // where the losers internal mapping fills all slots with WinnerOf sources
            // and leaves no slot available for LoserOf assignments.
            MapWinnersToLosersFlow(winnersMatches, losersMatches, winnersFeedRounds);

            // 3. Map Losers internal flow
            MapLosersBracketFlow(losersMatches);

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

        private void MapWinnersToLosersFlow(List<Match> winnersMatches, List<Match> losersMatches, int maxWinnersRoundForLosers)
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
                if (maxWinnersRoundForLosers > 0 && winnersRoundNumber > maxWinnersRoundForLosers)
                    break;
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

            // Assign players to slots using the explicit slot positions the client provided
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

        // Recursively resolve a textual projection for a match slot.
        // slotIndex: 1 or 2
        private string? ResolveProjectedPlayerDescription(Match match, int slotIndex, Dictionary<int, Match> matchLookup, HashSet<int> visited)
        {
            if (match == null) return null;
            if (visited.Contains(match.Id)) return null; // avoid cycles
            visited.Add(match.Id);

            // If slot already has an assigned player, return the display name
            if (slotIndex == 1 && match.Player1Tp != null)
                return match.Player1Tp.DisplayName;
            if (slotIndex == 2 && match.Player2Tp != null)
                return match.Player2Tp.DisplayName;

            MatchSlotSourceType? sourceType = slotIndex == 1 ? match.Player1SourceType : match.Player2SourceType;
            int? sourceMatchId = slotIndex == 1 ? match.Player1SourceMatchId : match.Player2SourceMatchId;

            if (sourceType is null || sourceMatchId is null)
                return null;

            if (!matchLookup.TryGetValue(sourceMatchId.Value, out var src))
                return null;

            // If source match has seeded players, show their seed names combined
            string DescribeMatchPlayers(Match m)
            {
                var p1 = m.Player1Tp != null ? m.Player1Tp.DisplayName : null;
                var p2 = m.Player2Tp != null ? m.Player2Tp.DisplayName : null;
                if (p1 != null && p2 != null)
                    return $"({p1} vs {p2})";
                if (p1 != null) return p1;
                if (p2 != null) return p2;
                return "(TBD)";
            }

            switch (sourceType)
            {
                case MatchSlotSourceType.WinnerOf:
                    // If source match has both participants, show Winner(of p1 vs p2)
                    if (src.Player1Tp != null || src.Player2Tp != null)
                        return $"Winner {DescribeMatchPlayers(src)}";
                    // else try to resolve recursively each side (use first non-null)
                    var a = ResolveProjectedPlayerDescription(src, 1, matchLookup, visited);
                    var b = ResolveProjectedPlayerDescription(src, 2, matchLookup, visited);
                    if (a != null && b != null) return $"Winner ({a} vs {b})";
                    if (a != null) return a;
                    if (b != null) return b;
                    return "(TBD)";

                case MatchSlotSourceType.LoserOf:
                    if (src.Player1Tp != null || src.Player2Tp != null)
                        return $"Loser {DescribeMatchPlayers(src)}";
                    // recursively attempt to describe losers
                    var la = ResolveProjectedPlayerDescription(src, 1, matchLookup, visited);
                    var lb = ResolveProjectedPlayerDescription(src, 2, matchLookup, visited);
                    if (la != null && lb != null) return $"Loser ({la} vs {lb})";
                    if (la != null) return la;
                    if (lb != null) return lb;
                    return "(TBD)";

                default:
                    return null;
            }
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
