# Tournament Player Module - Integration Test Checklist

> **Module:** TournamentsController (Player-related endpoints) & TournamentService  
> **Strategy:** Lean & Effective (80/20 Rule)  
> **Last Updated:** 2025-12-08

---

## 📋 Summary

| Endpoint | Method | Auth Required | Total Tests |
|:---------|:-------|:--------------|:------------|
| `POST /api/tournaments/{id}/players` | POST | ✅ Owner | 5 |
| `POST /api/tournaments/{id}/players/bulk-lines` | POST | ✅ Owner | 3 |
| `GET /api/tournaments/{id}/players` | GET | ❌ Anonymous | 1 |
| `PUT /api/tournaments/{id}/players/{tpId}` | PUT | ✅ Owner | 4 |
| `DELETE /api/tournaments/{id}/players` | DELETE | ✅ Owner | 3 |
| `POST /api/tournaments/{tournamentId}/players/{tpId}/link` | POST | ✅ Owner | 2 |
| `POST /api/tournaments/{tournamentId}/players/{tpId}/unlink` | POST | ✅ Owner | 1 |
| `POST /api/tournaments/{tournamentId}/players/{tpId}/create-profile` | POST | ✅ Owner | 2 |
| `GET /api/tournaments/players/search` | GET | ✅ Auth | 1 |
| `GET /api/tournaments/{id}/players/stats` | GET | ❌ Anonymous | 1 |
| **TOTAL** | | | **23** |

---

## 🧪 Test Cases

### 1. Add Tournament Player (`POST /api/tournaments/{id}/players`)

| ID | Category | Scenario Name | Pre-Condition (Arrange) | Action (Act) | Critical Verification (Assert) |
|:---|:---------|:--------------|:------------------------|:-------------|:------------------------------|
| TP-ADD-01 | ✅ Positive | AddPlayer_ValidData_Success | Auth as Tournament Owner, Tournament status=Upcoming | POST valid `AddTournamentPlayerModel` with DisplayName, optional PlayerId, Seed | Returns 200 OK with `id`, Player record created in DB with `Status=Confirmed` |
| TP-ADD-02 | ⚠️ Negative | AddPlayer_TournamentStarted_Fails | Auth as Owner, Tournament `IsStarted=true` OR `Status=InProgress` | POST valid player data | Returns 400 BadRequest, Message="Cannot add players after tournament has started or completed" |
| TP-ADD-03 | ⚠️ Negative | AddPlayer_TournamentFull_Fails | Auth as Owner, Tournament has `BracketSizeEstimate=8`, already 8 players | POST new player | Returns 400 BadRequest, Message contains "Tournament is full" |
| TP-ADD-04 | ⚠️ Negative | AddPlayer_DuplicateSeed_Fails | Auth as Owner, Player with Seed=1 already exists in tournament | POST player with same `Seed=1` | Returns 400 BadRequest, Message contains "Seed X is already assigned to player" |
| TP-ADD-05 | 🔒 Security | AddPlayer_NotOwner_Returns403 | Auth as different user (not tournament owner) | POST valid player data | Returns 403 Forbidden OR 404 NotFound (Message="Tournament not found or not owned by you") |

---

### 2. Bulk Add Players (`POST /api/tournaments/{id}/players/bulk-lines`)

| ID | Category | Scenario Name | Pre-Condition (Arrange) | Action (Act) | Critical Verification (Assert) |
|:---|:---------|:--------------|:------------------------|:-------------|:------------------------------|
| TP-BULK-01 | ✅ Positive | BulkAddPlayers_ValidLines_Success | Auth as Owner, Tournament status=Upcoming | POST `Lines="Player A\nPlayer B\nPlayer C"` | Returns 200 OK with `AddedCount=3`, All players created with `Status=Confirmed` |
| TP-BULK-02 | ⚠️ Negative | BulkAddPlayers_ExceedsCapacity_Fails | Auth as Owner, Tournament has 5 slots, sending 10 names | POST bulk lines exceeding capacity | Returns 400 BadRequest, Message contains "Cannot add X players. Tournament has Y available slots" |
| TP-BULK-03 | 💣 Edge | BulkAddPlayers_EmptyLinesSkipped_PartialSuccess | Auth as Owner | POST `Lines="Player A\n\n\nPlayer B"` (contains empty lines) | Returns 200 OK, `AddedCount=2`, `Skipped` contains entries with reason="Empty line" |

