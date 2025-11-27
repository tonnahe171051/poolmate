-- ============================================
-- SEED DATA: USERS ONLY
-- ============================================
-- Script này CHỈ tạo Users với role Player
-- Chạy script này TRƯỚC tiên, sau đó mới chạy script seed data khác

USE PoolMateDB;
GO

SET NOCOUNT ON;

PRINT '============================================';
PRINT '   SEED USERS - STEP 1                     ';
PRINT '============================================';
PRINT '';

-- =============================================
-- XÓA USERS CŨ (nếu có)
-- =============================================
PRINT 'Cleaning old test users...';

-- Xóa UserRoles
DELETE FROM AspNetUserRoles WHERE UserId IN (
    SELECT Id FROM AspNetUsers WHERE Email LIKE 'player%@test.com'
);

-- Xóa Users
DELETE FROM AspNetUsers WHERE Email LIKE 'player%@test.com';

PRINT '  ✓ Cleaned old test users';
PRINT '';

-- =============================================
-- TẠO ROLE "PLAYER" (nếu chưa có)
-- =============================================
PRINT 'Creating Role "Player"...';

DECLARE @PlayerRoleId NVARCHAR(450);

-- Kiểm tra role đã tồn tại chưa
IF NOT EXISTS (SELECT 1 FROM AspNetRoles WHERE Name = 'Player')
BEGIN
    SET @PlayerRoleId = NEWID();
    INSERT INTO AspNetRoles (Id, Name, NormalizedName, ConcurrencyStamp)
    VALUES (@PlayerRoleId, 'Player', 'PLAYER', NEWID());
    PRINT '  ✓ Role "Player" created';
END
ELSE
BEGIN
    SELECT @PlayerRoleId = Id FROM AspNetRoles WHERE Name = 'Player';
    PRINT '  ✓ Role "Player" already exists';
END

PRINT '';

-- =============================================
-- TẠO 5 TEST USERS
-- =============================================
PRINT 'Creating 5 test users...';

-- Password: "Player123!" (hash mẫu - trong production nên dùng API để tạo user)
-- Hash này là mẫu, có thể không work. Nên tạo users qua API /register
DECLARE @PasswordHash NVARCHAR(MAX) = 'AQAAAAIAAYagAAAAEJhwGYKqo7qp7VhqKbTvQGKjYp0p7u7VJXqp7VJqp7VJqp7VJqp7VJqp7VJqp7VJqg==';

-- Tạo GUIDs cho 5 users
DECLARE @User1Id NVARCHAR(450) = NEWID();
DECLARE @User2Id NVARCHAR(450) = NEWID();
DECLARE @User3Id NVARCHAR(450) = NEWID();
DECLARE @User4Id NVARCHAR(450) = NEWID();
DECLARE @User5Id NVARCHAR(450) = NEWID();

-- Insert Users
INSERT INTO AspNetUsers (
    Id, 
    UserName, 
    NormalizedUserName, 
    Email, 
    NormalizedEmail,
    EmailConfirmed, 
    PasswordHash, 
    SecurityStamp, 
    ConcurrencyStamp,
    PhoneNumberConfirmed, 
    TwoFactorEnabled, 
    LockoutEnabled, 
    AccessFailedCount
)
VALUES
-- User 1
(@User1Id, 
    'player1@test.com', 'PLAYER1@TEST.COM', 
    'player1@test.com', 'PLAYER1@TEST.COM',
    1, @PasswordHash, NEWID(), NEWID(), 
    0, 0, 1, 0),

-- User 2
(@User2Id, 
    'player2@test.com', 'PLAYER2@TEST.COM', 
    'player2@test.com', 'PLAYER2@TEST.COM',
    1, @PasswordHash, NEWID(), NEWID(), 
    0, 0, 1, 0),

-- User 3
(@User3Id, 
    'player3@test.com', 'PLAYER3@TEST.COM', 
    'player3@test.com', 'PLAYER3@TEST.COM',
    1, @PasswordHash, NEWID(), NEWID(), 
    0, 0, 1, 0),

-- User 4
(@User4Id, 
    'player4@test.com', 'PLAYER4@TEST.COM', 
    'player4@test.com', 'PLAYER4@TEST.COM',
    1, @PasswordHash, NEWID(), NEWID(), 
    0, 0, 1, 0),

-- User 5
(@User5Id, 
    'player5@test.com', 'PLAYER5@TEST.COM', 
    'player5@test.com', 'PLAYER5@TEST.COM',
    1, @PasswordHash, NEWID(), NEWID(), 
    0, 0, 1, 0);

PRINT '  ✓ Created 5 users';
PRINT '';

-- =============================================
-- GÁN ROLE "PLAYER" CHO USERS
-- =============================================
PRINT 'Assigning role "Player" to users...';

INSERT INTO AspNetUserRoles (UserId, RoleId)
VALUES
    (@User1Id, @PlayerRoleId),
    (@User2Id, @PlayerRoleId),
    (@User3Id, @PlayerRoleId),
    (@User4Id, @PlayerRoleId),
    (@User5Id, @PlayerRoleId);

