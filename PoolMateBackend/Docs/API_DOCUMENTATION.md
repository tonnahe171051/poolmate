# 📚 Tài Liệu API - Admin & Organizer Modules

## Tổng Quan Hệ Thống

Tài liệu này mô tả chi tiết các API của 3 module chính trong hệ thống PoolMate:
1. **AdminPlayersController** - Quản lý thông tin VĐV
2. **AdminUsersController** - Quản lý tài khoản người dùng
3. **OrganizerDashboardController** - Dashboard cho người tổ chức giải đấu

---

# 🎯 Module 1: Admin Players Management

**Base URL:** `/api/admin/players`  
**Role Required:** `Admin`  
**Mục đích:** Quản lý toàn bộ thông tin VĐV (Players) trong hệ thống, bao gồm linking với User accounts, thống kê, và data quality.

---

## 📋 1.1. Danh Sách & Thống Kê

### `GET /api/admin/players`
**Chức năng:** Lấy danh sách Players với filter, search, sort và pagination

**Use Cases:**
- Xem tất cả VĐV trong hệ thống
- Tìm kiếm VĐV theo tên, email, quốc gia
- Filter theo skill level, trạng thái (linked/unlinked)
- Sắp xếp theo tên, ranking, số giải tham gia

**Query Parameters:**
```typescript
{
  search?: string;           // Tìm theo tên, email
  skillLevel?: number;       // Filter theo skill level
  country?: string;          // Filter theo quốc gia
  isLinked?: boolean;        // Có link với User chưa
  pageIndex?: number;        // Trang hiện tại
  pageSize?: number;         // Số items/trang
  sortBy?: string;          // Field để sort
  sortOrder?: 'asc' | 'desc';
}
```

**Response:**
```json
{
  "items": [
    {
      "id": 123,
      "displayName": "Nguyễn Văn A",
      "email": "nguyenvana@example.com",
      "phone": "+84901234567",
      "country": "VN",
      "skillLevel": 5,
      "linkedUserId": "user-id-123",
      "tournamentCount": 15,
      "lastPlayedAt": "2025-11-15T..."
    }
  ],
  "totalCount": 500,
  "pageIndex": 1,
  "pageSize": 20,
  "hasNextPage": true
}
```

---

### `GET /api/admin/players/statistics`
**Chức năng:** Thống kê tổng quan về Players trong hệ thống

**Use Cases:**
- Dashboard overview cho Admin
- Báo cáo tổng quan về players
- Phân tích xu hướng tăng trưởng

**Response:**
```json
{
  "overview": {
    "totalPlayers": 5000,
    "linkedPlayers": 3500,
    "unlinkedPlayers": 1500,
    "activePlayersThisMonth": 1200
  },
  "activityStats": {
    "activeThisWeek": 500,
    "activeThisMonth": 1200,
    "inactivePlayers": 2000,
    "neverPlayedCount": 300
  },
  "distributionByCountry": [
    { "country": "VN", "count": 3000 },
    { "country": "US", "count": 1000 }
  ],
  "distributionBySkillLevel": [
    { "skillLevel": 5, "count": 800 },
    { "skillLevel": 4, "count": 1200 }
  ],
  "growthTrend": {
    "thisMonth": 150,
    "lastMonth": 120,
    "growthRate": 25.0
  }
}
```

**Công dụng:**
- 📊 Visualize số lượng players theo quốc gia
- 📈 Tracking tốc độ tăng trưởng players
- 🎯 Phân tích phân bố skill level
- ⚠️ Phát hiện players không active

---

### `GET /api/admin/players/unclaimed`
**Chức năng:** Lấy danh sách Players chưa được claim (chưa link với User)

**Use Cases:**
- Tìm players chưa có tài khoản trong hệ thống
- Gợi ý matching với users có email/phone trùng
- Xử lý bulk linking

**Response:**
```json
{
  "items": [
    {
      "playerId": 456,
      "displayName": "Trần Văn B",
      "email": "tranvanb@example.com",
      "phone": "+84909876543",
      "tournamentCount": 3,
      "potentialUsers": [
        {
          "userId": "user-789",
          "email": "tranvanb@example.com",
          "matchType": "EmailMatch",
          "confidence": 95
        }
      ]
    }
  ],
  "totalCount": 1500
}
```

**Công dụng:**
- 🔗 Tự động suggest user phù hợp để link
- 📧 Match based on email hoặc phone
- 🎯 Giảm số lượng unclaimed players

---

## 👤 1.2. Chi Tiết & Linking

### `GET /api/admin/players/{playerId}`
**Chức năng:** Xem thông tin chi tiết của 1 Player

**Use Cases:**
- Xem profile đầy đủ của VĐV
- Kiểm tra lịch sử thi đấu
- Xem user đã link (nếu có)