---

### 3. Get Tournament Players (`GET /api/tournaments/{id}/players`)

| ID | Category | Scenario Name | Pre-Condition (Arrange) | Action (Act) | Critical Verification (Assert) |
|:---|:---------|:--------------|:------------------------|:-------------|:------------------------------|
| TP-GET-01 | ✅ Positive | GetPlayers_ReturnsOrderedList | Tournament exists with players (seeded and unseeded) | GET without search filter | Returns 200 OK, Players ordered by `Seed` (nulls last), then by `DisplayName` |

---

### 4. Update Tournament Player (`PUT /api/tournaments/{id}/players/{tpId}`)

| ID | Category | Scenario Name | Pre-Condition (Arrange) | Action (Act) | Critical Verification (Assert) |
|:---|:---------|:--------------|:------------------------|:-------------|:------------------------------|
| TP-UPD-01 | ✅ Positive | UpdatePlayer_AllFields_Success | Auth as Owner, Tournament status=Upcoming, Player exists | PUT valid `UpdateTournamentPlayerModel` (DisplayName, Seed, Status, etc.) | Returns 200 OK, DB record updated with all new values |
| TP-UPD-02 | ⚠️ Negative | UpdatePlayer_TournamentStarted_Fails | Auth as Owner, Tournament `Status=InProgress` | PUT any update | Returns 400 BadRequest, Message="Cannot modify players after tournament has started or completed" |
| TP-UPD-03 | ⚠️ Negative | UpdatePlayer_InvalidPhone_Fails | Auth as Owner, Tournament status=Upcoming | PUT with `Phone="abc123"` (invalid format) | Returns 400 BadRequest, Message="Invalid phone number. Must be 10-15 digits, optional leading '+'" |
| TP-UPD-04 | 🔒 Security | UpdatePlayer_NotOwner_Returns404 | Auth as different user | PUT any data | Returns 404 NotFound (Message="Tournament player not found or you don't have permission") |

---

### 5. Delete Tournament Players (`DELETE /api/tournaments/{id}/players`)

| ID | Category | Scenario Name | Pre-Condition (Arrange) | Action (Act) | Critical Verification (Assert) |
|:---|:---------|:--------------|:------------------------|:-------------|:------------------------------|
| TP-DEL-01 | ✅ Positive | DeletePlayers_ValidIds_Success | Auth as Owner, Tournament status=Upcoming, Players exist | DELETE with `PlayerIds=[1,2,3]` | Returns 200 OK, `DeletedCount=3`, Records removed from DB |
| TP-DEL-02 | ⚠️ Negative | DeletePlayers_TournamentStarted_Fails | Auth as Owner, Tournament `Status=InProgress` | DELETE any player IDs | Returns 400 BadRequest, Message="Cannot delete players after tournament has started or completed" |
| TP-DEL-03 | 💣 Edge | DeletePlayers_MixedValidInvalid_PartialSuccess | Auth as Owner, Tournament status=Upcoming | DELETE with `PlayerIds=[existingId, nonExistentId]` | Returns 200 OK, `DeletedCount=1`, `Failed` array contains entry for non-existent ID with reason |

---

### 6. Link Player Profile (`POST /api/tournaments/{tournamentId}/players/{tpId}/link`)

| ID | Category | Scenario Name | Pre-Condition (Arrange) | Action (Act) | Critical Verification (Assert) |
|:---|:---------|:--------------|:------------------------|:-------------|:------------------------------|
| TP-LINK-01 | ✅ Positive | LinkPlayer_ValidPlayerId_Success | Auth as Owner, TournamentPlayer exists, Player profile exists | POST `LinkPlayerRequest` with `PlayerId` and `OverwriteSnapshot=true` | Returns 200 OK, `tp.PlayerId` set, Snapshot fields overwritten from Player |
| TP-LINK-02 | ⚠️ Negative | LinkPlayer_PlayerNotFound_Fails | Auth as Owner, TournamentPlayer exists, Invalid PlayerId | POST with non-existent `PlayerId` | Returns 400 BadRequest, Message="Link failed" |

---

### 7. Unlink Player Profile (`POST /api/tournaments/{tournamentId}/players/{tpId}/unlink`)

