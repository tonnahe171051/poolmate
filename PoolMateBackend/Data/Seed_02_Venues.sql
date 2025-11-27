-- ============================================
-- SEED DATA: VENUES (Step 2)
-- ============================================
-- Tạo Venues trước Tournaments
-- Dependencies: Users (CreatedByUserId)

USE PoolMateDB;
GO

SET NOCOUNT ON;

PRINT '============================================';
PRINT '   SEED VENUES - STEP 2                    ';
PRINT '============================================';
PRINT '';

-- =============================================
-- KIỂM TRA DEPENDENCIES
-- =============================================
PRINT 'Checking dependencies...';

DECLARE @UserCount INT;
SELECT @UserCount = COUNT(*) FROM AspNetUsers WHERE Email LIKE 'player%@test.com';

IF @UserCount < 5
BEGIN
    PRINT '  ❌ ERROR: Users not found!';
    PRINT '  💡 Please run Seed_01_Users.sql first';
    RETURN;
END

PRINT '  ✓ Users found: ' + CAST(@UserCount AS VARCHAR);
PRINT '';

-- =============================================
-- XÓA VENUES CŨ
-- =============================================
PRINT 'Cleaning old venues...';

DELETE FROM Venues WHERE Name LIKE '%Test Venue%' OR Name LIKE '%Billiards%' OR Name LIKE '%Poolroom%';

PRINT '  ✓ Cleaned old venues';
PRINT '';

-- Reset Identity
DBCC CHECKIDENT ('Venues', RESEED, 0);

-- =============================================
-- TẠO 5 VENUES
-- =============================================
PRINT 'Creating 5 venues...';

-- Lấy UserId của user đầu tiên làm creator
DECLARE @CreatorUserId NVARCHAR(450);
SELECT TOP 1 @CreatorUserId = Id FROM AspNetUsers WHERE Email LIKE 'player%@test.com' ORDER BY Email;

SET IDENTITY_INSERT Venues ON;

INSERT INTO Venues (
    Id, Name, Address, City, Country, CreatedByUserId, CreatedAt
)
VALUES
-- Venue 1: Hà Nội
(1, N'Billiards Club Hà Nội', 
    N'123 Trần Hưng Đạo, Hoàn Kiếm', 
    N'Hà Nội', 
    'VN', 
    @CreatorUserId, 
    DATEADD(DAY, -60, GETUTCDATE())),

-- Venue 2: HCM
(2, N'Poolroom Sài Gòn', 
    N'456 Nguyễn Huệ, Quận 1', 
    N'Hồ Chí Minh', 
    'VN', 
    @CreatorUserId, 
    DATEADD(DAY, -50, GETUTCDATE())),

-- Venue 3: Đà Nẵng
(3, N'Arena Billiards Đà Nẵng', 
    N'789 Lê Duẩn, Hải Châu', 
    N'Đà Nẵng', 
    'VN', 
    @CreatorUserId, 
    DATEADD(DAY, -45, GETUTCDATE())),

-- Venue 4: Hải Phòng
(4, N'Champion Pool Hải Phòng', 
    N'321 Lê Lợi, Ngô Quyền', 
    N'Hải Phòng', 
    'VN', 
    @CreatorUserId, 
    DATEADD(DAY, -40, GETUTCDATE())),

-- Venue 5: Cần Thơ
(5, N'Mekong Billiards Cần Thơ', 
    N'654 Mậu Thân, Ninh Kiều', 
    N'Cần Thơ', 
    'VN', 
    @CreatorUserId, 
    DATEADD(DAY, -35, GETUTCDATE()));

SET IDENTITY_INSERT Venues OFF;

PRINT '  ✓ Created 5 venues';
PRINT '';

-- =============================================
-- VERIFICATION
-- =============================================
PRINT '============================================';
PRINT '   VERIFICATION                             ';
PRINT '============================================';
PRINT '';

DECLARE @VenueCount INT;
SELECT @VenueCount = COUNT(*) FROM Venues;

PRINT 'Total Venues: ' + CAST(@VenueCount AS VARCHAR);
PRINT '';

-- Hiển thị danh sách venues
PRINT 'Venue List:';
SELECT 
    Id,
    Name,
    City,
    Country,
    CASE WHEN CreatedByUserId IS NOT NULL THEN 'Yes' ELSE 'No' END AS HasCreator
FROM Venues
ORDER BY Id;

PRINT '';
PRINT '============================================';
PRINT '   VENUES SEED COMPLETED!                   ';
PRINT '============================================';
PRINT '';
PRINT '📍 Venues Created:';
PRINT '   1. Billiards Club Hà Nội';
PRINT '   2. Poolroom Sài Gòn (HCM)';
PRINT '   3. Arena Billiards Đà Nẵng';
PRINT '   4. Champion Pool Hải Phòng';
PRINT '   5. Mekong Billiards Cần Thơ';
PRINT '';
PRINT '✅ Ready for next step: Tournaments';
PRINT '   Run: Seed_03_Tournaments.sql';
PRINT '============================================';