**Response:**
```json
{
  "id": 123,
  "displayName": "Nguyễn Văn A",
  "nickname": "Pro Player",
  "email": "nguyenvana@example.com",
  "phone": "+84901234567",
  "country": "VN",
  "city": "Hanoi",
  "skillLevel": 5,
  "linkedUser": {
    "userId": "user-id-123",
    "username": "nguyenvana",
    "email": "nguyenvana@example.com",
    "linkedAt": "2025-10-15T..."
  },
  "tournamentStats": {
    "totalTournaments": 15,
    "wins": 5,
    "losses": 10,
    "winRate": 33.33,
    "lastPlayedAt": "2025-11-15T..."
  },
  "recentTournaments": [
    {
      "tournamentId": 101,
      "tournamentName": "Vietnam Open 2025",
      "date": "2025-11-15T...",
      "placement": 3
    }
  ]
}
```

**Công dụng:**
- 📋 Xem profile đầy đủ
- 🏆 Tracking thành tích thi đấu
- 👤 Kiểm tra linking status

---

### `POST /api/admin/players/{playerId}/link-user`
**Chức năng:** Link Player với User account

**Use Cases:**
- User claim player profile
- Admin manually link player với user
- Merge duplicate profiles

**Request Body:**
```json
{
  "userId": "user-id-123"
}
```

**Response:**
```json
{
  "message": "Player linked to user successfully."
}
```

**Business Logic:**
- ✅ Kiểm tra Player chưa được link với user khác
- ✅ Kiểm tra User tồn tại
- ✅ Update Player.UserId
- ✅ Log linking action

**Errors:**
- `400 Bad Request`: Player đã được link với user khác
- `404 Not Found`: Player hoặc User không tồn tại

---

### `POST /api/admin/players/{playerId}/unlink-user`
**Chức năng:** Unlink Player khỏi User account

**Use Cases:**
- Sửa lỗi linking sai
- User request unlink profile
- Merge profiles

**Response:**
```json
{
  "message": "Player unlinked from user successfully."
}
```

**Business Logic:**
- ✅ Set Player.UserId = null
- ✅ Giữ lại lịch sử tournament của player
- ✅ Log unlinking action

---

### `GET /api/admin/players/{playerId}/linked-user`
**Chức năng:** Lấy thông tin User đã link với Player

**Use Cases:**
- Verify linking status
- Hiển thị user owner của player profile

**Response:**
```json
{
  "userId": "user-id-123",
  "username": "nguyenvana",
  "email": "nguyenvana@example.com",
  "fullName": "Nguyễn Văn A",
  "linkedAt": "2025-10-15T..."
}
```

---

### `GET /api/admin/players/user/{userId}`
**Chức năng:** Lấy tất cả Players của 1 User

**Use Cases:**
- User có thể sở hữu nhiều player profiles
- Hiển thị all profiles của user
- Manage multiple profiles

**Response:**
```json
[
  {
    "id": 123,
    "displayName": "Nguyễn Văn A",
    "skillLevel": 5,
    "tournamentCount": 15
  },
  {
    "id": 456,
    "displayName": "Player 456",
    "skillLevel": 4,
    "tournamentCount": 8
  }
]
```

**Công dụng:**
- 🔗 User quản lý nhiều profiles (nếu thi đấu dưới nhiều tên)
- 🎯 Merge duplicate profiles
- 📊 Aggregate stats across profiles

---

## 🔄 1.3. Bulk Operations

### `POST /api/admin/players/bulk-link`
**Chức năng:** Link nhiều Players với Users cùng lúc

**Use Cases:**
- Import data từ hệ thống cũ
- Batch processing sau khi có gợi ý match
- Migration từ legacy system

**Request Body:**
```json
{
  "links": [
    {
      "playerId": 123,
      "userId": "user-id-123"
    },
    {
      "playerId": 456,
      "userId": "user-id-456"
    }
  ]
}
```

**Response:**
```json
{
  "totalRequested": 2,
  "successCount": 2,
  "failedCount": 0,
  "results": [
    {
      "playerId": 123,
      "userId": "user-id-123",
      "success": true,
      "message": "Linked successfully"
    },
    {
      "playerId": 456,
      "userId": "user-id-456",
      "success": true,
      "message": "Linked successfully"
    }
  ]
}
```

**Công dụng:**
- ⚡ Xử lý hàng loạt linking
- 📊 Track success/failure rate
- 🔧 Rollback nếu cần

---

### `POST /api/admin/players/bulk-unlink`
**Chức năng:** Unlink nhiều Players cùng lúc

**Use Cases:**
- Undo bulk linking sai
- Clean up test data
- Reset profiles

**Request Body:**
```json
{
  "playerIds": [123, 456, 789]
}
```

