using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
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
    public class MatchService : IMatchService
    {
        private readonly ApplicationDbContext _db;
        private readonly IMatchLockService _lockService;
        private readonly IHubContext<TournamentHub> _hubContext;
        private readonly ILogger<MatchService> _logger;

        public MatchService(
            ApplicationDbContext db,
            IMatchLockService lockService,
            IHubContext<TournamentHub> hubContext,
            ILogger<MatchService> logger)
        {
            _db = db;
            _lockService = lockService;
            _hubContext = hubContext;
            _logger = logger;
        }

        public async Task<MatchDto> GetMatchAsync(int matchId, CancellationToken ct)
        {
            var match = await LoadMatchForScoringAsync(matchId, ct);
            var stageEval = await BracketComputationHelper.EvaluateStageAsync(_db, match.StageId, ct);
            return BracketComputationHelper.MapToMatchDto(match, stageEval, stageEval.TournamentCompletionAvailable);
        }

        public async Task<MatchDto> UpdateMatchAsync(int matchId, UpdateMatchRequest request, CancellationToken ct)
        {
            if (request is null)
                throw new ArgumentNullException(nameof(request));

            var match = await LoadMatchAggregateAsync(matchId, ct);
            await EnsureTournamentIsActiveAsync(match.TournamentId, ct);

            if (match.Stage.Status == StageStatus.Completed)
                throw new InvalidOperationException("Stage has been completed and cannot be modified.");

            if (!await IsMatchManuallyEditable(match, ct))
                throw new InvalidOperationException("This match cannot be modified manually; it is auto-propagated by bracket logic.");

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
                await ProcessAutoAdvancementsAsync(match.TournamentId, ct);

            var stageEval = await BracketComputationHelper.EvaluateStageAsync(_db, match.StageId, ct);
            var dto = BracketComputationHelper.MapToMatchDto(match, stageEval, stageEval.TournamentCompletionAvailable);

            var tableAssignmentChanged = previousTableId != match.TableId;
            await PublishRealtimeUpdates(match, dto, bracketChanged: match.Status == MatchStatus.Completed || tableAssignmentChanged, ct);

            return dto;
        }

        public async Task<MatchDto> CorrectMatchResultAsync(int matchId, CorrectMatchResultRequest request, CancellationToken ct)
        {
            if (request is null)
                throw new ArgumentNullException(nameof(request));

            var match = await LoadMatchAggregateAsync(matchId, ct);
            await EnsureTournamentIsActiveAsync(match.TournamentId, ct);

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
            await ProcessAutoAdvancementsAsync(match.TournamentId, ct);

            var stageEval = await BracketComputationHelper.EvaluateStageAsync(_db, match.StageId, ct);
            var dto = BracketComputationHelper.MapToMatchDto(match, stageEval, stageEval.TournamentCompletionAvailable);

            await PublishRealtimeUpdates(match, dto, bracketChanged: true, ct);

            return dto;
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
                await EnsureTournamentIsActiveAsync(match.TournamentId, ct);
                ValidateScoringAccess(match, actor, ensureNotCompleted: true);
                ApplyRowVersion(match, request.RowVersion);
                ValidateScoreInput(match, request.ScoreP1, request.ScoreP2, request.RaceTo);

                var intendsToUpdateScoresOrRace = request.ScoreP1.HasValue || request.ScoreP2.HasValue || request.RaceTo.HasValue;
                if (intendsToUpdateScoresOrRace && (!match.Player1TpId.HasValue || !match.Player2TpId.HasValue))
                    throw new InvalidOperationException("Both players must be assigned before updating scores or race-to.");

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

                var stageEval = await BracketComputationHelper.EvaluateStageAsync(_db, match.StageId, ct);
                var dto = BracketComputationHelper.MapToMatchDto(match, stageEval, stageEval.TournamentCompletionAvailable);

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
                await EnsureTournamentIsActiveAsync(match.TournamentId, ct);
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
                await ProcessAutoAdvancementsAsync(match.TournamentId, ct);

                var stageEval = await BracketComputationHelper.EvaluateStageAsync(_db, match.StageId, ct);
                var dto = BracketComputationHelper.MapToMatchDto(match, stageEval, stageEval.TournamentCompletionAvailable);

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

        public async Task ProcessAutoAdvancementsAsync(int tournamentId, CancellationToken ct)
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

        private async Task EnsureTournamentIsActiveAsync(int tournamentId, CancellationToken ct)
        {
            var state = await _db.Tournaments
                .Where(t => t.Id == tournamentId)
                .Select(t => new { t.IsStarted, t.Status })
                .FirstOrDefaultAsync(ct)
                ?? throw new InvalidOperationException("Tournament not found for the requested match.");

            if (!state.IsStarted || state.Status == TournamentStatus.Upcoming)
                throw new InvalidOperationException("Tournament must be started before updating matches.");

            if (state.Status == TournamentStatus.Completed)
                throw new InvalidOperationException("Tournament has already been completed.");
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

            var stageEval = await BracketComputationHelper.EvaluateStageAsync(_db, match.StageId, ct);
            return BracketComputationHelper.MapToMatchDto(match, stageEval, stageEval.TournamentCompletionAvailable);
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

        private async Task<bool> IsMatchManuallyEditable(Match match, CancellationToken ct)
        {
            if (match.Stage is null)
            {
                var stage = await _db.TournamentStages.FirstOrDefaultAsync(s => s.Id == match.StageId, ct);
                if (stage is null) return true;
                match.Stage = stage;
            }

            if (!match.Stage.AdvanceCount.HasValue)
                return true;

            var advance = match.Stage.AdvanceCount.Value;
            if (advance <= 0) return true;

            var firstRoundCount = await _db.Matches.CountAsync(m => m.StageId == match.StageId && m.Bracket == BracketSide.Winners && m.RoundNo == 1, ct);
            if (firstRoundCount <= 0) return true;

            var bracketSize = firstRoundCount * 2;
            var plan = CalculateTrimPlan(bracketSize, advance);

            if (match.Bracket == BracketSide.Winners)
            {
                return match.RoundNo <= plan.WinnersRounds;
            }

            if (match.Bracket == BracketSide.Finals)
            {
                return false;
            }

            if (match.Bracket == BracketSide.Losers)
            {
                if (plan.LosersRounds == 0)
                    return false;
                return match.RoundNo <= plan.LosersRounds;
            }

            return true;
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

        private static int DetermineTargetLosersRound(int winnersRoundNumber, int totalLosersRounds)
        {
            if (totalLosersRounds == 0)
                return 0;

            if (winnersRoundNumber == 1)
                return totalLosersRounds >= 1 ? 1 : 0;

            var round = 2 * (winnersRoundNumber - 1);
            return round <= totalLosersRounds ? round : 0;
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
    }
}
