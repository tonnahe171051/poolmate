using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PoolMate.Api.Data.Migrations
{
    /// <summary>
    /// Migration to seed sample TournamentStages and Matches for testing Match History API.
    /// IMPORTANT: This migration assumes you already have data in Players, Tournaments, TournamentPlayers tables.
    /// Before running this migration, you MUST check and update the TournamentPlayer IDs in the seed data.
    /// </summary>
    public partial class SeedMatchHistoryData : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ============================================
            // IMPORTANT: READ THIS BEFORE RUNNING!
            // ============================================
            // This migration uses PLACEHOLDER IDs for TournamentPlayers.
            // You MUST update these IDs based on your actual data:
            // 
            // Run this query first:
            // SELECT tp.Id, tp.TournamentId, tp.PlayerId, tp.DisplayName
            // FROM TournamentPlayers tp
            // ORDER BY tp.TournamentId;
            //
            // Then replace the Player1TpId and Player2TpId values below.
            // ============================================

            // ============================================
            // SECTION 1: Seed TournamentStages
            // ============================================
            
            // Tournament 1 - Double Elimination (Completed)
            migrationBuilder.InsertData(
                table: "TournamentStages",
                columns: new[] { "TournamentId", "StageNo", "Type", "Status", "AdvanceCount", "Ordering", "CreatedAt", "UpdatedAt", "CompletedAt" },
                values: new object[] { 1, 1, 1, 2, null, 0, DateTime.UtcNow.AddDays(-30), DateTime.UtcNow.AddDays(-26), DateTime.UtcNow.AddDays(-26) }
            );

            // Tournament 2 - Single Elimination (Completed)
            migrationBuilder.InsertData(
                table: "TournamentStages",
                columns: new[] { "TournamentId", "StageNo", "Type", "Status", "AdvanceCount", "Ordering", "CreatedAt", "UpdatedAt", "CompletedAt" },
                values: new object[] { 2, 1, 0, 2, null, 0, DateTime.UtcNow.AddDays(-20), DateTime.UtcNow.AddDays(-18), DateTime.UtcNow.AddDays(-18) }
            );

            // Tournament 3 - Double Elimination (Completed)
            migrationBuilder.InsertData(
                table: "TournamentStages",
                columns: new[] { "TournamentId", "StageNo", "Type", "Status", "AdvanceCount", "Ordering", "CreatedAt", "UpdatedAt", "CompletedAt" },
                values: new object[] { 3, 1, 1, 2, null, 1, DateTime.UtcNow.AddDays(-15), DateTime.UtcNow.AddDays(-13), DateTime.UtcNow.AddDays(-13) }
            );

            // ============================================
            // SECTION 2: Seed Matches
            // ============================================
            // NOTE: We'll use SQL to get StageIds dynamically
            
            migrationBuilder.Sql(@"
                -- Get Stage IDs
                DECLARE @Stage1Id INT = (SELECT Id FROM TournamentStages WHERE TournamentId = 1 AND StageNo = 1);
                DECLARE @Stage2Id INT = (SELECT Id FROM TournamentStages WHERE TournamentId = 2 AND StageNo = 1);
                DECLARE @Stage3Id INT = (SELECT Id FROM TournamentStages WHERE TournamentId = 3 AND StageNo = 1);

                -- ============================================
                -- Tournament 1 Matches (Double Elimination)
                -- ============================================
                -- ⚠️ REPLACE TournamentPlayer IDs (1-8) with your actual IDs!
                
                -- Winners Bracket Round 1
                INSERT INTO Matches (TournamentId, StageId, Bracket, RoundNo, PositionInRound, 
                                    Player1TpId, Player2TpId, Player1SourceType, Player2SourceType, 
                                    RaceTo, Status, ScoreP1, ScoreP2, WinnerTpId, ScheduledUtc)
                VALUES 
                    (1, @Stage1Id, 0, 1, 1, 1, 2, 0, 0, 7, 2, 7, 3, 1, DATEADD(day, -30, GETUTCDATE())),
                    (1, @Stage1Id, 0, 1, 2, 3, 4, 0, 0, 7, 2, 4, 7, 4, DATEADD(day, -30, GETUTCDATE())),
                    (1, @Stage1Id, 0, 1, 3, 5, 6, 0, 0, 7, 2, 7, 5, 5, DATEADD(day, -30, GETUTCDATE())),
                    (1, @Stage1Id, 0, 1, 4, 7, 8, 0, 0, 7, 2, 2, 7, 8, DATEADD(day, -30, GETUTCDATE()));

                -- Winners Bracket Round 2
                DECLARE @Match1Id INT = (SELECT TOP 1 Id FROM Matches WHERE TournamentId = 1 AND RoundNo = 1 AND PositionInRound = 1);
                DECLARE @Match2Id INT = (SELECT TOP 1 Id FROM Matches WHERE TournamentId = 1 AND RoundNo = 1 AND PositionInRound = 2);
                DECLARE @Match3Id INT = (SELECT TOP 1 Id FROM Matches WHERE TournamentId = 1 AND RoundNo = 1 AND PositionInRound = 3);
                DECLARE @Match4Id INT = (SELECT TOP 1 Id FROM Matches WHERE TournamentId = 1 AND RoundNo = 1 AND PositionInRound = 4);

                INSERT INTO Matches (TournamentId, StageId, Bracket, RoundNo, PositionInRound, 
                                    Player1TpId, Player2TpId, 
                                    Player1SourceType, Player1SourceMatchId, 
                                    Player2SourceType, Player2SourceMatchId, 
                                    RaceTo, Status, ScoreP1, ScoreP2, WinnerTpId, ScheduledUtc)
                VALUES 
                    (1, @Stage1Id, 0, 2, 1, 1, 4, 1, @Match1Id, 1, @Match2Id, 9, 2, 9, 6, 1, DATEADD(day, -29, GETUTCDATE())),
                    (1, @Stage1Id, 0, 2, 2, 5, 8, 1, @Match3Id, 1, @Match4Id, 9, 2, 9, 7, 5, DATEADD(day, -29, GETUTCDATE()));

                -- Losers Bracket Round 1
                INSERT INTO Matches (TournamentId, StageId, Bracket, RoundNo, PositionInRound, 
                                    Player1TpId, Player2TpId, 
                                    Player1SourceType, Player1SourceMatchId, 
                                    Player2SourceType, Player2SourceMatchId, 
                                    RaceTo, Status, ScoreP1, ScoreP2, WinnerTpId, ScheduledUtc)
                VALUES 
                    (1, @Stage1Id, 1, 1, 1, 2, 3, 2, @Match1Id, 2, @Match2Id, 7, 2, 7, 4, 2, DATEADD(day, -28, GETUTCDATE())),
                    (1, @Stage1Id, 1, 1, 2, 6, 7, 2, @Match3Id, 2, @Match4Id, 7, 2, 5, 7, 7, DATEADD(day, -28, GETUTCDATE()));

                -- ============================================
                -- Tournament 2 Matches (Single Elimination)
                -- ============================================
                -- ⚠️ REPLACE TournamentPlayer IDs (9-14) with your actual IDs!
                
                INSERT INTO Matches (TournamentId, StageId, Bracket, RoundNo, PositionInRound, 
                                    Player1TpId, Player2TpId, Player1SourceType, Player2SourceType, 
                                    RaceTo, Status, ScoreP1, ScoreP2, WinnerTpId, ScheduledUtc)
                VALUES 
                    (2, @Stage2Id, 3, 1, 1, 9, 10, 0, 0, 7, 2, 7, 4, 9, DATEADD(day, -20, GETUTCDATE())),
                    (2, @Stage2Id, 3, 1, 2, 11, 12, 0, 0, 7, 2, 5, 7, 12, DATEADD(day, -20, GETUTCDATE())),
                    (2, @Stage2Id, 3, 1, 3, 13, 14, 0, 0, 7, 2, 7, 6, 13, DATEADD(day, -20, GETUTCDATE()));

                DECLARE @Match11Id INT = (SELECT TOP 1 Id FROM Matches WHERE TournamentId = 2 AND RoundNo = 1 AND PositionInRound = 1);
                DECLARE @Match12Id INT = (SELECT TOP 1 Id FROM Matches WHERE TournamentId = 2 AND RoundNo = 1 AND PositionInRound = 2);

                INSERT INTO Matches (TournamentId, StageId, Bracket, RoundNo, PositionInRound, 
                                    Player1TpId, Player2TpId, 
                                    Player1SourceType, Player1SourceMatchId, 
                                    Player2SourceType, Player2SourceMatchId, 
                                    RaceTo, Status, ScoreP1, ScoreP2, WinnerTpId, ScheduledUtc)
                VALUES 
                    (2, @Stage2Id, 3, 2, 1, 9, 12, 1, @Match11Id, 1, @Match12Id, 9, 2, 9, 7, 9, DATEADD(day, -19, GETUTCDATE()));

                -- ============================================
                -- Tournament 3 Matches (Double Elimination)
                -- ============================================
                -- ⚠️ REPLACE TournamentPlayer IDs (15-20) with your actual IDs!
                
                INSERT INTO Matches (TournamentId, StageId, Bracket, RoundNo, PositionInRound, 
                                    Player1TpId, Player2TpId, Player1SourceType, Player2SourceType, 
                                    RaceTo, Status, ScoreP1, ScoreP2, WinnerTpId, ScheduledUtc)
                VALUES 
                    (3, @Stage3Id, 0, 1, 1, 15, 16, 0, 0, 7, 2, 7, 5, 15, DATEADD(day, -15, GETUTCDATE())),
                    (3, @Stage3Id, 0, 1, 2, 17, 18, 0, 0, 7, 2, 6, 7, 18, DATEADD(day, -15, GETUTCDATE())),
                    (3, @Stage3Id, 0, 1, 3, 19, 20, 0, 0, 7, 2, 7, 3, 19, DATEADD(day, -15, GETUTCDATE()));

                DECLARE @Match16Id INT = (SELECT TOP 1 Id FROM Matches WHERE TournamentId = 3 AND RoundNo = 1 AND PositionInRound = 1);
                DECLARE @Match17Id INT = (SELECT TOP 1 Id FROM Matches WHERE TournamentId = 3 AND RoundNo = 1 AND PositionInRound = 2);

                INSERT INTO Matches (TournamentId, StageId, Bracket, RoundNo, PositionInRound, 
                                    Player1TpId, Player2TpId, 
                                    Player1SourceType, Player1SourceMatchId, 
                                    Player2SourceType, Player2SourceMatchId, 
                                    RaceTo, Status, ScoreP1, ScoreP2, WinnerTpId, ScheduledUtc)
                VALUES 
                    (3, @Stage3Id, 0, 2, 1, 15, 18, 1, @Match16Id, 1, @Match17Id, 9, 2, 9, 6, 15, DATEADD(day, -14, GETUTCDATE()));
            ");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Remove all matches from these tournaments
            migrationBuilder.Sql(@"
                DELETE FROM Matches WHERE TournamentId IN (1, 2, 3);
                DELETE FROM TournamentStages WHERE TournamentId IN (1, 2, 3);
            ");
        }
    }
}