**Response:**
```json
{
  "totalRequested": 3,
  "successCount": 3,
  "failedCount": 0,
  "results": [
    {
      "playerId": 123,
      "success": true,
      "message": "Unlinked successfully"
    }
  ]
}
```

---

## 📊 1.4. Data Quality & Validation

### `GET /api/admin/players/data-quality`
**Chức năng:** Báo cáo chất lượng dữ liệu Players

**Use Cases:**
- Phát hiện dữ liệu thiếu hoặc không hợp lệ
- Dashboard data quality
- Prioritize data cleanup tasks

**Response:**
```json
{
  "overview": {
    "totalPlayers": 5000,
    "healthyProfiles": 4200,
    "profilesWithIssues": 800,
    "dataQualityScore": 84.0
  },
  "issueBreakdown": {
    "missingEmail": 300,
    "missingPhone": 200,
    "missingSkillLevel": 150,
    "invalidEmail": 50,
    "invalidPhone": 30,
    "inactive1Year": 500,
    "neverPlayed": 300
  },
  "topIssues": [
    {
      "issueType": "inactive-1y",
      "count": 500,
      "percentage": 10.0
    },
    {
      "issueType": "missing-email",
      "count": 300,
      "percentage": 6.0
    }
  ]
}
```

**Công dụng:**
- 🔍 Phát hiện data issues
- 📊 Track data quality metrics
- 🎯 Prioritize cleanup efforts

---

### `GET /api/admin/players/issues/{issueType}`
**Chức năng:** Lấy danh sách players theo loại issue cụ thể

**Issue Types:**
- `missing-email`: Players không có email
- `missing-phone`: Players không có phone
- `missing-skill`: Players thiếu skill level
- `invalid-email`: Email format sai
- `invalid-phone`: Phone format sai
- `inactive-1y`: Không thi đấu trong 1 năm
- `never-played`: Chưa thi đấu giải nào

**Use Cases:**
- Data cleanup campaigns
- Contact players để update info
- Batch validation

**Response:**
```json
{
  "issueType": "missing-email",
  "count": 300,
  "players": [
    {
      "id": 123,
      "displayName": "Player Name",
      "phone": "+84901234567",
      "lastPlayedAt": "2025-10-15T..."
    }
  ]
}
```

---

### `POST /api/admin/players/validate`
**Chức năng:** Validate dữ liệu của 1 player

**Use Cases:**
- Kiểm tra data trước khi save
- Real-time validation trong form
- Batch validation

**Request Body:**
```json
{
  "email": "test@example.com",
  "phone": "+84901234567",
  "skillLevel": 5
}
```

**Response:**
```json
{
  "isValid": true,
  "errors": [],
  "warnings": [
    "Email domain không phổ biến"
  ]
}
```

---

## 📥 1.5. Export

### `GET /api/admin/players/export`
**Chức năng:** Export danh sách players ra CSV

**Use Cases:**
- Backup data
- Báo cáo cho ban tổ chức
- Phân tích trong Excel

**Query Parameters:**
```typescript
{
  ...PlayerFilterDto,           // Tất cả filters như API list
  includeTournamentHistory: boolean;
  format: 'csv';                // Hiện tại chỉ hỗ trợ CSV
}
```

**Response:**
```
Content-Type: text/csv
Content-Disposition: attachment; filename="players_2025-11-19.csv"

ID,Name,Email,Phone,Country,Skill Level,Tournament Count,Win Rate
123,"Nguyen Van A","email@example.com","+84901234567","VN",5,15,33.33
...
```

**Công dụng:**
- 📊 Excel analysis
- 📧 Email marketing campaigns
- 💾 Backup & restore

---

# 👥 Module 2: Admin Users Management

**Base URL:** `/api/admin/users`  
**Role Required:** `Admin`  
**Mục đích:** Quản lý tài khoản người dùng (Users) trong hệ thống, bao gồm deactivate/reactivate, statistics, activity logs.

---

## 📋 2.1. Danh Sách & Chi Tiết

### `GET /api/admin/users`
**Chức năng:** Lấy danh sách Users với filter, search, sort và pagination

**Use Cases:**
- Quản lý tất cả tài khoản trong hệ thống
- Tìm kiếm user theo username, email
- Filter theo role, status (active/inactive)

**Query Parameters:**
```typescript
{
  search?: string;           // Tìm theo username, email, name
  role?: string;            // Filter theo role (Admin, Player, Organizer)
  isActive?: boolean;       // Active/Inactive
  emailConfirmed?: boolean; // Email verified hay chưa
  pageIndex?: number;
  pageSize?: number;
  sortBy?: string;
  sortOrder?: 'asc' | 'desc';
}
```

