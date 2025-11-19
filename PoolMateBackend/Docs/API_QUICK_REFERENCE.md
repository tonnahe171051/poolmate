# 📋 API Quick Reference - Admin & Organizer Modules

## 🎯 Admin Players Module (`/api/admin/players`)

| Endpoint | Method | Chức Năng | Use Case |
|----------|--------|-----------|----------|
| `/` | GET | Danh sách Players (filter, search, sort, pagination) | Quản lý tất cả VĐV |
| `/statistics` | GET | Thống kê tổng quan Players | Dashboard overview |
| `/unclaimed` | GET | Players chưa link với User | Tìm profiles cần claim |
| `/{id}` | GET | Chi tiết 1 Player | View full profile |
| `/{id}/link-user` | POST | Link Player với User | Claim player profile |
| `/{id}/unlink-user` | POST | Unlink Player khỏi User | Sửa lỗi linking |
| `/{id}/linked-user` | GET | Xem User đã link | Verify ownership |
| `/user/{userId}` | GET | All Players của 1 User | Multi-profile management |
| `/bulk-link` | POST | Link nhiều Players cùng lúc | Batch operations |
| `/bulk-unlink` | POST | Unlink nhiều Players cùng lúc | Batch corrections |
| `/data-quality` | GET | Báo cáo chất lượng dữ liệu | Data health check |
| `/issues/{type}` | GET | Players có issue cụ thể | Data cleanup |
| `/validate` | POST | Validate dữ liệu Player | Form validation |
| `/export` | GET | Export Players ra CSV | Backup & reporting |

### Key Features:
- ✅ **Player-User Linking**: Core feature để user claim player profiles
- 📊 **Statistics**: Phân tích phân bố, growth trends, activity metrics
- 🔍 **Data Quality**: Phát hiện missing/invalid data
- 📥 **Bulk Operations**: Xử lý hàng loạt
- 📤 **Export**: CSV export với filters

---

## 👥 Admin Users Module (`/api/admin/users`)

| Endpoint | Method | Chức Năng | Use Case |
|----------|--------|-----------|----------|
| `/` | GET | Danh sách Users (filter, search, sort, pagination) | Quản lý user base |
| `/{id}` | GET | Chi tiết 1 User | Full profile view |
| `/{id}/deactivate` | PUT | Vô hiệu hóa tài khoản | Ban/suspend user |
| `/{id}/reactivate` | PUT | Kích hoạt lại tài khoản | Restore access |
| `/statistics` | GET | Thống kê tổng quan Users | Dashboard KPIs |
| `/{id}/activity-log` | GET | Activity log của User | Investigate behavior |
| `/bulk-deactivate` | POST | Deactivate nhiều Users | Mass ban operation |
| `/bulk-reactivate` | POST | Reactivate nhiều Users | Mass restore |
| `/export` | GET | Export Users ra CSV | Backup & compliance |

### Key Features:
- 🔐 **Account Management**: Deactivate/Reactivate với audit trail
- 📊 **Statistics**: User growth, role distribution, security metrics
- 🔍 **Activity Tracking**: Monitor user behavior
- 📥 **Bulk Operations**: Mass account operations
- 📤 **Export**: Compliance reporting

---

## 📊 Organizer Dashboard Module (`/api/organizer/dashboard`)

| Endpoint | Method | Chức Năng | Use Case |
|----------|--------|-----------|----------|
| `/stats` | GET | KPI Statistics | Dashboard overview |
| `/activities` | GET | Recent Activities (30 days) | Activity feed |

### Key Metrics (v2.0):

```json
{
  "activeTournaments": 2,        // Giải InProgress
  "upcomingTournaments": 5,      // Giải Upcoming
  "totalParticipants": 150,      // LIFETIME count
  "totalRevenue": 50000000,      // Gross Revenue
  "netProfit": 5000000           // Net Profit (có thể âm)
}
```

### Key Features:
- 📊 **Financial Metrics**: TotalRevenue & NetProfit (NEW in v2.0)
- 📈 **Lifetime Tracking**: Total participants across all tournaments
- 📋 **Activity Feed**: Recent events với time formatting
- 🔒 **Data Isolation**: Chỉ xem data của mình (filter by OwnerUserId)

---

## 🔑 Authentication & Authorization

### Admin Players API
```
Authorization: Bearer <JWT_TOKEN>
Required Role: Admin
```

### Admin Users API
```
Authorization: Bearer <JWT_TOKEN>
Required Role: Admin
```

### Organizer Dashboard API
```
Authorization: Bearer <JWT_TOKEN>
Required Role: Any logged-in user
Data Isolation: Automatic filtering by OwnerUserId
```

---

## 📊 Response Formats

### Pagination (Admin APIs)
```json
{
  "items": [...],
  "totalCount": 500,
  "pageIndex": 1,
  "pageSize": 20,
  "hasNextPage": true
}
```

### Success Response
```json
{
  "success": true,
  "data": {...},
  "message": "Operation successful"
}
```

### Error Response
```json
{
  "success": false,
  "message": "Error description",
  "errors": ["Detail 1", "Detail 2"]
}
```

---

## 🧮 Financial Formulas (Organizer Dashboard)

### Total Revenue (Gross)
```
TotalRevenue = Σ[(EntryFee + AdminFee) × ConfirmedCount]
```
- **EntryFee**: Tiền giải thưởng
- **AdminFee**: Phí quản lý
- **Scope**: Chỉ giải InProgress + Upcoming

