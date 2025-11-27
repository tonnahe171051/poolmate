-- ============================================
-- SEED DATA: TOURNAMENT STAGES (Step 5)
-- ============================================
-- Tạo TournamentStages cho tournaments
-- Dependencies: Tournaments

USE PoolMateDB;
GO

SET NOCOUNT ON;

PRINT '============================================';
PRINT '   SEED TOURNAMENT STAGES - STEP 5          ';
PRINT '============================================';
PRINT '';

-- =============================================
-- KIỂM TRA DEPENDENCIES
-- =============================================
PRINT 'Checking dependencies...';

DECLARE @TournamentCount INT;
SELECT @TournamentCount = COUNT(*) FROM Tournaments WHERE Id IN (1, 2, 3);

IF @TournamentCount < 3
BEGIN
    PRINT '  ❌ ERROR: Tournaments not found!';
    PRINT '  💡 Please run Seed_03_Tournaments.sql first';
    RETURN;
END

PRINT '  ✓ Tournaments found: ' + CAST(@TournamentCount AS VARCHAR);
PRINT '';

-- =============================================
-- XÓA STAGES CŨ
-- =============================================
PRINT 'Cleaning old stages...';

DELETE FROM TournamentStages WHERE TournamentId IN (1, 2, 3);

PRINT '  ✓ Cleaned old stages';
PRINT '';

-- Reset Identity
DBCC CHECKIDENT ('TournamentStages', RESEED, 0);

-- =============================================
-- TẠO TOURNAMENT STAGES
-- =============================================
PRINT 'Creating tournament stages...';

SET IDENTITY_INSERT TournamentStages ON;

INSERT INTO TournamentStages (
    Id, TournamentId, StageNo, Type, Status, Ordering,
    CreatedAt, UpdatedAt
)
VALUES
-- Tournament 1: InProgress - DoubleElimination
(1, 1, 1, 1, 1, 0, -- StageNo=1, Type=DoubleElimination, Status=InProgress
    DATEADD(DAY, -10, GETUTCDATE()), 
    GETUTCDATE()),

-- Tournament 2: Upcoming - SingleElimination  
(2, 2, 1, 0, 0, 0, -- StageNo=1, Type=SingleElimination, Status=NotStarted
    DATEADD(DAY, -5, GETUTCDATE()),
    GETUTCDATE()),

-- Tournament 3: Completed - SingleElimination
(3, 3, 1, 0, 2, 0, -- StageNo=1, Type=SingleElimination, Status=Completed
    DATEADD(DAY, -30, GETUTCDATE()),
    DATEADD(DAY, -20, GETUTCDATE()));

SET IDENTITY_INSERT TournamentStages OFF;

PRINT '  ✓ Created 3 stages';
PRINT '';

-- =============================================
-- VERIFICATION
-- =============================================
PRINT '============================================';
PRINT '   VERIFICATION                             ';
PRINT '============================================';
PRINT '';

SELECT 
    ts.Id AS StageId,
    t.Name AS Tournament,
    ts.StageNo,
    CASE ts.Type
        WHEN 0 THEN 'SingleElimination'
        WHEN 1 THEN 'DoubleElimination'
        WHEN 2 THEN 'RoundRobin'
    END AS Type,
    CASE ts.Status
        WHEN 0 THEN 'NotStarted'
        WHEN 1 THEN 'InProgress'
        WHEN 2 THEN 'Completed'
    END AS Status
FROM TournamentStages ts
JOIN Tournaments t ON ts.TournamentId = t.Id
ORDER BY ts.Id;

PRINT '';
PRINT '============================================';
PRINT '   TOURNAMENT STAGES SEED COMPLETED!        ';
PRINT '============================================';
PRINT '';
PRINT '📊 Stages Created:';
PRINT '   Stage 1: Tournament 1 (DoubleElimination, InProgress)';
PRINT '   Stage 2: Tournament 2 (SingleElimination, NotStarted)';
PRINT '   Stage 3: Tournament 3 (SingleElimination, Completed)';
PRINT '';
PRINT '✅ Ready for next step: TournamentTables';
PRINT '   Run: Seed_06_TournamentTables.sql';
PRINT '============================================';