**Response:**
```json
{
  "items": [
    {
      "id": "user-id-123",
      "userName": "nguyenvana",
      "email": "nguyenvana@example.com",
      "fullName": "Nguyễn Văn A",
      "phoneNumber": "+84901234567",
      "emailConfirmed": true,
      "isActive": true,
      "roles": ["Player"],
      "createdAt": "2025-01-15T...",
      "lastLoginAt": "2025-11-18T...",
      "tournamentCount": 5,
      "linkedPlayerCount": 2
    }
  ],
  "totalCount": 1000,
  "pageIndex": 1,
  "pageSize": 20
}
```

**Công dụng:**
- 👥 Quản lý user base
- 🔍 Tìm user nhanh chóng
- 📊 Filter theo nhiều tiêu chí

---

### `GET /api/admin/users/{id}`
**Chức năng:** Lấy thông tin chi tiết của 1 User

**Use Cases:**
- View full profile của user
- Kiểm tra linked players
- Xem lịch sử hoạt động

**Response:**
```json
{
  "id": "user-id-123",
  "userName": "nguyenvana",
  "email": "nguyenvana@example.com",
  "fullName": "Nguyễn Văn A",
  "phoneNumber": "+84901234567",
  "profileImageUrl": "https://...",
  "bio": "Professional pool player",
  "country": "VN",
  "city": "Hanoi",
  "emailConfirmed": true,
  "phoneNumberConfirmed": true,
  "twoFactorEnabled": false,
  "isActive": true,
  "roles": ["Player"],
  "createdAt": "2025-01-15T...",
  "lastLoginAt": "2025-11-18T...",
  "linkedPlayers": [
    {
      "playerId": 123,
      "displayName": "Nguyễn Văn A",
      "skillLevel": 5
    }
  ],
  "tournamentStats": {
    "totalCreated": 3,
    "totalJoined": 15,
    "totalWins": 5
  },
  "accountHealth": {
    "loginCount": 150,
    "lastActive": "2025-11-18T...",
    "securityScore": 85
  }
}
```

**Công dụng:**
- 📋 Full profile overview
- 🔗 Xem linked players
- 📊 Account health check

---

## 🔐 2.2. Account Management

### `PUT /api/admin/users/{id}/deactivate`
**Chức năng:** Vô hiệu hóa tài khoản user (lock vĩnh viễn)

**Use Cases:**
- Ban user vi phạm điều khoản
- Suspend account tạm thời hoặc vĩnh viễn
- Prevent access nhưng giữ lại data

**Business Logic:**
- ✅ Set `IsActive = false`
- ✅ Revoke tất cả refresh tokens
- ✅ User không thể login
- ✅ Data được giữ lại (tournaments, posts, etc.)
- ✅ Log deactivation action với lý do

**Response:**
```json
{
  "userId": "user-id-123",
  "isActive": false,
  "deactivatedAt": "2025-11-19T...",
  "message": "User deactivated successfully."
}
```

**⚠️ Important:**
- User không thể login
- Tournaments đã tạo vẫn hoạt động
- Linked players vẫn giữ nguyên
- Có thể reactivate sau

---

### `PUT /api/admin/users/{id}/reactivate`
**Chức năng:** Kích hoạt lại tài khoản đã bị deactivate

**Use Cases:**
- Sau khi user khiếu nại thành công
- Temporary ban hết hạn
- Restore account sau investigation

**Business Logic:**
- ✅ Set `IsActive = true`
- ✅ User có thể login lại
- ✅ Restore full access
- ✅ Log reactivation action

**Response:**
```json
{
  "userId": "user-id-123",
  "isActive": true,
  "reactivatedAt": "2025-11-19T...",
  "message": "User reactivated successfully."
}
```

---

## 📊 2.3. Statistics & Analytics

### `GET /api/admin/users/statistics`
**Chức năng:** Thống kê tổng quan về Users

**Use Cases:**
- Admin dashboard overview
- Monitor user growth
- Security & verification metrics

**Response:**
```json
{
  "overview": {
    "totalUsers": 1000,
    "activeUsers": 850,
    "inactiveUsers": 150,
    "newUsersThisMonth": 50
  },
  "emailPhoneVerification": {
    "emailConfirmedCount": 800,
    "phoneConfirmedCount": 600,
    "bothConfirmed": 500,
    "noneConfirmed": 100
  },
  "securityMetrics": {
    "twoFactorEnabled": 200,
    "recentLogins24h": 150,
    "suspiciousActivityCount": 5
  },
  "roleDistribution": {
    "Admin": 5,
    "Player": 900,
    "Organizer": 95
  },
  "geographicDistribution": [
    { "country": "VN", "count": 700 },
    { "country": "US", "count": 200 }
  ],
  "growthTrend": {
    "thisMonth": 50,
    "lastMonth": 40,
    "growthRate": 25.0,
    "monthlyData": [
      { "month": "2025-09", "count": 30 },
      { "month": "2025-10", "count": 40 },
      { "month": "2025-11", "count": 50 }
    ]
  }
}
```

