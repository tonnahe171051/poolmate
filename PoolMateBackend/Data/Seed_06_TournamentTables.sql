-- ============================================
-- SEED DATA: TOURNAMENT TABLES (Step 6)
-- ============================================
-- Tạo TournamentTables cho tournaments
-- Dependencies: Tournaments

USE PoolMateDB;
GO

SET NOCOUNT ON;

PRINT '============================================';
PRINT '   SEED TOURNAMENT TABLES - STEP 6          ';
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
-- XÓA TABLES CŨ
-- =============================================
PRINT 'Cleaning old tables...';

DELETE FROM TournamentTables WHERE TournamentId IN (1, 2, 3);

PRINT '  ✓ Cleaned old tables';
PRINT '';

-- Reset Identity
DBCC CHECKIDENT ('TournamentTables', RESEED, 0);

-- =============================================
-- TẠO TOURNAMENT TABLES
-- =============================================
PRINT 'Creating tournament tables...';

SET IDENTITY_INSERT TournamentTables ON;

INSERT INTO TournamentTables (
    Id, TournamentId, Label, Manufacturer, SizeFoot, Status
)
VALUES
-- Tournament 1: 4 tables
(1, 1, 'Table 1', 'Diamond', 9.0, 0), -- Status=Open
(2, 1, 'Table 2', 'Diamond', 9.0, 0),
(3, 1, 'Table 3', 'Brunswick', 8.0, 0),
(4, 1, 'Table 4', 'Brunswick', 8.0, 0),

-- Tournament 2: 2 tables
(5, 2, 'Table 1', 'Diamond', 9.0, 0),
(6, 2, 'Table 2', 'Diamond', 9.0, 0),

-- Tournament 3: 2 tables
(7, 3, 'Table 1', 'Brunswick', 9.0, 0),
(8, 3, 'Table 2', 'Brunswick', 9.0, 0);

SET IDENTITY_INSERT TournamentTables OFF;

PRINT '  ✓ Created 8 tables';
PRINT '';

-- =============================================
-- VERIFICATION
-- =============================================
PRINT '============================================';
PRINT '   VERIFICATION                             ';
PRINT '============================================';
PRINT '';

SELECT 
    tt.Id AS TableId,
    t.Name AS Tournament,
    tt.Label,
    tt.Manufacturer,
    tt.SizeFoot,
    CASE tt.Status
        WHEN 0 THEN 'Open'
        WHEN 1 THEN 'InUse'
        WHEN 2 THEN 'Reserved'
        WHEN 3 THEN 'Maintenance'
    END AS Status
FROM TournamentTables tt
JOIN Tournaments t ON tt.TournamentId = t.Id
ORDER BY tt.TournamentId, tt.Id;

PRINT '';
PRINT '============================================';
PRINT '   TOURNAMENT TABLES SEED COMPLETED!        ';
PRINT '============================================';
PRINT '';
PRINT '🎱 Tables Created:';
PRINT '   Tournament 1: 4 tables';
PRINT '   Tournament 2: 2 tables';
PRINT '   Tournament 3: 2 tables';
PRINT '';
PRINT '✅ Ready for final step: Matches';
PRINT '   Run: Seed_07_Matches.sql';
PRINT '============================================';