| ID | Category | Scenario Name | Pre-Condition (Arrange) | Action (Act) | Critical Verification (Assert) |
|:---|:---------|:--------------|:------------------------|:-------------|:------------------------------|
| TP-UNLINK-01 | ✅ Positive | UnlinkPlayer_Success | Auth as Owner, TournamentPlayer has linked PlayerId | POST to unlink endpoint | Returns 200 OK, `tp.PlayerId=null` in DB, Snapshot data preserved |

---

### 8. Create Profile from Snapshot (`POST /api/tournaments/{tournamentId}/players/{tpId}/create-profile`)

| ID | Category | Scenario Name | Pre-Condition (Arrange) | Action (Act) | Critical Verification (Assert) |
|:---|:---------|:--------------|:------------------------|:-------------|:------------------------------|
| TP-CREATE-01 | ✅ Positive | CreateProfileFromSnapshot_Success | Auth as Owner, TournamentPlayer has no PlayerId, has DisplayName/Email/etc | POST `CreateProfileFromSnapshotRequest` | Returns 200 OK with `playerId`, New Player record created with unique Slug, TournamentPlayer linked to new Player |
| TP-CREATE-02 | 💣 Edge | CreateProfileFromSnapshot_SlugConflict_GeneratesUnique | Auth as Owner, Player with same slug already exists | POST create profile | Returns 200 OK, New Player created with slug suffix (e.g., "john-doe-1") |

---

### 9. Search Players (`GET /api/tournaments/players/search`)

| ID | Category | Scenario Name | Pre-Condition (Arrange) | Action (Act) | Critical Verification (Assert) |
|:---|:---------|:--------------|:------------------------|:-------------|:------------------------------|
| TP-SEARCH-01 | ✅ Positive | SearchPlayers_ByName_ReturnsMatches | Auth required, Players exist in DB | GET with `q=john&limit=10` | Returns 200 OK, Array of `PlayerSearchItemDto` matching query, ordered by FullName |

---

### 10. Get Player Stats (`GET /api/tournaments/{id}/players/stats`)

| ID | Category | Scenario Name | Pre-Condition (Arrange) | Action (Act) | Critical Verification (Assert) |
|:---|:---------|:--------------|:------------------------|:-------------|:------------------------------|
| TP-STATS-01 | ✅ Positive | GetPlayerStats_ReturnsMatchStatistics | Tournament with completed matches exists | GET stats endpoint | Returns 200 OK, Array of `TournamentPlayerStatsDto` with wins/losses/points |

---

## 📊 Coverage Matrix

| Logic Path | Covered By |
|:-----------|:-----------|
| Add Player to Tournament | TP-ADD-01, TP-ADD-02, TP-ADD-03, TP-ADD-04, TP-ADD-05 |
| Bulk Add Players | TP-BULK-01, TP-BULK-02, TP-BULK-03 |
| Tournament Status Guard (CanEditBracket) | TP-ADD-02, TP-UPD-02, TP-DEL-02 |
| Bracket Size Limit Enforcement | TP-ADD-03, TP-BULK-02 |
| Seed Uniqueness Validation | TP-ADD-04 |
| Owner Authorization Check | TP-ADD-05, TP-UPD-04 |
| Phone Number Validation (Regex) | TP-UPD-03 |
| Player Profile Linking | TP-LINK-01, TP-LINK-02 |
| Player Profile Creation from Snapshot | TP-CREATE-01, TP-CREATE-02 |
| Slug Generation with Conflict Resolution | TP-CREATE-02 |
| Partial Success with Error Tracking | TP-BULK-03, TP-DEL-03 |

---

## 🎯 Test Priority

### P0 - Critical (Must Pass)
- **TP-ADD-01**: Core player addition flow
- **TP-ADD-02**: Tournament status guard - prevents data corruption
- **TP-ADD-05**: Security - owner authorization
- **TP-DEL-01**: Player removal flow

### P1 - High (Business Critical)
- **TP-BULK-01**: Bulk add for efficient tournament setup
- **TP-UPD-01**: Player update flow
- **TP-ADD-03**: Bracket size enforcement
- **TP-ADD-04**: Seed uniqueness (bracket integrity)
- **TP-LINK-01**: Player profile linking

### P2 - Medium (Validation)
- **TP-UPD-02, TP-DEL-02**: Status guards on update/delete
- **TP-UPD-03**: Phone validation
- **TP-UPD-04**: Update authorization
- **TP-BULK-02**: Capacity check on bulk add
- **TP-LINK-02**: Link failure handling