**Công dụng:**
- 📊 Dashboard KPIs
- 📈 Track growth trends
- 🔒 Monitor security metrics
- 🌍 Geographic distribution

---

### `GET /api/admin/users/{id}/activity-log`
**Chức năng:** Lấy activity log của 1 user cụ thể

**Use Cases:**
- Investigate suspicious activity
- User support (xem lịch sử hoạt động)
- Audit trail

**Response:**
```json
{
  "userId": "user-id-123",
  "userName": "nguyenvana",
  "activitySummary": {
    "tournamentsCreated": 3,
    "tournamentsJoined": 15,
    "postsCreated": 8,
    "venuesCreated": 2,
    "lastLoginAt": "2025-11-18T..."
  },
  "recentActivities": [
    {
      "timestamp": "2025-11-18T14:30:00Z",
      "type": "TournamentCreated",
      "description": "Created tournament 'Vietnam Open 2025'",
      "details": {
        "tournamentId": 101,
        "tournamentName": "Vietnam Open 2025"
      }
    },
    {
      "timestamp": "2025-11-18T10:15:00Z",
      "type": "Login",
      "description": "Logged in from IP 123.456.789.0",
      "details": {
        "ipAddress": "123.456.789.0",
        "userAgent": "Mozilla/5.0..."
      }
    }
  ]
}
```

**Công dụng:**
- 🔍 Investigate user behavior
- 🛡️ Security audit
- 📞 Customer support

---

## 🔄 2.4. Bulk Operations

### `POST /api/admin/users/bulk-deactivate`
**Chức năng:** Deactivate nhiều users cùng lúc

**Use Cases:**
- Ban multiple spam accounts
- Mass suspension after investigation
- Clean up test accounts

**Request Body:**
```json
{
  "userIds": ["user-id-1", "user-id-2", "user-id-3"],
  "reason": "Spam accounts detected",
  "force": false
}
```

**Response:**
```json
{
  "totalRequested": 3,
  "successCount": 3,
  "failedCount": 0,
  "results": [
    {
      "userId": "user-id-1",
      "userName": "user1",
      "success": true,
      "message": "Deactivated successfully"
    }
  ]
}
```

**Business Logic:**
- ✅ Validate tất cả userIds trước khi process
- ✅ Không deactivate Admin users (trừ khi force=true)
- ✅ Log bulk action với reason
- ✅ Rollback nếu có lỗi critical

---

### `POST /api/admin/users/bulk-reactivate`
**Chức năng:** Reactivate nhiều users cùng lúc

**Use Cases:**
- Restore accounts sau appeal
- Undo bulk deactivation sai
- Temporary ban expired

**Request Body:**
```json
{
  "userIds": ["user-id-1", "user-id-2"],
  "reason": "Appeal approved"
}
```

**Response:**
```json
{
  "totalRequested": 2,
  "successCount": 2,
  "failedCount": 0,
  "results": [
    {
      "userId": "user-id-1",
      "success": true,
      "message": "Reactivated successfully"
    }
  ]
}
```

---

## 📥 2.5. Export

### `GET /api/admin/users/export`
**Chức năng:** Export danh sách users ra CSV

**Use Cases:**
- Backup user database
- Compliance reporting
- External analysis

**Query Parameters:**
```typescript
{
  ...AdminUserFilterDto,  // Tất cả filters như API list
  format: 'csv'
}
```

**Response:**
```
Content-Type: text/csv
Content-Disposition: attachment; filename="users_2025-11-19.csv"

ID,Username,Email,Full Name,Role,Status,Created At,Last Login
user-id-123,"nguyenvana","email@example.com","Nguyen Van A","Player","Active","2025-01-15","2025-11-18"
...
```

**Công dụng:**
- 💾 Regular backups
- 📊 External reporting
- 📧 Email campaigns

---

# 📊 Module 3: Organizer Dashboard

**Base URL:** `/api/organizer/dashboard`  
**Role Required:** Any logged-in user (Organizer không phải role riêng)  
**Mục đích:** Cung cấp dashboard insights cho người tổ chức giải đấu về tournaments, participants, và financial metrics.

---

## 🎯 Security Model

**Đặc Biệt:** Module này không yêu cầu role cụ thể. Bất kỳ user nào đã login đều có thể tạo tournament và trở thành "Organizer".

**Data Isolation:**
- ✅ Mỗi user chỉ xem được data của tournaments họ tạo
- ✅ Filter tự động theo `OwnerUserId`
- ✅ Không thể xem data của organizer khác

---

## 📊 3.1. Dashboard Statistics

### `GET /api/organizer/dashboard/stats`
**Chức năng:** Lấy số liệu tổng quan (KPI Stats) cho Organizer

**Use Cases:**
- Dashboard overview cho organizer
- Tracking tournament performance
- Financial metrics (revenue & profit)

