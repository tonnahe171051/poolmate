# Tournament Management Module - Integration Test Checklist

> **Module:** TournamentsController & TournamentService (Core CRUD & Lifecycle)  
> **Strategy:** Lean & Effective (80/20 Rule)  
> **Last Updated:** 2025-12-08

---

## 📋 Summary

| Endpoint | Method | Auth Required | Total Tests |
|:---------|:-------|:--------------|:------------|
| `POST /api/tournaments` | POST | ✅ Authorized | 5 |
| `GET /api/tournaments/{id}` | GET | ❌ Anonymous | 2 |
| `GET /api/tournaments` | GET | ❌ Anonymous | 2 |
| `GET /api/tournaments/my-tournaments` | GET | ✅ Authorized | 1 |
| `PUT /api/tournaments/{id}` | PUT | ✅ Owner | 5 |
| `PATCH /api/tournaments/{id}/flyer` | PATCH | ✅ Owner | 2 |
| `POST /api/tournaments/{id}/start` | POST | ✅ Owner | 3 |
| `DELETE /api/tournaments/{id}` | DELETE | ✅ Owner | 4 |
| `GET /api/tournaments/payout-templates` | GET | ✅ Authorized | 1 |
| **Tournament Tables** | | | |
| `POST /api/tournaments/{id}/tables` | POST | ✅ Owner | 2 |
| `POST /api/tournaments/{id}/tables/bulk` | POST | ✅ Owner | 2 |
| `PUT /api/tournaments/{id}/tables/{tableId}` | PUT | ✅ Owner | 1 |
| `DELETE /api/tournaments/{id}/tables` | DELETE | ✅ Owner | 2 |
| `GET /api/tournaments/{id}/tables` | GET | ❌ Anonymous | 1 |
| **TOTAL** | | | **33** |

---

## 🧪 Test Cases

### 1. Create Tournament (`POST /api/tournaments`)

| ID | Category | Scenario Name | Pre-Condition (Arrange) | Action (Act) | Critical Verification (Assert) |
|:---|:---------|:--------------|:------------------------|:-------------|:------------------------------|
| TM-CREATE-01 | ✅ Positive | Create_SingleStage_Success | Auth as Organizer | POST valid `CreateTournamentModel` with Name, BracketSizeEstimate=16, BracketType=DoubleElimination | Returns 200 OK with `id`, DB record has `Status=Upcoming`, `IsStarted=false`, `TotalPrize` calculated correctly |
| TM-CREATE-02 | ✅ Positive | Create_MultiStage_Success | Auth as Organizer | POST with `IsMultiStage=true`, `AdvanceToStage2Count=8`, `Stage1Type=DoubleElimination` | Returns 200 OK, DB record has `IsMultiStage=true`, `AdvanceToStage2Count=8` |
| TM-CREATE-03 | ⚠️ Negative | Create_MultiStage_InvalidAdvanceCount_Fails | Auth as Organizer | POST with `IsMultiStage=true`, `AdvanceToStage2Count=5` (not power of 2) | Returns 400 BadRequest, Message="AdvanceToStage2Count must be a power of 2" |
| TM-CREATE-04 | ⚠️ Negative | Create_MultiStage_SingleElimination_Fails | Auth as Organizer | POST with `IsMultiStage=true`, `Stage1Type=SingleElimination` | Returns 400 BadRequest, Message="Single Elimination is not compatible with multi-stage tournaments" |
| TM-CREATE-05 | 🔒 Security | Create_Unauthorized_Returns401 | No authentication | POST valid tournament data | Returns 401 Unauthorized |

---

### 2. Get Tournament Detail (`GET /api/tournaments/{id}`)

| ID | Category | Scenario Name | Pre-Condition (Arrange) | Action (Act) | Critical Verification (Assert) |
|:---|:---------|:--------------|:------------------------|:-------------|:------------------------------|
| TM-DETAIL-01 | ✅ Positive | GetDetail_ExistingTournament_Success | Tournament exists in DB | GET `/api/tournaments/{id}` | Returns 200 OK with full `TournamentDetailDto` including Venue, TotalPlayers, TotalTables, MultiStage settings |
| TM-DETAIL-02 | ⚠️ Negative | GetDetail_NotFound_Returns404 | Tournament ID does not exist | GET with invalid ID | Returns 404 NotFound |

---

### 3. Get Public Tournaments List (`GET /api/tournaments`)

