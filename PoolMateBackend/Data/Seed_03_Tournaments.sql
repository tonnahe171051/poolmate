-- ============================================
-- SEED DATA: TOURNAMENTS (Step 3)
-- ============================================
-- Tạo Tournaments (owner: Users đã tạo)
-- VenueId = NULL (không dùng venues)

USE PoolMateDB;
GO

SET NOCOUNT ON;

PRINT '============================================';
PRINT '   SEED TOURNAMENTS - STEP 3                ';
PRINT '============================================';
PRINT '';

-- =============================================
-- KIỂM TRA DEPENDENCIES
-- =============================================
PRINT 'Checking dependencies...';

DECLARE @UserCount INT, @PlayerCount INT;
SELECT @UserCount = COUNT(*) FROM AspNetUsers WHERE Email LIKE 'player%@test.com';
SELECT @PlayerCount = COUNT(*) FROM Players WHERE Email LIKE 'player%@test.com';

IF @UserCount < 5 OR @PlayerCount < 5
BEGIN
    PRINT '  ❌ ERROR: Users or Players not found!';
    PRINT '  💡 Please run Seed_01_Users.sql first';
    RETURN;
END

PRINT '  ✓ Users found: ' + CAST(@UserCount AS VARCHAR);
PRINT '  ✓ Players found: ' + CAST(@PlayerCount AS VARCHAR);
PRINT '';

-- =============================================
-- XÓA TOURNAMENTS CŨ
-- =============================================
PRINT 'Cleaning old tournaments...';

-- Xóa theo thứ tự dependencies
DELETE FROM Matches WHERE TournamentId IN (SELECT Id FROM Tournaments WHERE Name LIKE '%Test%' OR Name LIKE N'%Giải%');
DELETE FROM TournamentTables WHERE TournamentId IN (SELECT Id FROM Tournaments WHERE Name LIKE '%Test%' OR Name LIKE N'%Giải%');
DELETE FROM TournamentStages WHERE TournamentId IN (SELECT Id FROM Tournaments WHERE Name LIKE '%Test%' OR Name LIKE N'%Giải%');
DELETE FROM TournamentPlayers WHERE TournamentId IN (SELECT Id FROM Tournaments WHERE Name LIKE '%Test%' OR Name LIKE N'%Giải%');
DELETE FROM Tournaments WHERE Name LIKE '%Test%' OR Name LIKE N'%Giải%';

PRINT '  ✓ Cleaned old tournaments and related data';
PRINT '';

-- Reset Identity
DBCC CHECKIDENT ('Tournaments', RESEED, 0);

-- =============================================
-- TẠO 3 TOURNAMENTS
-- =============================================
PRINT 'Creating 3 tournaments...';

-- Lấy UserId của player1 làm owner
DECLARE @OwnerUserId NVARCHAR(450);
SELECT @OwnerUserId = Id FROM AspNetUsers WHERE Email = 'player1@test.com';

SET IDENTITY_INSERT Tournaments ON;

INSERT INTO Tournaments (
    Id, Name, Description, 
    GameType, BracketType, PlayerType,
    StartUtc, EndUtc, 
    Status, IsPublic, OnlineRegistrationEnabled,
    OwnerUserId, CreatedAt, UpdatedAt,
    [Rule], BracketOrdering, IsStarted, IsMultiStage,
    Stage1Ordering, Stage2Ordering,
    BracketSizeEstimate, EntryFee, AdminFee, AddedMoney
)
VALUES
-- Tournament 1: InProgress (đang diễn ra)
(1, N'Giải Billiards Mở Rộng Tháng 11', 
    N'Giải đấu 8-ball dành cho tất cả skill levels',
    0, -- EightBall
    1, -- DoubleElimination  
    0, -- Singles
    DATEADD(DAY, -3, GETUTCDATE()), 
    DATEADD(DAY, 4, GETUTCDATE()),
    2, -- InProgress
    1, 1, -- IsPublic, OnlineRegistrationEnabled
    @OwnerUserId, 
    DATEADD(DAY, -10, GETUTCDATE()), 
    GETUTCDATE(),
    0, 0, 1, 0, 0, 0, -- Rule, BracketOrdering, IsStarted, IsMultiStage, Stage1/2Ordering
    16, 200000.00, 20000.00, 500000.00), -- BracketSize, EntryFee, AdminFee, AddedMoney

-- Tournament 2: Upcoming (sắp diễn ra)
(2, N'Giải Vô Địch Khu Vực Miền Bắc',
    N'Giải 9-ball chuyên nghiệp',
    1, -- NineBall
    0, -- SingleElimination
    0, -- Singles
    DATEADD(DAY, 7, GETUTCDATE()),
    DATEADD(DAY, 10, GETUTCDATE()),
    1, -- Upcoming
    1, 1,
    @OwnerUserId,
    DATEADD(DAY, -5, GETUTCDATE()),
    GETUTCDATE(),
    0, 0, 0, 0, 0, 0,
    8, 500000.00, 50000.00, 2000000.00),

-- Tournament 3: Completed (đã kết thúc)
(3, N'Giải Giao Hữu Tháng 10',
    N'Giải 8-ball giao hữu đã kết thúc',
    0, -- EightBall
    0, -- SingleElimination
    0, -- Singles
    DATEADD(DAY, -25, GETUTCDATE()),
    DATEADD(DAY, -20, GETUTCDATE()),
    4, -- Completed
    1, 1,
    @OwnerUserId,
    DATEADD(DAY, -30, GETUTCDATE()),
    DATEADD(DAY, -20, GETUTCDATE()),
    0, 0, 1, 0, 0, 0,
    8, 100000.00, 10000.00, 200000.00);

SET IDENTITY_INSERT Tournaments OFF;

PRINT '  ✓ Created 3 tournaments';
PRINT '';

-- =============================================
-- VERIFICATION
-- =============================================
PRINT '============================================';
PRINT '   VERIFICATION                             ';
PRINT '============================================';
PRINT '';

DECLARE @TournamentCount INT;
SELECT @TournamentCount = COUNT(*) FROM Tournaments;

PRINT 'Total Tournaments: ' + CAST(@TournamentCount AS VARCHAR);
PRINT '';

-- Hiển thị danh sách tournaments
PRINT 'Tournament List:';
SELECT 
    Id,
    Name,
    CASE Status
        WHEN 0 THEN 'Draft'
        WHEN 1 THEN 'Upcoming'
        WHEN 2 THEN 'InProgress'
        WHEN 3 THEN 'Paused'
        WHEN 4 THEN 'Completed'
        WHEN 5 THEN 'Cancelled'
    END AS Status,
    BracketSizeEstimate AS MaxPlayers,
    EntryFee,
    StartUtc
FROM Tournaments
ORDER BY Id;

PRINT '';
PRINT '============================================';
PRINT '   TOURNAMENTS SEED COMPLETED!              ';
PRINT '============================================';
PRINT '';
PRINT '🏆 Tournaments Created:';
PRINT '   1. Giải Billiards Mở Rộng Tháng 11 (InProgress, 16 players)';
PRINT '   2. Giải Vô Địch Khu Vực Miền Bắc (Upcoming, 8 players)';
PRINT '   3. Giải Giao Hữu Tháng 10 (Completed, 8 players)';
PRINT '';
PRINT '✅ Ready for next step: TournamentPlayers';
PRINT '   Run: Seed_04_TournamentPlayers.sql';
PRINT '============================================';