**Response (v2.0):**
```json
{
  "activeTournaments": 2,
  "upcomingTournaments": 5,
  "totalParticipants": 150,
  "totalRevenue": 50000000.00,
  "netProfit": 5000000.00,
  "timestamp": "2025-11-19T10:30:00Z"
}
```

### Chi Tiết Các Metrics

#### 1. `activeTournaments` (int)
**Định nghĩa:** Số giải đang diễn ra (Status = `InProgress`)

**Use Case:**
- Hiển thị số giải đang quản lý
- Prioritize active tournaments

**Query:**
```sql
SELECT COUNT(*) 
FROM Tournaments 
WHERE OwnerUserId = @userId 
  AND Status = 'InProgress'
```

---

#### 2. `upcomingTournaments` (int)
**Định nghĩa:** Số giải sắp diễn ra (Status = `Upcoming`)

**Use Case:**
- Planning & preparation
- Forecast workload

**Query:**
```sql
SELECT COUNT(*) 
FROM Tournaments 
WHERE OwnerUserId = @userId 
  AND Status = 'Upcoming'
```

---

#### 3. `totalParticipants` (int) - **LIFETIME**
**Định nghĩa:** Tổng số VĐV tham gia trọn đời (tất cả giải từ trước đến nay)

**⚠️ Important:** Đây là **lifetime metric**, không phải tháng này!

**Use Case:**
- Track tổng reach/impact
- Bragging rights ("Đã tổ chức giải cho 1000+ VĐV")
- Long-term growth metric

**Query:**
```sql
SELECT COUNT(*) 
FROM TournamentPlayers tp
INNER JOIN Tournaments t ON tp.TournamentId = t.Id
WHERE t.OwnerUserId = @userId
-- Không có filter thời gian!
```

**Ví dụ:**
```
- Tournament A (Completed): 50 VĐV
- Tournament B (InProgress): 30 VĐV
- Tournament C (Upcoming): 20 VĐV
→ totalParticipants = 100 (tất cả giải)
```

---

#### 4. `totalRevenue` (decimal) - **NEW in v2.0**
**Định nghĩa:** Tổng dòng tiền (Gross Revenue) = Σ[(EntryFee + AdminFee) × Confirmed]

**Công thức:**
```
TotalRevenue = Σ[(EntryFee + AdminFee) × ConfirmedCount]
```

**Thành phần:**
- **EntryFee**: Tiền giải thưởng (sẽ trả lại cho VĐV thắng cuộc)
- **AdminFee**: Phí quản lý (của Organizer)
- **Tổng**: Tổng tiền mặt Organizer đang giữ

**Use Case:**
- Quản lý cash flow
- Budgeting
- Transparency với sponsors

**Ví dụ:**
```
Tournament A:
- EntryFee = 100,000 VND
- AdminFee = 20,000 VND
- Confirmed = 10 VĐV
→ TotalRevenue = (100,000 + 20,000) × 10 = 1,200,000 VND

Tournament B:
- EntryFee = 200,000 VND
- AdminFee = 50,000 VND
- Confirmed = 20 VĐV
→ TotalRevenue = (200,000 + 50,000) × 20 = 5,000,000 VND

TỔNG: 1,200,000 + 5,000,000 = 6,200,000 VND
```

**⚠️ Lưu Ý:**
- Chỉ tính giải **InProgress** và **Upcoming**
- Không tính giải **Completed** (đã thanh toán xong)
- Chỉ tính VĐV **Confirmed** (không tính Unconfirmed)

---

#### 5. `netProfit` (decimal) - **NEW in v2.0**
**Định nghĩa:** Lợi nhuận ròng = Σ[(AdminFee × Confirmed) - AddedMoney]

**Công thức:**
```
NetProfit = Σ[(AdminFee × ConfirmedCount) - AddedMoney]
```

**Thành phần:**
- **AdminFee × Confirmed**: Tổng phí quản lý thu được
- **AddedMoney**: Tiền sponsor mà Organizer bỏ thêm vào giải thưởng
- **Kết quả**: Số tiền thực tế Organizer "bỏ túi"

**⚠️ Có Thể Âm:**
- Nếu AddedMoney > AdminFee thu được → **Lỗ** (NetProfit âm)
- Nếu AdminFee > AddedMoney → **Lãi** (NetProfit dương)

**Use Cases:**
- Đánh giá hiệu quả kinh doanh
- Quyết định có nên sponsor thêm không
- Financial planning

**Ví dụ:**

**Case 1: Lãi ✅**
```
Tournament A:
- AdminFee = 20,000 VND
- AddedMoney = 0 VND (không sponsor)
- Confirmed = 10 VĐV
→ NetProfit = (20,000 × 10) - 0 = 200,000 VND ✅ LÃI
```