| ID | Category | Scenario Name | Pre-Condition (Arrange) | Action (Act) | Critical Verification (Assert) |
|:---|:---------|:--------------|:------------------------|:-------------|:------------------------------|
| TM-LIST-01 | ✅ Positive | GetList_PublicOnly_Success | Mix of public and private tournaments | GET without filters | Returns 200 OK, Only `IsPublic=true` tournaments, Paginated response |
| TM-LIST-02 | ✅ Positive | GetList_WithFilters_ReturnsFiltered | Multiple tournaments with different status/gameType | GET with `status=InProgress&gameType=NineBall` | Returns only matching tournaments |

---

### 4. Get My Tournaments (`GET /api/tournaments/my-tournaments`)

| ID | Category | Scenario Name | Pre-Condition (Arrange) | Action (Act) | Critical Verification (Assert) |
|:---|:---------|:--------------|:------------------------|:-------------|:------------------------------|
| TM-MY-01 | ✅ Positive | GetMyTournaments_ReturnsOwnerOnly | Auth as User A, User A owns 2 tournaments, User B owns 3 | GET my-tournaments | Returns only User A's 2 tournaments |

---

### 5. Update Tournament (`PUT /api/tournaments/{id}`)

| ID | Category | Scenario Name | Pre-Condition (Arrange) | Action (Act) | Critical Verification (Assert) |
|:---|:---------|:--------------|:------------------------|:-------------|:------------------------------|
| TM-UPDATE-01 | ✅ Positive | Update_BasicFields_Success | Auth as Owner, Tournament `Status=Upcoming` | PUT with new Name, Description, StartUtc | Returns 200 OK, DB updated with new values |
| TM-UPDATE-02 | ✅ Positive | Update_BracketSettings_Success | Auth as Owner, Tournament `Status=Upcoming`, No bracket created | PUT with new `BracketType`, `BracketOrdering`, `WinnersRaceTo` | Returns 200 OK, Bracket settings updated |
| TM-UPDATE-03 | ⚠️ Negative | Update_ReduceBracketSize_BelowPlayerCount_Fails | Auth as Owner, Tournament has 10 players | PUT with `BracketSizeEstimate=8` | Returns 400 BadRequest, Message="Cannot reduce bracket size below current player count" |
| TM-UPDATE-04 | ⚠️ Negative | Update_BracketSettingsAfterStart_Blocked | Auth as Owner, Tournament `IsStarted=true` | PUT with `BracketType=SingleElimination` | Returns 200 OK BUT bracket fields NOT changed (silently ignored due to `CanEditBracket` check) |
| TM-UPDATE-05 | 🔒 Security | Update_NotOwner_Returns403 | Auth as different user (not owner) | PUT any data | Returns 403 Forbidden |

---

### 6. Update Flyer (`PATCH /api/tournaments/{id}/flyer`)

| ID | Category | Scenario Name | Pre-Condition (Arrange) | Action (Act) | Critical Verification (Assert) |
|:---|:---------|:--------------|:------------------------|:-------------|:------------------------------|
| TM-FLYER-01 | ✅ Positive | UpdateFlyer_NewImage_Success | Auth as Owner, Tournament exists | PATCH with `FlyerUrl` and `FlyerPublicId` | Returns 200 OK, DB updated with new flyer, Old flyer deleted from Cloudinary |
| TM-FLYER-02 | 🔒 Security | UpdateFlyer_NotOwner_Returns403 | Auth as different user | PATCH with flyer data | Returns 403 Forbidden |

---

### 7. Start Tournament (`POST /api/tournaments/{id}/start`)

| ID | Category | Scenario Name | Pre-Condition (Arrange) | Action (Act) | Critical Verification (Assert) |
|:---|:---------|:--------------|:------------------------|:-------------|:------------------------------|
| TM-START-01 | ✅ Positive | Start_UpcomingTournament_Success | Auth as Owner, Tournament `Status=Upcoming`, `IsStarted=false` | POST to start endpoint | Returns 200 OK, DB: `IsStarted=true`, `Status=InProgress` |
| TM-START-02 | 💣 Edge | Start_AlreadyStarted_Idempotent | Auth as Owner, Tournament `IsStarted=true` | POST to start endpoint | Returns 200 OK (idempotent - no error) |
| TM-START-03 | 🔒 Security | Start_NotOwner_Returns403 | Auth as different user | POST to start | Returns 403 Forbidden |

---

### 8. Delete Tournament (`DELETE /api/tournaments/{id}`)