PRINT '  ✓ Role assigned to all users';
PRINT '';

-- =============================================
-- TẠO PLAYERS CHO TỪNG USER (1 User = 1 Player)
-- =============================================
PRINT 'Creating Players for each user...';

-- Xóa Players cũ của test users (nếu có)
DELETE FROM Players WHERE Email LIKE 'player%@test.com';

-- Reset Identity
DBCC CHECKIDENT ('Players', RESEED, 0);

-- Tạo 5 Players tương ứng với 5 Users
SET IDENTITY_INSERT Players ON;

INSERT INTO Players (
    Id, FullName, Nickname, Email, Phone, Country, City, SkillLevel, UserId, CreatedAt
)
VALUES
-- Player 1 cho User 1
(1, N'Nguyễn Văn An', N'An Billiards', 'player1@test.com', '0901234567', 'VN', N'Hà Nội', 650, @User1Id, GETUTCDATE()),

-- Player 2 cho User 2
(2, N'Trần Thị Bình', N'Bình Pro', 'player2@test.com', '0902345678', 'VN', N'Hồ Chí Minh', 720, @User2Id, GETUTCDATE()),

-- Player 3 cho User 3
(3, N'Lê Hoàng Cường', N'Cường Pool', 'player3@test.com', '0903456789', 'VN', N'Đà Nẵng', 680, @User3Id, GETUTCDATE()),

-- Player 4 cho User 4
(4, N'Phạm Minh Đức', N'Đức Master', 'player4@test.com', '0904567890', 'VN', N'Hà Nội', 700, @User4Id, GETUTCDATE()),

-- Player 5 cho User 5
(5, N'Võ Thị Hoa', N'Hoa 9-Ball', 'player5@test.com', '0905678901', 'VN', N'Hồ Chí Minh', 640, @User5Id, GETUTCDATE());

SET IDENTITY_INSERT Players OFF;

PRINT '  ✓ Created 5 Players linked to Users';
PRINT '';

-- =============================================
-- VERIFICATION
-- =============================================
PRINT '============================================';
PRINT '   VERIFICATION                             ';
PRINT '============================================';
PRINT '';

DECLARE @UserCount INT, @RoleCount INT, @PlayerCount INT;
SELECT @UserCount = COUNT(*) FROM AspNetUsers WHERE Email LIKE 'player%@test.com';
SELECT @RoleCount = COUNT(*) FROM AspNetUserRoles WHERE UserId IN (
    SELECT Id FROM AspNetUsers WHERE Email LIKE 'player%@test.com'
);
SELECT @PlayerCount = COUNT(*) FROM Players WHERE Email LIKE 'player%@test.com';

PRINT 'Users Created: ' + CAST(@UserCount AS VARCHAR);
PRINT 'Role Assignments: ' + CAST(@RoleCount AS VARCHAR);
PRINT 'Players Created: ' + CAST(@PlayerCount AS VARCHAR);
PRINT '';

-- Hiển thị danh sách users với players
PRINT 'User & Player List:';
SELECT 
    u.Email,
    r.Name AS Role,
    p.Id AS PlayerId,
    p.FullName AS PlayerName,
    p.SkillLevel,
    CASE WHEN p.UserId IS NOT NULL THEN 'Linked' ELSE 'Not Linked' END AS LinkStatus
FROM AspNetUsers u
LEFT JOIN AspNetUserRoles ur ON u.Id = ur.UserId
LEFT JOIN AspNetRoles r ON ur.RoleId = r.Id
LEFT JOIN Players p ON u.Id = p.UserId
WHERE u.Email LIKE 'player%@test.com'
ORDER BY u.Email;

PRINT '';
PRINT '============================================';
PRINT '   USERS & PLAYERS SEED COMPLETED!          ';
PRINT '============================================';
PRINT '';
PRINT '📋 CREDENTIALS (User → Player):';
PRINT '';
PRINT '   Email: player1@test.com | Password: Player123!';
PRINT '   → Player: Nguyễn Văn An (ID: 1, Skill: 650)';
PRINT '';
PRINT '   Email: player2@test.com | Password: Player123!';
PRINT '   → Player: Trần Thị Bình (ID: 2, Skill: 720)';
PRINT '';
PRINT '   Email: player3@test.com | Password: Player123!';
PRINT '   → Player: Lê Hoàng Cường (ID: 3, Skill: 680)';
PRINT '';
PRINT '   Email: player4@test.com | Password: Player123!';
PRINT '   → Player: Phạm Minh Đức (ID: 4, Skill: 700)';
PRINT '';
PRINT '   Email: player5@test.com | Password: Player123!';
PRINT '   → Player: Võ Thị Hoa (ID: 5, Skill: 640)';
PRINT '';
PRINT '⚠️  NOTE: Password hash là mẫu, có thể không work.';
PRINT '    Nếu login không được, hãy register qua API:';
PRINT '    POST /api/auth/register';
PRINT '';
PRINT '✅ Mỗi User đã được tự động tạo 1 Player!';
PRINT '✅ Bây giờ bạn có thể chạy script seed data khác!';
PRINT '============================================';