### Net Profit
```
NetProfit = Σ[(AdminFee × ConfirmedCount) - AddedMoney]
```
- **AdminFee × Confirmed**: Phí thu được
- **AddedMoney**: Tiền sponsor
- **Có thể âm**: Nếu sponsor > phí thu được

---

## 🎯 Common Use Cases

### 1. Player Claiming Workflow
```
1. User search unclaimed player
   GET /api/admin/players/unclaimed?search=name

2. System suggests match based on email
   Response: potentialUsers with confidence score

3. User/Admin confirms linking
   POST /api/admin/players/{id}/link-user
   Body: { userId: "..." }

4. Verify linking
   GET /api/admin/players/{id}/linked-user
```

### 2. User Investigation Workflow
```
1. Admin searches for user
   GET /api/admin/users?search=username

2. View detailed profile
   GET /api/admin/users/{id}

3. Check activity log
   GET /api/admin/users/{id}/activity-log

4. Take action if needed
   PUT /api/admin/users/{id}/deactivate
```

### 3. Organizer Dashboard Loading
```
1. Load KPI stats
   GET /api/organizer/dashboard/stats

2. Load recent activities
   GET /api/organizer/dashboard/activities?limit=10

3. Refresh every 30-60 seconds
   setInterval(() => refreshStats(), 30000)
```

---

## 📈 Data Quality Issues (Admin Players)

| Issue Type | Meaning | Fix Action |
|------------|---------|------------|
| `missing-email` | Player không có email | Contact player để update |
| `missing-phone` | Player không có phone | Request phone number |
| `missing-skill` | Thiếu skill level | Estimate from tournament history |
| `invalid-email` | Email format sai | Validate & correct |
| `invalid-phone` | Phone format sai | Normalize phone format |
| `inactive-1y` | 1 năm không thi đấu | Verify still active |
| `never-played` | Chưa thi đấu giải nào | Consider removing |

---

## 🔄 Bulk Operation Best Practices

### Request Format
```json
{
  "items": [...],           // Array of IDs hoặc objects
  "reason": "...",          // Optional: Lý do
  "force": false            // Optional: Force operation
}
```

### Response Format
```json
{
  "totalRequested": 10,
  "successCount": 9,
  "failedCount": 1,
  "results": [
    {
      "id": "...",
      "success": true,
      "message": "Success"
    },
    {
      "id": "...",
      "success": false,
      "message": "Error: ..."
    }
  ]
}
```

### Best Practices
- ✅ Validate all items trước khi process
- ✅ Process từng item riêng biệt
- ✅ Return detailed results per item
- ✅ Log bulk operations với reason
- ✅ Consider rollback strategy

---

## 📊 Statistics Breakdown

### Player Statistics
```json
{
  "overview": {
    "totalPlayers": 5000,
    "linkedPlayers": 3500,
    "unlinkedPlayers": 1500,
    "activePlayersThisMonth": 1200
  },
  "distributionByCountry": [...],
  "distributionBySkillLevel": [...],
  "growthTrend": {...}
}
```

### User Statistics
```json
{
  "overview": {
    "totalUsers": 1000,
    "activeUsers": 850,
    "inactiveUsers": 150
  },
  "roleDistribution": {...},
  "securityMetrics": {...},
  "growthTrend": {...}
}
```

---

## 🚦 HTTP Status Codes

| Code | Meaning | When Used |
|------|---------|-----------|
| 200 | OK | Success response |
| 400 | Bad Request | Invalid input, validation error |
| 401 | Unauthorized | Missing/invalid JWT token |
| 403 | Forbidden | Insufficient permissions |
| 404 | Not Found | Resource doesn't exist |
| 500 | Internal Server Error | Unexpected error |

---

## 🔧 Testing Examples

### cURL - Admin Players
```bash
curl -X GET "https://localhost:7127/api/admin/players?pageIndex=1&pageSize=20" \
  -H "Authorization: Bearer YOUR_TOKEN" \
  -k
```

### cURL - Organizer Dashboard
```bash
curl -X GET "https://localhost:7127/api/organizer/dashboard/stats" \
  -H "Authorization: Bearer YOUR_TOKEN" \
  -k
```

### PowerShell
```powershell
$token = "YOUR_TOKEN"
$headers = @{ "Authorization" = "Bearer $token" }

Invoke-RestMethod -Uri "https://localhost:7127/api/admin/users/statistics" `
  -Headers $headers `
  -SkipCertificateCheck | ConvertTo-Json
```

---

## 📝 Version History

### Organizer Dashboard
- **v2.0 (2025-11-19)**: Breaking changes
  - ❌ Removed: `pendingRegistrations`, `estimatedRevenue`
  - ✅ Added: `totalRevenue`, `netProfit`
  - ✅ Changed: `totalParticipants` → lifetime
- **v1.0 (2025-11-18)**: Initial release

### Admin Modules
- **v1.0 (2025-11)**: Stable release

---

## 🎯 Quick Links

- **Full Documentation**: `API_DOCUMENTATION.md`
- **Organizer Dashboard v2.0 Spec**: `ORGANIZER_DASHBOARD_API_V2.md`
- **Implementation Summary**: `IMPLEMENTATION_SUMMARY.md`
- **Security Model**: `SECURITY_MODEL_EXPLANATION.md`

---

**Last Updated:** 2025-11-19  
**Status:** Production Ready ✅