| ID | Category | Scenario Name | Pre-Condition (Arrange) | Action (Act) | Critical Verification (Assert) |
|:---|:---------|:--------------|:------------------------|:-------------|:------------------------------|
| TM-DELETE-01 | ✅ Positive | Delete_UpcomingTournament_Success | Auth as Owner, Tournament `Status=Upcoming`, has players/tables/matches | DELETE tournament | Returns 200 OK, Tournament + related Matches deleted, Flyer deleted from Cloudinary |
| TM-DELETE-02 | ✅ Positive | Delete_CompletedTournament_Success | Auth as Owner, Tournament `Status=Completed` | DELETE tournament | Returns 200 OK, Tournament deleted |
| TM-DELETE-03 | ⚠️ Negative | Delete_InProgressTournament_Fails | Auth as Owner, Tournament `Status=InProgress` | DELETE tournament | Returns 400 BadRequest, Message="Tournament can only be deleted before it starts or after it's completed" |
| TM-DELETE-04 | 🔒 Security | Delete_NotOwner_Returns404 | Auth as different user | DELETE tournament | Returns 404 NotFound |

---

### 9. Get Payout Templates (`GET /api/tournaments/payout-templates`)

| ID | Category | Scenario Name | Pre-Condition (Arrange) | Action (Act) | Critical Verification (Assert) |
|:---|:---------|:--------------|:------------------------|:-------------|:------------------------------|
| TM-PAYOUT-01 | ✅ Positive | GetPayoutTemplates_ReturnsUserTemplates | Auth as User, User owns templates | GET payout-templates | Returns 200 OK, Only user's own templates with parsed percentages |

---

### 10. Tournament Tables - Add Single (`POST /api/tournaments/{id}/tables`)

| ID | Category | Scenario Name | Pre-Condition (Arrange) | Action (Act) | Critical Verification (Assert) |
|:---|:---------|:--------------|:------------------------|:-------------|:------------------------------|
| TM-TABLE-01 | ✅ Positive | AddTable_Success | Auth as Owner, Tournament exists | POST `AddTournamentTableModel` with Label | Returns 200 OK with `id`, `label`, DB record with `Status=Open`, `IsStreaming=false` |
| TM-TABLE-02 | 🔒 Security | AddTable_NotOwner_Returns404 | Auth as different user | POST table data | Returns 404 NotFound |

---

### 11. Tournament Tables - Bulk Add (`POST /api/tournaments/{id}/tables/bulk`)

| ID | Category | Scenario Name | Pre-Condition (Arrange) | Action (Act) | Critical Verification (Assert) |
|:---|:---------|:--------------|:------------------------|:-------------|:------------------------------|
| TM-TABLE-BULK-01 | ✅ Positive | BulkAddTables_Success | Auth as Owner, Tournament exists | POST with `StartNumber=1, EndNumber=5` | Returns 200 OK with `addedCount=5`, Tables labeled "Table 1" to "Table 5" |
| TM-TABLE-BULK-02 | ⚠️ Negative | BulkAddTables_ExceedsLimit_Fails | Auth as Owner | POST with range > 50 tables | Returns 400 BadRequest, Message="Cannot add more than 50 tables at once" |

---

### 12. Tournament Tables - Update (`PUT /api/tournaments/{id}/tables/{tableId}`)

| ID | Category | Scenario Name | Pre-Condition (Arrange) | Action (Act) | Critical Verification (Assert) |
|:---|:---------|:--------------|:------------------------|:-------------|:------------------------------|
| TM-TABLE-UPD-01 | ✅ Positive | UpdateTable_Success | Auth as Owner, Table exists | PUT with new Label, Status, LiveStreamUrl | Returns 200 OK, DB updated with all fields |

---

### 13. Tournament Tables - Delete (`DELETE /api/tournaments/{id}/tables`)

| ID | Category | Scenario Name | Pre-Condition (Arrange) | Action (Act) | Critical Verification (Assert) |
|:---|:---------|:--------------|:------------------------|:-------------|:------------------------------|
| TM-TABLE-DEL-01 | ✅ Positive | DeleteTables_Success | Auth as Owner, Tournament `Status=Upcoming`, Tables exist | DELETE with `TableIds=[1,2,3]` | Returns 200 OK, `deletedCount=3`, Tables removed from DB |
| TM-TABLE-DEL-02 | ⚠️ Negative | DeleteTables_TournamentStarted_Fails | Auth as Owner, Tournament `Status=InProgress` | DELETE table IDs | Returns 400 BadRequest, Message="Cannot delete tables after tournament has started" |

