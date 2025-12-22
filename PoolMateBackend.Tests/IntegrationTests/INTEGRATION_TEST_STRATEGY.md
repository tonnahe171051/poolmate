# 🎱 PoolMate - Prioritized Integration Test Strategy

> **Tài liệu chiến lược kiểm thử tích hợp** cho hệ thống quản lý giải đấu Billiards  
> **Ngày tạo:** 2025-06-08  
> **Phiên bản:** 1.0

---

## 📋 Mục lục

1. [Tổng quan](#tổng-quan)
2. [Bảng ưu tiên kiểm thử](#bảng-ưu-tiên-kiểm-thử)
3. [Chi tiết từng Phase](#chi-tiết-từng-phase)
4. [Sơ đồ phụ thuộc](#sơ-đồ-phụ-thuộc)
5. [Hướng dẫn thực hiện](#hướng-dẫn-thực-hiện)
6. [Tổng kết](#tổng-kết)

---

## Tổng quan

Tài liệu này xác định thứ tự ưu tiên kiểm thử các module dựa trên nguyên tắc **"Domino Effect"**:

| Tiêu chí | Mô tả |
|----------|-------|
| **Foundation (Nền tảng)** | Module tạo dữ liệu cho các module khác. Nếu hỏng → toàn bộ hệ thống fail |
| **Core Business (Nghiệp vụ chính)** | Lý do chính người dùng sử dụng app (80% giá trị) |
| **End-of-Flow (Kết quả)** | Module xảy ra ở cuối quy trình (Payouts, History) |
| **Auxiliary (Phụ trợ)** | Tính năng độc lập, ít quan trọng (Profile update, Statistics) |

---

## Bảng ưu tiên kiểm thử

| Order | Module Name | Dependency | Why it is Core? | Suggested Test Approach |
|:-----:|-------------|------------|-----------------|------------------------|
| **1** | **Auth** (AuthController, AuthService) | None (Foundation) | **CRITICAL**: Tất cả endpoint yêu cầu authentication. JWT token kiểm soát truy cập tournament, match, admin. Nếu Auth hỏng → **100% protected features fail**. | Test happy path trước (register → confirm email → login). Test validation: duplicate emails, weak passwords, invalid tokens. Test role assignment (Organizer/Admin). |
| **2** | **User Profile** (ProfileController, ProfileService) | Auth | **HIGH**: Dữ liệu user (name, phone, avatar) được tham chiếu khi tạo tournament, player profile, posts. | Test CRUD operations. Validate phone format. Test edge cases: empty fields, profile-not-found. |
| **3** | **Player Profile** (PlayerProfileController, PlayerProfileService) | Auth, User Profile | **HIGH**: Player là **entity cơ bản** cho tournaments. Không có player profile → không thể đăng ký vào tournaments. Model `Player` liên kết với `TournamentPlayer`. | Test profile creation (Admin không được tạo). Test duplicate prevention. Validate linkage to `ApplicationUser`. |
| **4** | **Venues** (VenuesController, VenueService) | Auth | **MEDIUM-HIGH**: Tournament tham chiếu `VenueId`. Tournament có thể tồn tại không có venue, nhưng thực tế cần địa điểm với bàn chơi. | Test create & search. Validate optional fields (city, country filtering). Test unauthorized creation blocked. |
| **5** | **Payout Templates** (PayoutsController, PayoutService) | Auth (Organizer role) | **MEDIUM-HIGH**: Tournament tham chiếu `PayoutTemplateId` để phân chia giải thưởng. Quan trọng cho tính toàn vẹn tài chính. | Test template CRUD. Validate percentage calculations (tổng phải = 100%). Test ownership restrictions. |
| **6** | **Tournament Management** (TournamentsController, TournamentService) | Auth, Player, Venue, Payout | **CRITICAL (Core Business)**: Đây là **trái tim của PoolMate**. Tạo tournament với tất cả settings: bracket type, game type, entry fees, payout mode. Quản lý lifecycle (Upcoming → InProgress → Completed). | Test happy path: create → add players → start. **Test validation nặng**: multi-stage requirements, bracket size, player limits. Test update restrictions theo status. |
| **7** | **Tournament Players** (trong TournamentsController) | Tournament, Player Profile | **CRITICAL**: `TournamentPlayer` là join entity. Không có → không thể generate brackets. Xử lý seeding, status (Confirmed/Unconfirmed), player snapshots. | Test add/remove players. Validate seed uniqueness. Test player count limits (`BracketSizeEstimate`). Test status transitions. |
| **8** | **Bracket Generation** (BracketService) | Tournament, Tournament Players | **CRITICAL (Core Business)**: Generate cấu trúc match (Single/Double Elimination). Đây là **algorithmic core** — nếu bracket generation fail → tournament không thể tiến hành. | Test cả hai bracket types. Test multi-stage logic (`AdvanceToStage2Count`). Validate player seeding/ordering. Test preview vs. create (idempotency). |
| **9** | **Match Management** (MatchesController, BracketService) | Bracket | **CRITICAL (Core Business)**: Update match scores, xác định winner, advance players qua brackets. **Real-time gameplay loop**. | Test score updates. **Test progression logic nặng** (winner → next match, loser → loser bracket). Test concurrency (RowVersion). Test result correction. |
| **10** | **Live Score** (LiveScoreController, TableTokenService) | Match, Tournament Tables | **HIGH**: Enable real-time scoring từ table devices. Sử dụng token-based authentication cho table access. SignalR integration cho live updates. | Test token generation/validation. Test score updates via token. Test unauthorized access blocked. Test active match lookup per table. |
| **11** | **Organizer Dashboard** (OrganizerDashboardController, OrganizerDashboardService) | Tournament, Match, Players | **MEDIUM**: Aggregate stats cho organizers (tournament count, player count, revenue). Read-only nhưng quan trọng cho UX. | Test stats accuracy. Test filtering by tournament status. Test pagination. |
| **12** | **Admin User Management** (AdminUsersController, AdminUserService) | Auth (Admin role) | **MEDIUM**: Cho phép admin view/manage tất cả users. Quan trọng cho platform governance nhưng không nằm trong main user flow. | Test list/filter/detail. Test role-based access (non-admin rejected). Test pagination. |
| **13** | **Admin Player Management** (AdminPlayersController, AdminPlayerService) | Admin Auth, Players | **MEDIUM**: Admin oversight của tất cả player profiles. Bao gồm statistics aggregation. | Test player listing. Test statistics endpoint. Test unauthorized access. |
| **14** | **Admin Dashboard** (AdminDashboardController) | Admin Auth, All Entities | **LOW-MEDIUM**: Summary statistics cho platform admins. Read-only aggregation. | Test summary endpoint. Validate counts are accurate. |
| **15** | **Fargo Rating Integration** (FargoRatingController, FargoRateService) | Tournament, Players | **LOW-MEDIUM**: External API integration cho player skill ratings. Nice-to-have cho seeding nhưng không phải core functionality. | Test batch search. Test apply ratings (mock external API). Test error handling for API failures. |
| **16** | **Media Upload** (MediaController, CloudinaryService) | Auth | **LOW**: Cloudinary signature generation cho avatars, flyers, post images. Isolated utility service. | Test signature generation. Validate folder paths. |
| **17** | **Posts** (PostController, PostService) | Auth, User Profile | **LOW (Add-on)**: Social feature cho users share content. Hoàn toàn isolated khỏi tournament flow. | Test CRUD. Test visibility toggle. Test ownership validation. |

---

## Chi tiết từng Phase

### **Phase 1: Foundation Layer (Phải Pass Trước)**

> ⚠️ **KHÔNG ĐƯỢC BỎ QUA** - Nếu foundation fail, tất cả tests khác đều vô nghĩa

| Priority | Module | Critical Tests |
|----------|--------|----------------|
| 1 | Auth | `Register_WithValidData_ReturnsSuccess` |
| | | `Login_WithValidCredentials_ReturnsJWT` |
| | | `Login_UnconfirmedEmail_Returns403` |
| | | `ConfirmEmail_ValidToken_Succeeds` |
| | | `ForgotPassword_ValidEmail_SendsResetLink` |
| | | `ChangePassword_ValidCurrentPassword_Succeeds` |
| 2 | User Profile | `GetMe_Authenticated_ReturnsProfile` |
| | | `Update_ValidPhone_Succeeds` |
| | | `Update_InvalidPhoneFormat_Returns400` |
| | | `GetUserProfile_ExistingUser_ReturnsData` |
| 3 | Player Profile | `CreatePlayerProfile_NewUser_ReturnsCreated` |
| | | `CreatePlayerProfile_AdminRole_Returns403` |
| | | `CreatePlayerProfile_AlreadyExists_Returns409` |
| | | `GetMyProfiles_ReturnsLinkedProfiles` |

---

### **Phase 2: Tournament Setup Layer**

> 🔧 Các module cần thiết để **thiết lập** tournament

| Priority | Module | Critical Tests |
|----------|--------|----------------|
| 4 | Venues | `Create_Authenticated_ReturnsId` |
| | | `Create_Unauthenticated_Returns401` |
| | | `Search_ByCity_ReturnsFiltered` |
| | | `Search_ByCountry_ReturnsFiltered` |
| 5 | Payout Templates | `CreateTemplate_ValidPercentages_Succeeds` |
| | | `CreateTemplate_InvalidSum_Fails` (tổng ≠ 100%) |
| | | `GetTemplates_ReturnsOnlyOwned` |
| | | `GetTemplateById_NotOwner_Returns404` |
| 6 | Tournament | `Create_WithAllSettings_ReturnsId` |
| | | `Create_MultiStage_ValidAdvanceCount_Succeeds` |
| | | `Create_MultiStage_InvalidAdvanceCount_Fails` |
| | | `Create_MultiStage_SingleElimination_Fails` |
| | | `Update_BeforeStart_Succeeds` |
| | | `Update_AfterStart_Blocked` |
| | | `GetTournamentDetail_Public_ReturnsData` |
| | | `GetMyTournaments_ReturnsPaginated` |
| 7 | Tournament Players | `AddPlayer_UnderLimit_Succeeds` |
| | | `AddPlayer_AtLimit_Fails` |
| | | `AddPlayer_DuplicateSeed_Fails` |
| | | `AddPlayer_InvalidPhone_Fails` |
| | | `RemovePlayer_BeforeStart_Succeeds` |
| | | `RemovePlayer_AfterStart_Blocked` |
| | | `UpdatePlayerSeed_UniqueSeed_Succeeds` |

---

### **Phase 3: Core Business Logic (80% Giá trị)**

> 🎯 Đây là **LÝ DO TỒN TẠI** của PoolMate - Test kỹ nhất

| Priority | Module | Critical Tests |
|----------|--------|----------------|
| 8 | Bracket Generation | `Preview_DoubleElimination_ReturnsStructure` |
| | | `Preview_SingleElimination_ReturnsStructure` |
| | | `Preview_LessThan2Players_Fails` |
| | | `Preview_MultiStage_InsufficientPlayers_Fails` |
| | | `CreateBracket_DoubleElimination_GeneratesCorrectStructure` |
| | | `CreateBracket_SeededOrdering_RespectsSeedPositions` |
| | | `CreateBracket_RandomOrdering_ShufflesPlayers` |
| | | `CreateBracket_AlreadyStarted_Fails` |
| 9 | Match Management | `UpdateMatch_SetScore_UpdatesStatus` |
| | | `UpdateMatch_SetWinner_AdvancesToNextRound` |
| | | `UpdateMatch_LoserBracket_MovesToLoserSide` |
| | | `UpdateMatch_Concurrent_ConflictHandled` (RowVersion) |
| | | `CorrectResult_RevertsWinnerProgression` |
| | | `CorrectResult_UpdatesLoserBracket` |
| | | `GetMatch_ReturnsFullDetails` |
| 10 | Live Score | `GenerateToken_ValidTable_ReturnsToken` |
| | | `GenerateToken_InvalidTable_Returns404` |
| | | `GetActiveMatch_ValidToken_ReturnsMatch` |
| | | `GetActiveMatch_NoActiveMatch_Returns204` |
| | | `UpdateScore_ValidToken_Succeeds` |
| | | `UpdateScore_ExpiredToken_Fails` |
| | | `UpdateScore_InvalidToken_Returns401` |

---

### **Phase 4: Dashboards & Analytics**

> 📊 Read-only aggregation - Ít critical hơn nhưng cần cho UX

| Priority | Module | Critical Tests |
|----------|--------|----------------|
| 11 | Organizer Dashboard | `GetStats_ReturnsAccurateCounts` |
| | | `GetTournaments_FilterByStatus_Works` |
| | | `GetPlayers_SearchByName_ReturnsFiltered` |
| | | `GetTournamentOverview_ReturnsDetails` |
| 12 | Admin Users | `GetUsers_AdminRole_Succeeds` |
| | | `GetUsers_NonAdmin_Returns403` |
| | | `GetUserDetail_ExistingUser_ReturnsData` |
| | | `GetUsers_FilterByRole_Works` |
| 13 | Admin Players | `GetPlayers_AdminRole_ReturnsPaginated` |
| | | `GetPlayerStatistics_ReturnsAggregates` |
| | | `GetPlayerDetail_ExistingPlayer_ReturnsData` |
| 14 | Admin Dashboard | `GetSummary_ReturnsAllCounts` |

---

### **Phase 5: Auxiliary Features**

> 🎨 Nice-to-have - Test sau khi core features stable

| Priority | Module | Critical Tests |
|----------|--------|----------------|
| 15 | Fargo Rating | `BatchSearch_ValidRequests_ReturnsMappedResults` |
| | | `ApplyRatings_ValidTournament_UpdatesSeeds` |
| | | `ApplyRatings_EmptyList_Returns400` |
| | | `BatchSearch_ExternalAPIError_HandlesGracefully` |
| 16 | Media | `SignUpload_Avatar_ReturnsValidSignature` |
| | | `SignPostImageUpload_ReturnsSignature` |
| | | `SignFlyerUpload_ReturnsSignature` |
| 17 | Posts | `CreatePost_ReturnsId` |
| | | `UpdatePost_Owner_Succeeds` |
| | | `UpdatePost_NotOwner_Returns404` |
| | | `ToggleVisibility_TogglesIsPublic` |
| | | `HardDelete_Owner_RemovesPermanently` |
| | | `GetMyPosts_ReturnsOwnedPosts` |

---

## Sơ đồ phụ thuộc

```
                    ┌─────────────────────────────────────────────────────────┐
                    │                     [FOUNDATION]                        │
                    │                                                         │
                    │    ┌──────────┐                                        │
                    │    │   AUTH   │ ◄── Mọi thứ phụ thuộc vào đây          │
                    │    └────┬─────┘                                        │
                    │         │                                               │
                    └─────────┼───────────────────────────────────────────────┘
                              │
           ┌──────────────────┼──────────────────┐
           │                  │                  │
           ▼                  ▼                  ▼
    ┌─────────────┐    ┌─────────────┐    ┌─────────────┐
    │   Profile   │    │   Venues    │    │   Payout    │
    │   (User)    │    │             │    │  Templates  │
    └──────┬──────┘    └──────┬──────┘    └──────┬──────┘
           │                  │                  │
           ▼                  │                  │
    ┌─────────────┐           │                  │
    │   Player    │           │                  │
    │   Profile   │           │                  │
    └──────┬──────┘           │                  │
           │                  │                  │
           └──────────────────┼──────────────────┘
                              │
                              ▼
                    ┌─────────────────────┐
                    │     TOURNAMENT      │ ◄── CORE BUSINESS
                    │   (The Hub Entity)  │
                    └──────────┬──────────┘
                               │
              ┌────────────────┼────────────────┐
              │                │                │
              ▼                ▼                ▼
    ┌─────────────────┐ ┌─────────────┐ ┌─────────────┐
    │ TournamentPlayer│ │   Tables    │ │   Stages    │
    │   (Seeding)     │ │             │ │             │
    └────────┬────────┘ └──────┬──────┘ └──────┬──────┘
             │                 │               │
             └─────────────────┼───────────────┘
                               │
                               ▼
                    ┌─────────────────────┐
                    │    BRACKET/MATCH    │ ◄── CORE ALGORITHM
                    │    (Game Engine)    │
                    └──────────┬──────────┘
                               │
              ┌────────────────┼────────────────┐
              │                │                │
              ▼                ▼                ▼
       ┌───────────┐    ┌───────────┐    ┌───────────┐
       │ LiveScore │    │ Dashboard │    │  Results  │
       │  (Real-   │    │  (Stats)  │    │  History  │
       │   time)   │    │           │    │           │
       └───────────┘    └───────────┘    └───────────┘
```

---

## Hướng dẫn thực hiện

### 1. Thiết lập Test Infrastructure

```csharp
// Tạo JWT token helper để reuse across tests
public static class TestAuthHelper
{
    public static string GenerateTestToken(string userId, string role)
    {
        // Implementation...
    }
}

// Tạo Test Data Factory
public static class TestDataFactory
{
    public static Tournament CreateValidTournament(string ownerId) { }
    public static Player CreateValidPlayer(string userId) { }
    public static TournamentPlayer CreateTournamentPlayer(int tournamentId, int playerId) { }
}
```

### 2. Nguyên tắc viết test

| Nguyên tắc | Mô tả |
|------------|-------|
| **Happy Path First** | Test case thành công trước, edge cases sau |
| **One Assert Per Test** | Mỗi test chỉ verify một behavior |
| **Descriptive Names** | `MethodName_Scenario_ExpectedBehavior` |
| **Arrange-Act-Assert** | Cấu trúc test rõ ràng |
| **Independent Tests** | Mỗi test tự setup/cleanup data |

### 3. Mock External Services

```csharp
// Mock Cloudinary
services.AddScoped<ICloudinaryService, MockCloudinaryService>();

// Mock Fargo Rate API
services.AddScoped<IFargoRateService, MockFargoRateService>();

// Mock Email Service
services.AddScoped<IEmailService, MockEmailService>();
```

### 4. Concurrency Testing cho Match

```csharp
[Fact]
public async Task UpdateMatch_ConcurrentUpdates_HandlesConflict()
{
    // Arrange: Get same match from 2 different contexts
    var match1 = await _context1.Matches.FindAsync(matchId);
    var match2 = await _context2.Matches.FindAsync(matchId);
    
    // Act: Update from both contexts
    match1.ScoreP1 = 5;
    match2.ScoreP1 = 3;
    
    await _context1.SaveChangesAsync(); // Succeeds
    
    // Assert: Second save should throw DbUpdateConcurrencyException
    await Assert.ThrowsAsync<DbUpdateConcurrencyException>(
        () => _context2.SaveChangesAsync()
    );
}
```

---

## Tổng kết

### Phân bổ Coverage Priority

| Layer | Modules | % Priority |
|-------|---------|------------|
| **Foundation** | Auth, Profile, Player | **25%** - Phải rock solid |
| **Setup** | Venue, Payout, Tournament, TournamentPlayer | **30%** - Enable core flow |
| **Core Business** | Bracket, Match, LiveScore | **35%** - Đây LÀ product |
| **Auxiliary** | Dashboard, Admin, Posts, Media, Fargo | **10%** - Nice to have |

### Checklist trước khi release

- [ ] Tất cả Phase 1 tests pass
- [ ] Tất cả Phase 2 tests pass
- [ ] Tất cả Phase 3 tests pass (đặc biệt bracket progression)
- [ ] Phase 4-5 tests có coverage > 70%
- [ ] Không có flaky tests
- [ ] Performance tests cho bracket generation (>64 players)

---

## Files Test hiện có

Dựa trên cấu trúc thư mục, các file test đã có:

```
IntegrationTests/
├── Base/                                    # Test infrastructure
├── Services/
│   ├── AdminPlayerServiceIntegrationTests.cs
│   ├── BracketServiceIntegrationTests.cs    ✅ Core
│   ├── PayoutServiceIntegrationTests.cs     ✅ Core  
│   └── TournamentServiceIntegrationTests.cs ✅ Core
```

### Cần bổ sung tests cho:

1. **AuthService** (Priority 1) - Chưa có
2. **ProfileService** (Priority 2) - Chưa có
3. **PlayerProfileService** (Priority 3) - Chưa có
4. **VenueService** (Priority 4) - Chưa có
5. **MatchController/BracketService.UpdateMatch** (Priority 9) - Kiểm tra coverage
6. **LiveScoreController** (Priority 10) - Chưa có

---

> 📝 **Ghi chú**: Tài liệu này nên được cập nhật khi có thay đổi về cấu trúc module hoặc business requirements.

