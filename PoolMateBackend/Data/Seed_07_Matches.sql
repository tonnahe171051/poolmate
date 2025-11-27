-- ============================================
-- SEED DATA: MATCHES (Step 7 - FINAL)
-- ============================================
-- Tạo Matches với đầy đủ match history
-- Dependencies: Tournaments, TournamentStages, TournamentPlayers, TournamentTables

USE PoolMateDB;
GO

SET NOCOUNT ON;

PRINT '============================================';
PRINT '   SEED MATCHES - STEP 7 (FINAL)            ';
PRINT '============================================';
PRINT '';

-- =============================================
-- KIỂM TRA DEPENDENCIES
-- =============================================
PRINT 'Checking dependencies...';

DECLARE @TournamentCount INT, @StageCount INT, @TPCount INT;
SELECT @TournamentCount = COUNT(*) FROM Tournaments WHERE Id IN (1, 2, 3);
SELECT @StageCount = COUNT(*) FROM TournamentStages WHERE TournamentId IN (1, 2, 3);
SELECT @TPCount = COUNT(*) FROM TournamentPlayers WHERE TournamentId IN (1, 2, 3);

IF @TournamentCount < 3 OR @StageCount < 3 OR @TPCount < 10
BEGIN
    PRINT '  ❌ ERROR: Dependencies not found!';
    PRINT '  Tournaments: ' + CAST(@TournamentCount AS VARCHAR) + '/3';
    PRINT '  Stages: ' + CAST(@StageCount AS VARCHAR) + '/3';
    PRINT '  TournamentPlayers: ' + CAST(@TPCount AS VARCHAR) + '/10';
    PRINT '  💡 Please run previous seed scripts first';
    RETURN;
END

PRINT '  ✓ All dependencies found';
PRINT '';

-- =============================================
-- XÓA MATCHES CŨ
-- =============================================
PRINT 'Cleaning old matches...';

DELETE FROM Matches WHERE TournamentId IN (1, 2, 3);

PRINT '  ✓ Cleaned old matches';
PRINT '';

-- Reset Identity
DBCC CHECKIDENT ('Matches', RESEED, 0);

-- =============================================
-- TẠO MATCHES
-- =============================================
PRINT 'Creating matches...';

SET IDENTITY_INSERT Matches ON;

-- ==========================================
-- TOURNAMENT 3 (COMPLETED) - Full bracket
-- ==========================================
PRINT 'Tournament 3: Creating completed matches...';

-- Round 1: 2 matches (4 players → 2 winners)
INSERT INTO Matches (
    Id, TournamentId, StageId, Bracket, RoundNo, PositionInRound,
    Player1TpId, Player2TpId, RaceTo, Status,
    ScoreP1, ScoreP2, WinnerTpId, ScheduledUtc
)
VALUES
-- Match 1: Player 1 vs Player 2 (from TournamentPlayers IDs 9, 10)
(1, 3, 3, 0, 1, 1, 9, 10, 7, 2, 7, 5, 9, DATEADD(DAY, -23, GETUTCDATE())),
-- Match 2: Player 4 vs Player 5 (from TournamentPlayers IDs 11, 12)
(2, 3, 3, 0, 1, 2, 11, 12, 7, 2, 7, 4, 11, DATEADD(DAY, -23, GETUTCDATE()));

-- Round 2: Finals (2 winners → 1 champion)
INSERT INTO Matches (
    Id, TournamentId, StageId, Bracket, RoundNo, PositionInRound,
    Player1TpId, Player2TpId, RaceTo, Status,
    ScoreP1, ScoreP2, WinnerTpId, ScheduledUtc
)
VALUES
(3, 3, 3, 0, 2, 1, 9, 11, 9, 2, 9, 7, 9, DATEADD(DAY, -22, GETUTCDATE()));

PRINT '  ✓ Tournament 3: 3 matches (completed)';

-- ==========================================
-- TOURNAMENT 1 (IN PROGRESS) - Partial matches
-- ==========================================
PRINT 'Tournament 1: Creating in-progress matches...';