---

### 14. Tournament Tables - Get List (`GET /api/tournaments/{id}/tables`)

| ID | Category | Scenario Name | Pre-Condition (Arrange) | Action (Act) | Critical Verification (Assert) |
|:---|:---------|:--------------|:------------------------|:-------------|:------------------------------|
| TM-TABLE-GET-01 | ✅ Positive | GetTables_ReturnsList | Tournament with tables exists | GET tables | Returns 200 OK, List of `TournamentTableDto` ordered by Label |

---

## 📊 Coverage Matrix

| Logic Path | Covered By |
|:-----------|:-----------|
| Tournament Creation (Single Stage) | TM-CREATE-01 |
| Tournament Creation (Multi Stage) | TM-CREATE-02, TM-CREATE-03, TM-CREATE-04 |
| Multi-Stage Validation (Power of 2) | TM-CREATE-03 |
| Multi-Stage Validation (No Single Elim) | TM-CREATE-04 |
| Tournament Detail Retrieval | TM-DETAIL-01, TM-DETAIL-02 |
| Public Tournament Listing | TM-LIST-01, TM-LIST-02 |
| Owner Tournament Listing | TM-MY-01 |
| Tournament Update (Basic Fields) | TM-UPDATE-01 |
| Tournament Update (Bracket Settings) | TM-UPDATE-02 |
| Bracket Size vs Player Count Validation | TM-UPDATE-03 |
| CanEditBracket Guard | TM-UPDATE-04 |
| Flyer Upload with Cloudinary | TM-FLYER-01 |
| Tournament Start Flow | TM-START-01, TM-START-02 |
| Tournament Deletion Rules | TM-DELETE-01, TM-DELETE-02, TM-DELETE-03 |
| Cascade Delete (Matches) | TM-DELETE-01 |
| Owner Authorization | TM-UPDATE-05, TM-FLYER-02, TM-START-03, TM-DELETE-04, TM-TABLE-02 |
| Payout Template Retrieval | TM-PAYOUT-01 |
| TotalPrize Calculation | TM-CREATE-01 |
| Table Management CRUD | TM-TABLE-01, TM-TABLE-BULK-01, TM-TABLE-UPD-01, TM-TABLE-DEL-01 |
| Table Delete Status Guard | TM-TABLE-DEL-02 |

---

## 🎯 Test Priority

### P0 - Critical (Must Pass)
- **TM-CREATE-01**: Core tournament creation
- **TM-CREATE-05**: Security - auth required
- **TM-START-01**: Tournament lifecycle start
- **TM-DELETE-01**: Tournament deletion with cascade
- **TM-DELETE-03**: Deletion status guard
- **TM-UPDATE-05**: Owner authorization

### P1 - High (Business Critical)
- **TM-CREATE-02, TM-CREATE-03, TM-CREATE-04**: Multi-stage validation
- **TM-UPDATE-01, TM-UPDATE-02**: Update flows
- **TM-UPDATE-03**: Bracket size validation
- **TM-DETAIL-01**: Detail retrieval
- **TM-TABLE-01, TM-TABLE-BULK-01**: Table management

### P2 - Medium (Validation & Edge Cases)
- **TM-UPDATE-04**: CanEditBracket behavior
- **TM-START-02**: Idempotent start
- **TM-DELETE-02**: Completed deletion
- **TM-FLYER-01**: Flyer upload
- **TM-TABLE-DEL-02**: Table status guard
- **TM-TABLE-BULK-02**: Bulk limit validation

### P3 - Low (Read Operations & Auxiliary)
- **TM-LIST-01, TM-LIST-02**: Public listing
- **TM-MY-01**: Owner listing
- **TM-DETAIL-02**: Not found
- **TM-PAYOUT-01**: Template retrieval
- **TM-TABLE-GET-01, TM-TABLE-UPD-01**: Table read/update

---

## 🔄 State Machine Tests

### Tournament Status Transitions

```
┌──────────────┐     Start()      ┌──────────────┐    CompleteStage()   ┌──────────────┐
│   Upcoming   │ ───────────────► │  InProgress  │ ────────────────────► │  Completed   │
└──────────────┘                  └──────────────┘                       └──────────────┘
     │                                  │                                      │
     │ Delete ✅                        │ Delete ❌                            │ Delete ✅
     ▼                                  ▼                                      ▼
   Deleted                           BLOCKED                               Deleted
```

### Allowed Operations by Status