**Case 2: Lỗ ❌**
```
Tournament B:
- AdminFee = 20,000 VND
- AddedMoney = 500,000 VND (sponsor nhiều)
- Confirmed = 10 VĐV
→ NetProfit = (20,000 × 10) - 500,000 = -300,000 VND ❌ LỖ
```

**Case 3: Hòa Vốn**
```
Tournament C:
- AdminFee = 30,000 VND
- AddedMoney = 300,000 VND
- Confirmed = 10 VĐV
→ NetProfit = (30,000 × 10) - 300,000 = 0 VND ⚖️ HÒA
```

**Công dụng:**
- 💰 Biết chính xác đang lãi hay lỗ
- 📊 Compare profitability across tournaments
- 🎯 Optimize pricing strategy

---

## 📋 3.2. Recent Activities

### `GET /api/organizer/dashboard/activities`
**Chức năng:** Lấy lịch sử hoạt động gần đây (30 ngày)

**Query Parameters:**
```typescript
{
  limit?: number;  // Default: 20
}
```

**Use Cases:**
- Timeline của hoạt động organizer
- Quick overview của recent events
- Notification feed

**Response:**
```json
[
  {
    "time": "Vừa xong",
    "message": "VĐV Trần Minh Tú đăng ký giải Vietnam Open 2025",
    "type": "PlayerRegistration"
  },
  {
    "time": "15 phút trước",
    "message": "VĐV Lê Văn Nam đăng ký giải Hanoi Masters",
    "type": "PlayerRegistration"
  },
  {
    "time": "1 giờ trước",
    "message": "Giải \"Vietnam Open 2025\" đã bắt đầu",
    "type": "TournamentStarted"
  },
  {
    "time": "3 giờ trước",
    "message": "Bạn đã tạo giải đấu \"Hanoi Masters\"",
    "type": "TournamentCreated"
  },
  {
    "time": "Hôm qua",
    "message": "Giải \"Southeast Asia Championship\" đã kết thúc",
    "type": "TournamentEnded"
  }
]
```

### Activity Types

| Type | Mô Tả | Use Case |
|------|-------|----------|
| `PlayerRegistration` | VĐV mới đăng ký giải | Monitor registrations |
| `TournamentCreated` | Giải mới được tạo | Track your actions |
| `TournamentStarted` | Giải đã bắt đầu | Monitor active tournaments |
| `TournamentEnded` | Giải đã kết thúc | Track completions |
| `PlayerStatusChanged` | Trạng thái VĐV thay đổi | Approval notifications |

### Time Formatting

API tự động format thời gian thành dạng dễ đọc:
- **< 1 phút**: "Vừa xong"
- **< 1 giờ**: "X phút trước"
- **< 24 giờ**: "X giờ trước"
- **1-2 ngày**: "Hôm qua"
- **< 7 ngày**: "X ngày trước"
- **≥ 7 ngày**: "dd/MM/yyyy HH:mm"

**Công dụng:**
- 📱 Activity feed cho mobile app
- 🔔 Notification center
- 📊 Quick overview of what's happening

---

## 🎨 Dashboard UI Guidelines

### Recommended Layout

```
┌─────────────────────────────────────────────────────────────┐
│  ORGANIZER DASHBOARD                                        │
├─────────────────────────────────────────────────────────────┤
│                                                             │
│  📊 KPI CARDS (từ /api/organizer/dashboard/stats)          │
│  ┌──────────┐ ┌──────────┐ ┌──────────┐ ┌──────────┐      │
│  │ Active   │ │ Upcoming │ │Lifetime  │ │Net Profit│      │
│  │    2     │ │    5     │ │Players   │ │ +5.0M ✅ │      │
│  │          │ │          │ │   150    │ │          │      │
│  └──────────┘ └──────────┘ └──────────┘ └──────────┘      │
│                                                             │
│  💰 FINANCIAL OVERVIEW                                      │
│  ┌───────────────────────────────────────────────────────┐ │
│  │ Total Revenue (Gross): 50,000,000 VND                 │ │
│  │ Net Profit:           +5,000,000 VND  ✅              │ │
│  │ Profit Margin:        10%                             │ │
│  └───────────────────────────────────────────────────────┘ │
│                                                             │
│  📋 RECENT ACTIVITIES                                       │
│  (từ /api/organizer/dashboard/activities)                  │
│  • [Vừa xong] VĐV Trần Minh Tú đăng ký giải Vietnam Open   │
│  • [15 phút trước] VĐV Lê Văn Nam đăng ký Hanoi Masters    │
│  • [1 giờ trước] Giải "Vietnam Open 2025" đã bắt đầu       │
│                                                             │
└─────────────────────────────────────────────────────────────┘
```

### Handling NetProfit Display