-- Round 1: 2 completed matches (4 out of 5 players fought)
INSERT INTO Matches (
    Id, TournamentId, StageId, Bracket, RoundNo, PositionInRound,
    Player1TpId, Player2TpId, RaceTo, Status,
    ScoreP1, ScoreP2, WinnerTpId, ScheduledUtc
)
VALUES
(4, 1, 1, 0, 1, 1, 1, 2, 7, 2, 7, 4, 1, DATEADD(DAY, -2, GETUTCDATE())),
(5, 1, 1, 0, 1, 2, 3, 4, 7, 2, 5, 7, 4, DATEADD(DAY, -2, GETUTCDATE()));

-- Round 2: 1 in-progress match
INSERT INTO Matches (
    Id, TournamentId, StageId, Bracket, RoundNo, PositionInRound,
    Player1TpId, Player2TpId, RaceTo, Status,
    ScoreP1, ScoreP2, WinnerTpId, ScheduledUtc
)
VALUES
(6, 1, 1, 0, 2, 1, 1, 4, 9, 1, 3, 2, NULL, GETUTCDATE()); -- InProgress

PRINT '  ✓ Tournament 1: 3 matches (2 completed, 1 in-progress)';

-- ==========================================
-- TOURNAMENT 2 (UPCOMING) - No matches yet
-- ==========================================
PRINT 'Tournament 2: No matches (tournament not started)';

SET IDENTITY_INSERT Matches OFF;

PRINT '';

-- =============================================
-- VERIFICATION
-- =============================================
PRINT '============================================';
PRINT '   VERIFICATION                             ';
PRINT '============================================';
PRINT '';

-- Tổng kết matches
SELECT 
    t.Id AS TournamentId,
    t.Name AS Tournament,
    COUNT(m.Id) AS TotalMatches,
    SUM(CASE WHEN m.Status = 2 THEN 1 ELSE 0 END) AS Completed,
    SUM(CASE WHEN m.Status = 1 THEN 1 ELSE 0 END) AS InProgress,
    SUM(CASE WHEN m.Status = 0 THEN 1 ELSE 0 END) AS NotStarted
FROM Tournaments t
LEFT JOIN Matches m ON t.Id = m.TournamentId
WHERE t.Id IN (1, 2, 3)
GROUP BY t.Id, t.Name
ORDER BY t.Id;

PRINT '';

-- Chi tiết matches
PRINT 'Match Details:';
SELECT 
    m.Id AS MatchId,
    m.TournamentId,
    m.RoundNo,
    tp1.DisplayName AS Player1,
    tp2.DisplayName AS Player2,
    CAST(m.ScoreP1 AS VARCHAR) + '-' + CAST(m.ScoreP2 AS VARCHAR) AS Score,
    tpw.DisplayName AS Winner,
    CASE m.Status
        WHEN 0 THEN 'NotStarted'
        WHEN 1 THEN 'InProgress'
        WHEN 2 THEN 'Completed'
    END AS Status
FROM Matches m
LEFT JOIN TournamentPlayers tp1 ON m.Player1TpId = tp1.Id
LEFT JOIN TournamentPlayers tp2 ON m.Player2TpId = tp2.Id
LEFT JOIN TournamentPlayers tpw ON m.WinnerTpId = tpw.Id
ORDER BY m.TournamentId, m.RoundNo, m.Id;

PRINT '';
PRINT '============================================';
PRINT '   MATCHES SEED COMPLETED!                  ';
PRINT '============================================';
PRINT '';
PRINT '🎯 Matches Created:';
PRINT '   Tournament 1: 3 matches (2 completed, 1 in-progress)';
PRINT '   Tournament 2: 0 matches (upcoming)';
PRINT '   Tournament 3: 3 matches (all completed - full bracket)';
PRINT '';
PRINT '✅ ALL SEED DATA COMPLETED!';
PRINT '';
PRINT '📊 Summary:';
PRINT '   - Users: 5';
PRINT '   - Players: 5';
PRINT '   - Tournaments: 3';
PRINT '   - TournamentPlayers: 12';
PRINT '   - TournamentStages: 3';
PRINT '   - TournamentTables: 8';
PRINT '   - Matches: 6 (5 completed, 1 in-progress)';
PRINT '';
PRINT '🚀 Ready to test APIs!';
PRINT '   - Login: player1@test.com / Player123!';
PRINT '   - Match History: GET /api/players/1/match-history';
PRINT '   - My Profiles: GET /api/players/my-profiles';
PRINT '============================================';

