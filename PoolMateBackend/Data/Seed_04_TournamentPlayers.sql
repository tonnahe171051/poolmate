-- ============================================
-- SEED DATA: TOURNAMENT PLAYERS (Step 4)
-- ============================================
-- Tạo TournamentPlayers (registrations)
-- Dependencies: Tournaments, Players

USE PoolMateDB;
GO

SET NOCOUNT ON;

PRINT '============================================';
PRINT '   SEED TOURNAMENT PLAYERS - STEP 4         ';
PRINT '============================================';
PRINT '';

-- =============================================
-- KIỂM TRA DEPENDENCIES
-- =============================================
PRINT 'Checking dependencies...';

DECLARE @TournamentCount INT, @PlayerCount INT;
SELECT @TournamentCount = COUNT(*) FROM Tournaments;
SELECT @PlayerCount = COUNT(*) FROM Players WHERE Email LIKE 'player%@test.com';

IF @TournamentCount < 3
BEGIN
    PRINT '  ❌ ERROR: Tournaments not found!';
    PRINT '  💡 Please run Seed_03_Tournaments.sql first';
    RETURN;
END

IF @PlayerCount < 5
BEGIN
    PRINT '  ❌ ERROR: Players not found!';
    PRINT '  💡 Please run Seed_01_Users.sql first';
    RETURN;
END

PRINT '  ✓ Tournaments found: ' + CAST(@TournamentCount AS VARCHAR);
PRINT '  ✓ Players found: ' + CAST(@PlayerCount AS VARCHAR);
PRINT '';

-- =============================================
-- XÓA TOURNAMENT PLAYERS CŨ
-- =============================================
PRINT 'Cleaning old tournament players...';

DELETE FROM TournamentPlayers WHERE TournamentId IN (1, 2, 3);

PRINT '  ✓ Cleaned old registrations';
PRINT '';

-- Reset Identity
DBCC CHECKIDENT ('TournamentPlayers', RESEED, 0);

-- =============================================
-- TẠO TOURNAMENT PLAYERS
-- =============================================
PRINT 'Creating tournament player registrations...';

SET IDENTITY_INSERT TournamentPlayers ON;

-- Tournament 1 (InProgress) - 5 players (tất cả đã linked)
INSERT INTO TournamentPlayers (
    Id, TournamentId, PlayerId, DisplayName, Status, 
    SkillLevel, Nickname, Email, Phone, Country, City
)
SELECT 
    ROW_NUMBER() OVER (ORDER BY p.Id), -- Id
    1, -- TournamentId
    p.Id, -- PlayerId
    p.FullName, -- DisplayName
    1, -- Status = Confirmed
    p.SkillLevel,
    p.Nickname,
    p.Email,
    p.Phone,
    p.Country,
    p.City
FROM Players p
WHERE p.Email LIKE 'player%@test.com'
ORDER BY p.Id;

PRINT '  ✓ Tournament 1: Registered 5 players';

-- Tournament 2 (Upcoming) - 3 players
DECLARE @Offset INT = (SELECT COUNT(*) FROM TournamentPlayers);

INSERT INTO TournamentPlayers (
    Id, TournamentId, PlayerId, DisplayName, Status,
    SkillLevel, Nickname, Email, Phone, Country, City
)
SELECT 
    @Offset + ROW_NUMBER() OVER (ORDER BY p.Id), -- Id
    2, -- TournamentId
    p.Id, -- PlayerId
    p.FullName,
    1, -- Confirmed
    p.SkillLevel,
    p.Nickname,
    p.Email,
    p.Phone,
    p.Country,
    p.City
FROM Players p
WHERE p.Id IN (1, 2, 3) -- Player 1, 2, 3
ORDER BY p.Id;

PRINT '  ✓ Tournament 2: Registered 3 players';

-- Tournament 3 (Completed) - 4 players
SET @Offset = (SELECT COUNT(*) FROM TournamentPlayers);

INSERT INTO TournamentPlayers (
    Id, TournamentId, PlayerId, DisplayName, Status,
    SkillLevel, Nickname, Email, Phone, Country, City
)
SELECT 
    @Offset + ROW_NUMBER() OVER (ORDER BY p.Id), -- Id
    3, -- TournamentId
    p.Id, -- PlayerId
    p.FullName,
    1, -- Confirmed
    p.SkillLevel,
    p.Nickname,
    p.Email,
    p.Phone,
    p.Country,
    p.City
FROM Players p
WHERE p.Id IN (1, 2, 4, 5) -- Player 1, 2, 4, 5
ORDER BY p.Id;

PRINT '  ✓ Tournament 3: Registered 4 players';

SET IDENTITY_INSERT TournamentPlayers OFF;

PRINT '';

-- =============================================
-- VERIFICATION
-- =============================================
PRINT '============================================';
PRINT '   VERIFICATION                             ';
PRINT '============================================';
PRINT '';

-- Tổng kết
SELECT 
    t.Id AS TournamentId,
    t.Name AS TournamentName,
    COUNT(tp.Id) AS RegisteredPlayers
FROM Tournaments t
LEFT JOIN TournamentPlayers tp ON t.Id = tp.TournamentId
WHERE t.Id IN (1, 2, 3)
GROUP BY t.Id, t.Name
ORDER BY t.Id;

PRINT '';

-- Chi tiết registrations
PRINT 'Registration Details:';
SELECT 
    tp.Id AS TpId,
    tp.TournamentId,
    tp.PlayerId,
    tp.DisplayName,
    CASE tp.Status
        WHEN 0 THEN 'Unconfirmed'
        WHEN 1 THEN 'Confirmed'
        WHEN 2 THEN 'CheckedIn'
        WHEN 3 THEN 'Withdrawn'
    END AS Status
FROM TournamentPlayers tp
ORDER BY tp.TournamentId, tp.Id;

PRINT '';
PRINT '============================================';
PRINT '   TOURNAMENT PLAYERS SEED COMPLETED!       ';
PRINT '============================================';
PRINT '';
PRINT '👥 Registrations Created:';
PRINT '   Tournament 1: 5 players (player1-5)';
PRINT '   Tournament 2: 3 players (player1-3)';
PRINT '   Tournament 3: 4 players (player1,2,4,5)';
PRINT '';
PRINT '✅ Ready for next step: TournamentStages';
PRINT '   Run: Seed_05_TournamentStages.sql';
PRINT '============================================';

