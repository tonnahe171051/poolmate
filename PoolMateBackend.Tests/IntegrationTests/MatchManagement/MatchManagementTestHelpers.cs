using Microsoft.EntityFrameworkCore;
using PoolMate.Api.Data;
using PoolMate.Api.Models;

namespace PoolMateBackend.Tests.IntegrationTests.MatchManagement;

/// <summary>
/// Helper class for creating test data for Match Management integration tests.
/// Provides reusable methods to create tournaments, stages, matches, and tables.
/// </summary>
public static class MatchManagementTestHelpers
{
    /// <summary>
    /// Creates a complete tournament with a single elimination bracket.
    /// </summary>
    /// <param name="db">Database context</param>
    /// <param name="ownerUserId">Tournament owner user ID</param>
    /// <param name="playerCount">Number of players (must be power of 2: 4, 8, 16, etc.)</param>
    /// <param name="bracketType">Type of bracket (SingleElimination or DoubleElimination)</param>
    /// <param name="raceTo">Race-to value for matches</param>
    /// <param name="isMultiStage">Whether this is a multi-stage tournament</param>
    /// <param name="advanceCount">Number of players to advance (for multi-stage)</param>
    /// <returns>The created tournament with stage and matches</returns>
    public static async Task<Tournament> CreateTournamentWithBracketAsync(
        ApplicationDbContext db,
        string ownerUserId,
        int playerCount = 4,
        BracketType bracketType = BracketType.SingleElimination,
        int raceTo = 7,
        bool isMultiStage = false,
        int? advanceCount = null)
    {
        // Create tournament
        var tournament = new Tournament
        {
            Name = $"Test Tournament {Guid.NewGuid().ToString()[..8]}",
            Description = "Integration Test Tournament",
            StartUtc = DateTime.UtcNow,
            OwnerUserId = ownerUserId,
            PlayerType = PlayerType.Singles,
            BracketType = bracketType,
            GameType = GameType.NineBall,
            WinnersRaceTo = raceTo,
            LosersRaceTo = raceTo,
            FinalsRaceTo = raceTo,
            BracketOrdering = BracketOrdering.Seeded,
            Status = TournamentStatus.InProgress,
            IsStarted = true,
            IsPublic = true,
            IsMultiStage = isMultiStage,
            AdvanceToStage2Count = advanceCount,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        db.Tournaments.Add(tournament);
        await db.SaveChangesAsync();

        // Create stage
        var stage = new TournamentStage
        {
            TournamentId = tournament.Id,
            StageNo = 1,
            Type = bracketType,
            Status = StageStatus.InProgress,
            Ordering = BracketOrdering.Seeded,
            AdvanceCount = advanceCount,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        db.TournamentStages.Add(stage);
        await db.SaveChangesAsync();

        // Create tournament players
        var existingPlayers = await db.Players.Take(playerCount).ToListAsync();
        var tournamentPlayers = new List<PoolMate.Api.Models.TournamentPlayer>();

        for (int i = 0; i < playerCount; i++)
        {
            var player = existingPlayers[i];
            var tp = new PoolMate.Api.Models.TournamentPlayer
            {
                TournamentId = tournament.Id,
                PlayerId = player.Id,
                Seed = i + 1,
                Status = TournamentPlayerStatus.Confirmed,
                DisplayName = player.FullName,
                Nickname = player.Nickname,
                Email = player.Email,
                Phone = player.Phone,
                Country = player.Country,
                City = player.City,
                SkillLevel = player.SkillLevel
            };

            db.TournamentPlayers.Add(tp);
            tournamentPlayers.Add(tp);
        }

        await db.SaveChangesAsync();

        // Create matches based on bracket type
        if (bracketType == BracketType.SingleElimination)
        {
            await CreateSingleEliminationMatchesAsync(db, tournament.Id, stage.Id, tournamentPlayers, raceTo);
        }
        else
        {
            await CreateDoubleEliminationMatchesAsync(db, tournament.Id, stage.Id, tournamentPlayers, raceTo);
        }

        return tournament;
    }

    /// <summary>
    /// Creates matches for a single elimination bracket.
    /// </summary>
    private static async Task CreateSingleEliminationMatchesAsync(
        ApplicationDbContext db,
        int tournamentId,
        int stageId,
        List<PoolMate.Api.Models.TournamentPlayer> players,
        int raceTo)
    {
        var playerCount = players.Count;
        var rounds = (int)Math.Log2(playerCount);

        // Create all matches for the bracket
        var allMatches = new List<Match>();

        // Round 1 (e.g., for 4 players: 2 matches)
        var round1MatchCount = playerCount / 2;
        for (int i = 0; i < round1MatchCount; i++)
        {
            var match = new Match
            {
                TournamentId = tournamentId,
                StageId = stageId,
                Bracket = BracketSide.Winners,
                RoundNo = 1,
                PositionInRound = i + 1,
                Player1TpId = players[i * 2].Id,
                Player2TpId = players[i * 2 + 1].Id,
                RaceTo = raceTo,
                Status = MatchStatus.NotStarted,
                RowVersion = new byte[8]
            };

            db.Matches.Add(match);
            allMatches.Add(match);
        }

        await db.SaveChangesAsync();

        // Create subsequent rounds (without players yet - they'll be auto-propagated)
        for (int round = 2; round <= rounds; round++)
        {
            var matchesInRound = (int)Math.Pow(2, rounds - round);
            for (int pos = 1; pos <= matchesInRound; pos++)
            {
                var match = new Match
                {
                    TournamentId = tournamentId,
                    StageId = stageId,
                    Bracket = BracketSide.Winners,
                    RoundNo = round,
                    PositionInRound = pos,
                    RaceTo = raceTo,
                    Status = MatchStatus.NotStarted,
                    RowVersion = new byte[8]
                };

                db.Matches.Add(match);
                allMatches.Add(match);
            }
        }

        await db.SaveChangesAsync();

        // Link matches (NextWinnerMatchId)
        for (int round = 1; round < rounds; round++)
        {
            var currentRoundMatches = allMatches.Where(m => m.RoundNo == round).OrderBy(m => m.PositionInRound).ToList();
            var nextRoundMatches = allMatches.Where(m => m.RoundNo == round + 1).OrderBy(m => m.PositionInRound).ToList();

            for (int i = 0; i < currentRoundMatches.Count; i++)
            {
                var nextMatchIndex = i / 2;
                currentRoundMatches[i].NextWinnerMatchId = nextRoundMatches[nextMatchIndex].Id;
            }
        }

        await db.SaveChangesAsync();
    }

    /// <summary>
    /// Creates matches for a double elimination bracket (simplified version).
    /// </summary>
    private static async Task CreateDoubleEliminationMatchesAsync(
        ApplicationDbContext db,
        int tournamentId,
        int stageId,
        List<PoolMate.Api.Models.TournamentPlayer> players,
        int raceTo)
    {
        // For simplicity, create a basic double elimination structure
        // Winners bracket similar to single elimination
        var playerCount = players.Count;
        var rounds = (int)Math.Log2(playerCount);

        var allMatches = new List<Match>();

        // Winners Round 1
        var round1MatchCount = playerCount / 2;
        for (int i = 0; i < round1MatchCount; i++)
        {
            var match = new Match
            {
                TournamentId = tournamentId,
                StageId = stageId,
                Bracket = BracketSide.Winners,
                RoundNo = 1,
                PositionInRound = i + 1,
                Player1TpId = players[i * 2].Id,
                Player2TpId = players[i * 2 + 1].Id,
                RaceTo = raceTo,
                Status = MatchStatus.NotStarted,
                RowVersion = new byte[8]
            };

            db.Matches.Add(match);
            allMatches.Add(match);
        }

        await db.SaveChangesAsync();

        // Create Winners Round 2 (Finals for 4-player bracket)
        if (playerCount >= 4)
        {
            var finalsMatch = new Match
            {
                TournamentId = tournamentId,
                StageId = stageId,
                Bracket = BracketSide.Winners,
                RoundNo = 2,
                PositionInRound = 1,
                RaceTo = raceTo,
                Status = MatchStatus.NotStarted,
                RowVersion = new byte[8]
            };

            db.Matches.Add(finalsMatch);
            allMatches.Add(finalsMatch);

            await db.SaveChangesAsync();

            // Link R1 matches to finals
            var r1Matches = allMatches.Where(m => m.RoundNo == 1).ToList();
            foreach (var m in r1Matches)
            {
                m.NextWinnerMatchId = finalsMatch.Id;
            }

            await db.SaveChangesAsync();
        }

        // Add basic losers bracket structure
        var losersR1 = new Match
        {
            TournamentId = tournamentId,
            StageId = stageId,
            Bracket = BracketSide.Losers,
            RoundNo = 1,
            PositionInRound = 1,
            RaceTo = raceTo,
            Status = MatchStatus.NotStarted,
            RowVersion = new byte[8]
        };

        db.Matches.Add(losersR1);
        await db.SaveChangesAsync();
    }

    /// <summary>
    /// Creates a match in InProgress state with scores.
    /// </summary>
    public static async Task<Match> CreateMatchInProgressAsync(
        ApplicationDbContext db,
        int tournamentId,
        int stageId,
        int player1TpId,
        int player2TpId,
        int scoreP1,
        int scoreP2,
        int raceTo = 7)
    {
        var match = new Match
        {
            TournamentId = tournamentId,
            StageId = stageId,
            Bracket = BracketSide.Winners,
            RoundNo = 1,
            PositionInRound = 1,
            Player1TpId = player1TpId,
            Player2TpId = player2TpId,
            RaceTo = raceTo,
            ScoreP1 = scoreP1,
            ScoreP2 = scoreP2,
            Status = MatchStatus.InProgress,
            RowVersion = new byte[8]
        };

        db.Matches.Add(match);
        await db.SaveChangesAsync();

        return match;
    }

    /// <summary>
    /// Creates a completed match with a winner.
    /// </summary>
    public static async Task<Match> CreateCompletedMatchAsync(
        ApplicationDbContext db,
        int tournamentId,
        int stageId,
        int player1TpId,
        int player2TpId,
        int winnerTpId,
        int scoreP1,
        int scoreP2,
        int raceTo = 7)
    {
        var match = new Match
        {
            TournamentId = tournamentId,
            StageId = stageId,
            Bracket = BracketSide.Winners,
            RoundNo = 1,
            PositionInRound = 1,
            Player1TpId = player1TpId,
            Player2TpId = player2TpId,
            RaceTo = raceTo,
            ScoreP1 = scoreP1,
            ScoreP2 = scoreP2,
            WinnerTpId = winnerTpId,
            Status = MatchStatus.Completed,
            RowVersion = new byte[8]
        };

        db.Matches.Add(match);
        await db.SaveChangesAsync();

        return match;
    }

    /// <summary>
    /// Creates tournament tables for testing.
    /// </summary>
    public static async Task<List<TournamentTable>> CreateTablesAsync(
        ApplicationDbContext db,
        int tournamentId,
        int count = 3)
    {
        var tables = new List<TournamentTable>();

        for (int i = 1; i <= count; i++)
        {
            var table = new TournamentTable
            {
                TournamentId = tournamentId,
                Label = $"Table {i}",
                Manufacturer = "Diamond",
                SizeFoot = 9.0m,
                Status = TableStatus.Open,
                IsStreaming = false
            };

            db.TournamentTables.Add(table);
            tables.Add(table);
        }

        await db.SaveChangesAsync();
        return tables;
    }

    /// <summary>
    /// Assigns a table to a match.
    /// </summary>
    public static async Task AssignTableToMatchAsync(
        ApplicationDbContext db,
        int matchId,
        int tableId)
    {
        var match = await db.Matches.FindAsync(matchId);
        var table = await db.TournamentTables.FindAsync(tableId);

        if (match != null && table != null)
        {
            match.TableId = tableId;
            table.Status = TableStatus.InUse;
            await db.SaveChangesAsync();
        }
    }

    /// <summary>
    /// Creates a match with both player slots having the same player (edge case).
    /// </summary>
    public static async Task<Match> CreateMatchWithSamePlayerInBothSlotsAsync(
        ApplicationDbContext db,
        int tournamentId,
        int stageId,
        int playerTpId,
        int raceTo = 7)
    {
        var match = new Match
        {
            TournamentId = tournamentId,
            StageId = stageId,
            Bracket = BracketSide.Winners,
            RoundNo = 1,
            PositionInRound = 1,
            Player1TpId = playerTpId,
            Player2TpId = playerTpId, // Same player in both slots
            RaceTo = raceTo,
            Status = MatchStatus.NotStarted,
            RowVersion = new byte[8]
        };

        db.Matches.Add(match);
        await db.SaveChangesAsync();

        return match;
    }

    /// <summary>
    /// Creates a match with one empty slot (Player2 is TBD).
    /// </summary>
    public static async Task<Match> CreateMatchWithEmptySlotAsync(
        ApplicationDbContext db,
        int tournamentId,
        int stageId,
        int player1TpId,
        int raceTo = 7)
    {
        var match = new Match
        {
            TournamentId = tournamentId,
            StageId = stageId,
            Bracket = BracketSide.Winners,
            RoundNo = 2,
            PositionInRound = 1,
            Player1TpId = player1TpId,
            Player2TpId = null, // Empty slot (TBD)
            RaceTo = raceTo,
            Status = MatchStatus.NotStarted,
            RowVersion = new byte[8]
        };

        db.Matches.Add(match);
        await db.SaveChangesAsync();

        return match;
    }

    /// <summary>
    /// Completes a stage.
    /// </summary>
    public static async Task CompleteStageAsync(ApplicationDbContext db, int stageId)
    {
        var stage = await db.TournamentStages.FindAsync(stageId);
        if (stage != null)
        {
            stage.Status = StageStatus.Completed;
            stage.CompletedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
        }
    }
}