```tsx
// Color coding for NetProfit
const getNetProfitStyle = (netProfit: number) => {
  if (netProfit > 0) {
    return { color: 'green', icon: '📈', prefix: '+' };
  } else if (netProfit < 0) {
    return { color: 'red', icon: '📉', prefix: '' };
  } else {
    return { color: 'gray', icon: '⚖️', prefix: '' };
  }
};

// Format currency with style
<NetProfitCard 
  value={stats.netProfit} 
  style={getNetProfitStyle(stats.netProfit)}
/>
```

---

## 🔄 API Version History

### v2.0 (2025-11-19) - Current

#### Breaking Changes:
- ❌ **Removed**: `pendingRegistrations`
- ❌ **Removed**: `estimatedRevenue`
- ✅ **Added**: `totalRevenue` (Gross Revenue)
- ✅ **Added**: `netProfit` (Net Profit)
- ✅ **Changed**: `totalParticipants` từ monthly → lifetime

#### Migration Guide:
```typescript
// OLD (v1.0) ❌
interface OrganizerStats {
  totalParticipants: number;      // Chỉ tháng này
  pendingRegistrations: number;   // DEPRECATED
  estimatedRevenue: number;       // DEPRECATED
}

// NEW (v2.0) ✅
interface OrganizerStats {
  totalParticipants: number;      // LIFETIME (tất cả thời gian)
  totalRevenue: number;           // NEW: Gross Revenue
  netProfit: number;              // NEW: Net Profit (có thể âm!)
}
```

### v1.0 (2025-11-18) - Legacy
- Initial release với `estimatedRevenue` và `pendingRegistrations`

---

## 📊 Use Case Examples

### Use Case 1: Organizer Dashboard Homepage
```
1. User login → Navigate to dashboard
2. Call GET /api/organizer/dashboard/stats
3. Display KPI cards: Active, Upcoming, Participants, Profit
4. Call GET /api/organizer/dashboard/activities?limit=10
5. Display recent activity feed
```

### Use Case 2: Financial Planning
```
1. Organizer xem netProfit = -500k (đang lỗ)
2. Analyze: AddedMoney quá cao
3. Decision: Giảm sponsor hoặc tăng adminFee
4. Create new tournament với pricing mới
5. Monitor netProfit improvement
```

### Use Case 3: Growth Tracking
```
1. Xem totalParticipants = 1000 (lifetime)
2. Set goal: Reach 1500 by end of year
3. Track monthly growth
4. Adjust strategy để attract more players
```

---

## 🔍 FAQ

### Q1: Tại sao không có role "Organizer"?
**A:** Bất kỳ user nào cũng có thể tạo tournament và trở thành organizer. Security được đảm bảo qua data filtering (OwnerUserId).

### Q2: TotalParticipants có bao gồm giải Completed không?
**A:** **Có**. Đây là lifetime metric, bao gồm tất cả giải (Completed, InProgress, Upcoming).

### Q3: NetProfit âm nghĩa là gì?
**A:** Organizer đang lỗ. AddedMoney (sponsor) lớn hơn AdminFee thu được.

### Q4: TotalRevenue và NetProfit tính từ giải nào?
**A:** Chỉ từ giải **InProgress** và **Upcoming**. Không tính Completed (đã thanh toán xong).

### Q5: Làm sao để tăng NetProfit?
**A:** 
- Tăng AdminFee
- Giảm AddedMoney
- Tăng số lượng participants
- Optimize costs

---

## 🎯 Best Practices

### For Frontend Integration

1. **Polling Interval**
   - Stats API: Mỗi 30-60 giây
   - Activities API: Mỗi 2-5 phút

2. **Caching**
   ```javascript
   const cacheTime = 30000; // 30 seconds
   ```

3. **Error Handling**
   ```javascript
   try {
     const stats = await fetch('/api/organizer/dashboard/stats');
     if (!stats.ok) throw new Error('Failed');
   } catch (error) {
     // Show fallback UI
   }
   ```

4. **Loading States**
   - Show skeleton screens while loading
   - Handle 500 errors gracefully
   - Retry failed requests

5. **Negative Number Display**
   ```tsx
   // Always show + or - prefix for NetProfit
   const formatProfit = (value) => {
     const prefix = value >= 0 ? '+' : '';
     return `${prefix}${value.toLocaleString()}`;
   };
   ```

---

## 📚 Related Documentation

- **Admin Players API**: Full CRUD + linking + statistics
- **Admin Users API**: User management + deactivate/reactivate
- **Organizer Dashboard API**: Financial metrics + activities
- **Security Model**: Data isolation strategy
- **Migration Guide v1→v2**: Breaking changes and updates

---

## 📞 Support

Nếu có vấn đề về API:
1. Check server logs
2. Verify JWT token at https://jwt.io
3. Test với Postman/curl
4. Review documentation này
5. Contact backend team

---

**Last Updated:** 2025-11-19  
**API Version:** 2.0  
**Status:** ✅ Production Ready

