using Microsoft.EntityFrameworkCore;
using PoolMate.Api.Models;

namespace PoolMate.Api.Data.Seeds
{
    /// <summary>
    /// Seed data cho Match (các trận đấu)
    /// </summary>
    public static class MatchSeed
    {
        public static async Task SeedAsync(ApplicationDbContext context)
        {
            // Kiểm tra nếu đã có match thì không seed nữa
            if (await context.Matches.AnyAsync())
            {
                return;
            }

            // Lấy stages có tournament players
            var stages = await context.TournamentStages
                .Include(s => s.Tournament)
                    .ThenInclude(t => t.TournamentPlayers.Where(tp => tp.Status == TournamentPlayerStatus.Confirmed))
                .Where(s => s.Tournament.TournamentPlayers.Any())
                .ToListAsync();
            
            if (!stages.Any())
            {
                return;
            }

            // Lấy tables
            var tables = await context.TournamentTables
                .GroupBy(t => t.TournamentId)
                .ToDictionaryAsync(g => g.Key, g => g.ToList());

            var matches = new List<Match>();

            foreach (var stage in stages)
            {
                // Lấy confirmed players cho stage này
                var confirmedPlayers = stage.Tournament.TournamentPlayers
                    .Where(tp => tp.Status == TournamentPlayerStatus.Confirmed)
                    .OrderBy(tp => tp.Seed ?? Random.Shared.Next())
                    .ToList();

                if (confirmedPlayers.Count < 2)
                {
                    continue; // Cần ít nhất 2 players
                }

                // Lấy tables cho tournament này
                var tournamentTables = tables.ContainsKey(stage.TournamentId) 
                    ? tables[stage.TournamentId] 
                    : new List<TournamentTable>();

                // Tạo matches dựa trên bracket type
                if (stage.Type == BracketType.SingleElimination)
                {
                    matches.AddRange(CreateSingleEliminationMatches(stage, confirmedPlayers, tournamentTables));
                }
                else // DoubleElimination
                {
                    matches.AddRange(CreateDoubleEliminationMatches(stage, confirmedPlayers, tournamentTables));
                }
            }

            if (matches.Any())
            {
                await context.Matches.AddRangeAsync(matches);
                await context.SaveChangesAsync();
            }
        }

        private static List<Match> CreateSingleEliminationMatches(
            TournamentStage stage, 
            List<TournamentPlayer> players,
            List<TournamentTable> tables)
        {
            var matches = new List<Match>();
            int playerCount = players.Count;
            
            // Round 1: Tạo matches cho tất cả players
            int matchesInRound1 = playerCount / 2;
            
            for (int i = 0; i < matchesInRound1; i++)
            {
                var match = new Match
                {
                    TournamentId = stage.TournamentId,
                    StageId = stage.Id,
                    Bracket = BracketSide.Knockout,
                    RoundNo = 1,
                    PositionInRound = i + 1,
                    Player1TpId = players[i * 2].Id,
                    Player2TpId = players[i * 2 + 1].Id,
                    Player1SourceType = MatchSlotSourceType.Seed,
                    Player2SourceType = MatchSlotSourceType.Seed,
                    TableId = tables.Any() ? tables[i % tables.Count].Id : null,
                    RaceTo = stage.Tournament.WinnersRaceTo,
                    Status = stage.Status == StageStatus.Completed ? MatchStatus.Completed : MatchStatus.NotStarted
                };

                // Nếu stage completed, set scores
                if (stage.Status == StageStatus.Completed)
                {
                    var winner = Random.Shared.Next(2) == 0 ? players[i * 2] : players[i * 2 + 1];
                    match.WinnerTpId = winner.Id;
                    match.ScoreP1 = winner.Id == players[i * 2].Id ? match.RaceTo : Random.Shared.Next(match.RaceTo ?? 5);
                    match.ScoreP2 = winner.Id == players[i * 2 + 1].Id ? match.RaceTo : Random.Shared.Next(match.RaceTo ?? 5);
                }

                matches.Add(match);
            }

            return matches;
        }

        private static List<Match> CreateDoubleEliminationMatches(
            TournamentStage stage,
            List<TournamentPlayer> players,
            List<TournamentTable> tables)
        {
            var matches = new List<Match>();
            int playerCount = players.Count;
            
            // Winners Bracket Round 1
            int matchesInRound1 = playerCount / 2;
            
            for (int i = 0; i < matchesInRound1; i++)
            {
                var match = new Match
                {
                    TournamentId = stage.TournamentId,
                    StageId = stage.Id,
                    Bracket = BracketSide.Winners,
                    RoundNo = 1,
                    PositionInRound = i + 1,
                    Player1TpId = players[i * 2].Id,
                    Player2TpId = players[i * 2 + 1].Id,
                    Player1SourceType = MatchSlotSourceType.Seed,
                    Player2SourceType = MatchSlotSourceType.Seed,
                    TableId = tables.Any() ? tables[i % tables.Count].Id : null,
                    RaceTo = stage.Tournament.WinnersRaceTo,
                    Status = DetermineMatchStatus(stage, 1),
                    ScheduledUtc = stage.Tournament.StartUtc.AddHours(i)
                };

                // Set scores cho completed matches
                if (match.Status == MatchStatus.Completed)
                {
                    SetMatchScores(match, players[i * 2], players[i * 2 + 1]);
                }

                matches.Add(match);
            }

            // Losers Bracket Round 1 (chỉ tạo một vài matches mẫu)
            int losersMatchCount = Math.Min(2, matchesInRound1);
            for (int i = 0; i < losersMatchCount; i++)
            {
                var match = new Match
                {
                    TournamentId = stage.TournamentId,
                    StageId = stage.Id,
                    Bracket = BracketSide.Losers,
                    RoundNo = 1,
                    PositionInRound = i + 1,
                    Player1SourceType = MatchSlotSourceType.LoserOf,
                    Player2SourceType = MatchSlotSourceType.LoserOf,
                    Player1SourceMatchId = matches[i * 2].Id,
                    Player2SourceMatchId = matches[i * 2 + 1].Id,
                    TableId = tables.Any() ? tables[i % tables.Count].Id : null,
                    RaceTo = stage.Tournament.LosersRaceTo,
                    Status = MatchStatus.NotStarted,
                    ScheduledUtc = stage.Tournament.StartUtc.AddHours(matchesInRound1 + i)
                };

                matches.Add(match);
            }

            return matches;
        }

        private static MatchStatus DetermineMatchStatus(TournamentStage stage, int roundNo)
        {
            if (stage.Status == StageStatus.Completed)
            {
                return MatchStatus.Completed;
            }
            else if (stage.Status == StageStatus.InProgress)
            {
                // Round 1 completed, round 2 in progress
                return roundNo == 1 ? MatchStatus.Completed : MatchStatus.InProgress;
            }
            
            return MatchStatus.NotStarted;
        }

        private static void SetMatchScores(Match match, TournamentPlayer player1, TournamentPlayer player2)
        {
            var raceTo = match.RaceTo ?? 7;
            var winner = Random.Shared.Next(2) == 0 ? player1 : player2;
            
            match.WinnerTpId = winner.Id;
            match.ScoreP1 = winner.Id == player1.Id ? raceTo : Random.Shared.Next(raceTo);
            match.ScoreP2 = winner.Id == player2.Id ? raceTo : Random.Shared.Next(raceTo);
        }
    }
}