| Operation | Upcoming | InProgress | Completed |
|:----------|:---------|:-----------|:----------|
| Update Basic Fields | ✅ | ✅ | ✅ |
| Update Bracket Settings | ✅ | ❌ | ❌ |
| Add/Remove Players | ✅ | ❌ | ❌ |
| Add/Remove Tables | ✅ | ❌ | ❌ |
| Start Tournament | ✅ | ✅ (no-op) | ❌ |
| Delete Tournament | ✅ | ❌ | ✅ |

---

## 🔧 Test Implementation Notes

### Required Test Infrastructure
```csharp
// 1. WebApplicationFactory with test database
// 2. Mock ICloudinaryService for flyer tests
// 3. Helper methods:
//    - CreateUserAsync(email, role)
//    - CreateTournamentAsync(ownerId, status, settings)
//    - GetAuthenticatedClient(user)
```

### Key Test Data Setup
```csharp
// Organizer User
var organizer = await CreateUserAsync("organizer@test.com", UserRoles.ORGANIZER);

// Single Stage Tournament
var tournament = new Tournament
{
    Name = "Test Tournament",
    OwnerUserId = organizer.Id,
    Status = TournamentStatus.Upcoming,
    IsStarted = false,
    BracketSizeEstimate = 16,
    BracketType = BracketType.DoubleElimination,
    PayoutMode = PayoutMode.Template,
    EntryFee = 100,
    AdminFee = 10,
    AddedMoney = 500
};

// Multi-Stage Tournament
var multiStageTournament = new Tournament
{
    ...tournament,
    IsMultiStage = true,
    AdvanceToStage2Count = 8,
    Stage1Ordering = BracketOrdering.Random,
    Stage2Ordering = BracketOrdering.Seeded
};

// Started Tournament
var startedTournament = new Tournament
{
    ...tournament,
    IsStarted = true,
    Status = TournamentStatus.InProgress
};
```

### TotalPrize Calculation Verification
```csharp
// PayoutMode.Template formula:
// TotalPrize = (Players * EntryFee) + AddedMoney - (Players * AdminFee)
// Example: (16 * 100) + 500 - (16 * 10) = 1600 + 500 - 160 = 1940

var expectedPrize = (bracketSize * entryFee) + addedMoney - (bracketSize * adminFee);
tournamentInDb.TotalPrize.Should().Be(expectedPrize);
```

### Assertions Patterns
```csharp
// Create Success
response.StatusCode.Should().Be(HttpStatusCode.OK);
var result = await response.Content.ReadFromJsonAsync<CreateResponse>();
result.Id.Should().BeGreaterThan(0);

var dbTournament = await dbContext.Tournaments.FindAsync(result.Id);
dbTournament.Status.Should().Be(TournamentStatus.Upcoming);
dbTournament.IsStarted.Should().BeFalse();

// Owner Authorization Failure
response.StatusCode.Should().Be(HttpStatusCode.Forbidden);

// Business Rule Violation
response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
var error = await response.Content.ReadFromJsonAsync<ErrorResponse>();
error.Message.Should().Contain("expected error text");

// Cascade Delete Verification
await dbContext.Tournaments.FindAsync(id).Should().BeNull();
await dbContext.Matches.Where(m => m.TournamentId == id).CountAsync().Should().Be(0);
```

---

## ✅ Checklist Sign-off

- [ ] All P0 tests implemented and passing
- [ ] All P1 tests implemented and passing  
- [ ] All P2 tests implemented and passing
- [ ] All P3 tests implemented and passing
- [ ] Code coverage > 80% for TournamentService core methods
- [ ] No flaky tests
- [ ] CI/CD pipeline integration verified

---

## 📝 Business Rules Summary

### Multi-Stage Tournament Rules
1. `AdvanceToStage2Count` must be a power of 2 (4, 8, 16, 32...)
2. `AdvanceToStage2Count` must be at least 4
3. `SingleElimination` cannot be used as Stage 1 type for multi-stage

### CanEditBracket Logic
```csharp
private static bool CanEditBracket(Tournament t)
    => !(t.IsStarted || t.Status == TournamentStatus.InProgress || t.Status == TournamentStatus.Completed);
```

### Deletion Rules
- Can delete: `Upcoming` OR `Completed` status
- Cannot delete: `InProgress` status
- Cascade: All related `Matches` are deleted
- Cleanup: Flyer image deleted from Cloudinary

### TotalPrize Calculation
- **Template Mode**: Auto-calculated from fees and bracket size
- **Custom Mode**: User-specified value (must be ≥ 0)