### P3 - Low (Edge Cases & Auxiliary)
- **TP-BULK-03**: Partial success handling
- **TP-DEL-03**: Mixed valid/invalid IDs
- **TP-CREATE-01, TP-CREATE-02**: Profile creation from snapshot
- **TP-UNLINK-01**: Unlink flow
- **TP-GET-01**: Read operations
- **TP-SEARCH-01, TP-STATS-01**: Search and stats

---

## 🔧 Test Implementation Notes

### Required Test Infrastructure
```csharp
// 1. WebApplicationFactory with InMemory/TestContainer DB
// 2. Helper methods for:
//    - Creating tournament with specific status
//    - Creating users (owner vs non-owner)
//    - Adding players to tournament
//    - Generating valid JWT tokens
```

### Key Test Data Setup
```csharp
// Tournament Owner Setup
var owner = await CreateUserAsync("organizer@test.com");
var tournament = await CreateTournamentAsync(owner.Id, status: TournamentStatus.Upcoming);

// Tournament with Players at Capacity
var fullTournament = await CreateTournamentAsync(owner.Id, bracketSize: 8);
await AddPlayersAsync(fullTournament.Id, count: 8);

// Started Tournament (for status guard tests)
var startedTournament = await CreateTournamentAsync(owner.Id, isStarted: true, status: TournamentStatus.InProgress);

// Player with Seed
var seededPlayer = await AddPlayerAsync(tournamentId, displayName: "Player 1", seed: 1);
```

### Assertions Patterns
```csharp
// Success Response with ID
response.StatusCode.Should().Be(HttpStatusCode.OK);
var content = await response.Content.ReadFromJsonAsync<JsonElement>();
content.GetProperty("id").GetInt32().Should().BeGreaterThan(0);

// BadRequest with Message
response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
var error = await response.Content.ReadFromJsonAsync<ErrorResponse>();
error.Message.Should().Contain("expected error text");

// DB State Verification
var playerInDb = await dbContext.TournamentPlayers.FindAsync(playerId);
playerInDb.Should().NotBeNull();
playerInDb.Status.Should().Be(TournamentPlayerStatus.Confirmed);

// Bulk Operation Result
var result = await response.Content.ReadFromJsonAsync<BulkAddPlayersResult>();
result.AddedCount.Should().Be(expectedCount);
result.Skipped.Should().HaveCount(expectedSkipped);
```

### Phone Validation Test Data
```csharp
// Valid Phones
"+84123456789"    // With country code
"0912345678"      // 10 digits
"123456789012345" // 15 digits max

// Invalid Phones (should fail)
"abc123"          // Letters
"123"             // Too short (< 10)
"1234567890123456" // Too long (> 15)
"   "             // Whitespace only
```

---

## 🔄 State Transition Tests

### Tournament Status vs Allowed Operations

| Tournament Status | Add Player | Update Player | Delete Player |
|:------------------|:-----------|:--------------|:--------------|
| `Upcoming` | ✅ Allowed | ✅ Allowed | ✅ Allowed |
| `InProgress` | ❌ Blocked | ❌ Blocked | ❌ Blocked |
| `Completed` | ❌ Blocked | ❌ Blocked | ❌ Blocked |

```csharp
// CanEditBracket logic (from TournamentService)
private static bool CanEditBracket(Tournament t)
    => !(t.IsStarted || t.Status == TournamentStatus.InProgress || t.Status == TournamentStatus.Completed);
```

---

## ✅ Checklist Sign-off

- [ ] All P0 tests implemented and passing
- [ ] All P1 tests implemented and passing  
- [ ] All P2 tests implemented and passing
- [ ] All P3 tests implemented and passing
- [ ] Code coverage > 80% for TournamentPlayer-related methods
- [ ] No flaky tests
- [ ] CI/CD pipeline integration verified

---

## 📝 Additional Notes

### Business Rules Validated
1. **Seed Uniqueness**: No two players in same tournament can have the same seed number
2. **Bracket Size Limit**: Cannot exceed `BracketSizeEstimate` when adding players
3. **Status Guard**: Player modifications locked after tournament starts
4. **Owner Authorization**: Only tournament owner can manage players
5. **Phone Format**: Must match regex `^\+?\d{10,15}$`

### Integration Points
- Links to `Player` entity for profile management
- Affects `BracketService` for bracket generation
- `TournamentPlayer.Status` affects bracket participation

