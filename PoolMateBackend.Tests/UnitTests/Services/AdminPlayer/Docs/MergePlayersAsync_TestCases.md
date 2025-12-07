# Test Cases cho `MergePlayersAsync` - AdminPlayerService

## Thông tin hàm

**File:** `PoolMateBackend/Services/AdminPlayerService.cs`

**Signature:**
```csharp
public async Task<Response> MergePlayersAsync(
    MergePlayerRequestDto request, 
    CancellationToken ct = default)
```

---

## Phân tích Control Flow

Hàm `MergePlayersAsync` là một hàm phức tạp với transaction management và nhiều nhánh validation:

```
┌─────────────────────────────────────────────────────────────┐
│                     MergePlayersAsync                        │
└─────────────────────────────────────────────────────────────┘
                              │
                              ▼
              ┌───────────────────────────────┐
              │ SourcePlayerIds == null ||    │
              │ !SourcePlayerIds.Any() ?      │
              └───────────────────────────────┘
                    │YES              │NO
                    ▼                 ▼
          ┌──────────────────┐  ┌─────────────────────────────┐
          │ Return Error:    │  │ SourcePlayerIds.Contains    │
          │ "No source       │  │ (TargetPlayerId) ?          │
          │ players..."      │  └─────────────────────────────┘
          └──────────────────┘        │YES        │NO
                                      ▼           ▼
                          ┌──────────────────┐  ┌─────────────┐
                          │ Return Error:    │  │ BEGIN       │
                          │ "Target cannot   │  │ TRANSACTION │
                          │ be in source..." │  └─────────────┘
                          └──────────────────┘        │
                                                      ▼
                          ┌───────────────────────────────────┐
                          │ TRY BLOCK                         │
                          └───────────────────────────────────┘
                                      │
                                      ▼
                          ┌───────────────────────────────────┐
                          │ targetPlayer = Find by            │
                          │ TargetPlayerId                    │
                          └───────────────────────────────────┘
                                      │
                              ┌───────┴───────┐
                              │NULL           │NOT NULL
                              ▼               ▼
                    ┌──────────────────┐  ┌─────────────────┐
                    │ Return Error:    │  │ Find source     │
                    │ "Target not      │  │ players         │
                    │ found..."        │  └─────────────────┘
                    └──────────────────┘        │
                                                ▼
                          ┌───────────────────────────────────┐
                          │ sourcePlayers.Count !=            │
                          │ SourcePlayerIds.Count ?           │
                          └───────────────────────────────────┘
                                │YES              │NO
                                ▼                 ▼
                    ┌──────────────────┐  ┌─────────────────────┐
                    │ Return Error:    │  │ Get source history  │
                    │ "One or more     │  │ Get target tourney  │
                    │ not found..."    │  │ IDs                 │
                    └──────────────────┘  └─────────────────────┘
                                                  │
                                                  ▼
                          ┌───────────────────────────────────┐
                          │ foreach (record in sourceHistory) │
                          └───────────────────────────────────┘
                                      │
                    ┌─────────────────┴─────────────────┐
                    │                                   │
            targetTournamentIdSet               targetTournamentIdSet
            .Contains(record.TournamentId)?     NOT Contains
                    │YES                               │
                    ▼                                  ▼
          ┌──────────────────┐              ┌──────────────────┐
          │ continue (skip)  │              │ record.PlayerId  │
          │ DON'T transfer   │              │ = targetPlayer.Id│
          └──────────────────┘              │ movedCount++     │
                    │                       └──────────────────┘
                    └─────────────────┬─────────────────┘
                                      │
                                      ▼
              ┌───────────────────────────────────────────────┐
              │ targetPlayer.UserId == null ?                 │
              └───────────────────────────────────────────────┘
                    │YES                            │NO
                    ▼                               ▼
      ┌─────────────────────────┐    ┌─────────────────────────────┐
      │ sourceWithUser =        │    │ sourcePlayers.Any(p =>      │
      │ sourcePlayers.First     │    │   p.UserId != null &&       │
      │ (p => p.UserId != null) │    │   p.UserId != target.UserId)│
      └─────────────────────────┘    └─────────────────────────────┘
                │                           │YES            │NO
        ┌───────┴───────┐                   ▼               ▼
        │NULL          │NOT NULL   ┌──────────────┐  ┌────────────┐
        ▼               ▼          │ ROLLBACK     │  │ Continue   │
   No action    Transfer UserId    │ Return Error │  │ to delete  │
                                   │ "Cannot      │  └────────────┘
                                   │ merge..."    │        │
                                   └──────────────┘        │
                    │                                      │
                    └──────────────────┬───────────────────┘
                                       │
                                       ▼
                          ┌───────────────────────────────┐
                          │ _db.Players.RemoveRange       │
                          │ SaveChangesAsync              │
                          │ COMMIT TRANSACTION            │
                          └───────────────────────────────┘
                                       │
                                       ▼
                          ┌───────────────────────────────┐
                          │ Return Response.Ok(...)       │
                          └───────────────────────────────┘

                          ┌───────────────────────────────┐
                          │ CATCH BLOCK                   │
                          │ - Rollback Transaction        │
                          │ - Return Error with message   │
                          └───────────────────────────────┘
```

---

## Boundary Analysis

### 1. SourcePlayerIds Collection

| Scenario | Value | Result |
|:---------|:------|:-------|
| Null | `null` | Error: "No source players provided." |
| Empty | `[]` | Error: "No source players provided." |
| Has items | `[1, 2, 3]` | Continue processing |

### 2. Target in Source List

| Scenario | SourcePlayerIds | TargetPlayerId | Result |
|:---------|:----------------|:---------------|:-------|
| Target IN source | `[1, 2, 3]` | `2` | Error: "Target cannot be in source list." |
| Target NOT in source | `[1, 2, 3]` | `5` | Continue processing |

### 3. Tournament Conflict Detection

| Scenario | Source Tournament IDs | Target Tournament IDs | Transferred |
|:---------|:----------------------|:----------------------|:------------|
| No overlap | `[1, 2]` | `[3, 4]` | All (2 records) |
| Full overlap | `[1, 2]` | `[1, 2]` | None (0 records) |
| Partial overlap | `[1, 2, 3]` | `[2]` | 2 records (skip #2) |

### 4. User Link Scenarios

| Target.UserId | Source.UserId | Action |
|:--------------|:--------------|:-------|
| `null` | `null` | No change |
| `null` | `"user-A"` | Transfer to Target |
| `"user-A"` | `null` | No change |
| `"user-A"` | `"user-A"` | No change (same user) |
| `"user-A"` | `"user-B"` | ❌ Error + Rollback |

---

## Bảng Test Cases

| ID | Loại | Tên Test Case (Gợi ý đặt tên) | Input Giả định (Mock Data) | Kết quả Mong đợi | Ghi chú |
|:---|:-----|:------------------------------|:---------------------------|:-----------------|:--------|
| 1 | Error | MergePlayersAsync_WhenSourcePlayerIdsIsNull_ReturnsError | `request.SourcePlayerIds = null` | `Response.Error("No source players provided.")` | [Fact] |
| 2 | Error | MergePlayersAsync_WhenSourcePlayerIdsIsEmpty_ReturnsError | `request.SourcePlayerIds = []` (empty list) | `Response.Error("No source players provided.")` | [Fact] |
| 3 | Error | MergePlayersAsync_WhenTargetPlayerInSourceList_ReturnsError | `SourcePlayerIds = [1, 2, 3]`, `TargetPlayerId = 2` | `Response.Error("Target player cannot be in the source list.")` | [Fact] |
| 4 | Error | MergePlayersAsync_WhenTargetPlayerNotFound_ReturnsError | `TargetPlayerId = 999` (không tồn tại trong DB) | `Response.Error("Target player (ID: 999) not found.")` | [Fact] |
| 5 | Error | MergePlayersAsync_WhenSomeSourcePlayersNotFound_ReturnsError | `SourcePlayerIds = [1, 2, 999]`, DB chỉ có player 1 và 2 | `Response.Error("One or more source players not found.")` | [Fact] |
| 6 | Happy | MergePlayersAsync_WhenValidRequest_MergesSuccessfully | Target (ID=5) và Sources (ID=1,2) đều tồn tại, không có conflict | `Response.Ok()` với `MergedCount = 2`, `TargetPlayerId = 5` | [Fact] |
| 7 | Happy | MergePlayersAsync_WhenSourceHasHistoryNotInTarget_TransfersAllHistory | Source có 3 tournament records, Target có 0 | `MovedTournamentRecords = 3`, tất cả records chuyển sang Target | [Fact] |
| 8 | Edge | MergePlayersAsync_WhenSourceAndTargetShareTournament_SkipsConflictingRecords | Source có TournamentId = [1,2,3], Target đã có TournamentId = [2] | `MovedTournamentRecords = 2` (skip tournament 2), tournament 2 không bị duplicate | [Fact] |
| 9 | Edge | MergePlayersAsync_WhenAllTournamentsOverlap_TransfersZeroRecords | Source và Target đều có TournamentId = [1, 2] | `MovedTournamentRecords = 0`, merge vẫn thành công | [Fact] |
| 10 | Happy | MergePlayersAsync_WhenTargetHasNoUserAndSourceHasUser_TransfersUserId | `targetPlayer.UserId = null`, `sourcePlayer.UserId = "user-123"` | `targetPlayer.UserId` được set thành `"user-123"`, source's UserId set null | [Fact] |
| 11 | Edge | MergePlayersAsync_WhenTargetHasNoUserAndNoSourceHasUser_NoUserIdChange | `targetPlayer.UserId = null`, tất cả sources cũng `UserId = null` | Không có thay đổi về UserId | [Fact] |
| 12 | Error | MergePlayersAsync_WhenTargetHasUserAndSourceHasDifferentUser_ReturnsErrorAndRollbacks | `targetPlayer.UserId = "user-A"`, `sourcePlayer.UserId = "user-B"` | `Response.Error("Cannot merge: One of the source players belongs to a different User account...")`, transaction rolled back | [Fact] |
| 13 | Happy | MergePlayersAsync_WhenTargetHasUserAndSourceHasSameUser_MergesSuccessfully | `targetPlayer.UserId = "user-A"`, `sourcePlayer.UserId = "user-A"` | Merge thành công, không có user conflict | [Fact] |
| 14 | Happy | MergePlayersAsync_WhenTargetHasUserAndAllSourcesHaveNoUser_MergesSuccessfully | `targetPlayer.UserId = "user-A"`, tất cả sources có `UserId = null` | Merge thành công | [Fact] |
| 15 | Happy | MergePlayersAsync_DeletesAllSourcePlayersAfterMerge | 3 source players được merge | Verify `_db.Players.RemoveRange()` được gọi với 3 players, tất cả bị xóa | [Fact] |
| 16 | Happy | MergePlayersAsync_CommitsTransactionOnSuccess | Valid merge request | Verify `transaction.CommitAsync()` được gọi | [Fact] |
| 17 | Error | MergePlayersAsync_WhenExceptionThrown_RollbacksAndReturnsError | `_db.SaveChangesAsync()` throws exception | `Response.Error("Merge failed: ...")`, verify `transaction.RollbackAsync()` được gọi | [Fact] |
| 18 | Edge | MergePlayersAsync_WhenMultipleSourcesWithOneHavingUser_TransfersFirstUserFound | Sources: [P1: UserId=null, P2: UserId="user-X", P3: UserId=null], Target: UserId=null | Target.UserId = "user-X" (từ P2), P2.UserId = null | [Fact] |
| 19 | Happy | MergePlayersAsync_WhenSourcesHaveNoTournamentHistory_MergesWithZeroMoved | Source players không có TournamentPlayer records nào | `MovedTournamentRecords = 0`, merge vẫn thành công | [Fact] |
| 20 | Happy | MergePlayersAsync_ReturnsCorrectMergeStatistics | Merge 3 sources với 5 tournament records transferable | Response chứa đúng: `MergedCount = 3`, `MovedTournamentRecords = 5`, `TargetPlayerName`, `TargetPlayerId` | [Fact] |

---

## Chi tiết Mock Setup cho từng Test Case

### Test Case #1 & #2: Null/Empty source validation
```csharp
[Fact]
public async Task MergePlayersAsync_WhenSourcePlayerIdsIsNull_ReturnsError()
{
    // Arrange
    var request = new MergePlayerRequestDto
    {
        SourcePlayerIds = null,
        TargetPlayerId = 1
    };

    // Act
    var result = await _adminPlayerService.MergePlayersAsync(request);

    // Assert
    Assert.False(result.IsSuccess);
    Assert.Equal("No source players provided.", result.Message);
    
    // Verify no DB operations
    _mockDbContext.Verify(x => x.Database.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
}

[Fact]
public async Task MergePlayersAsync_WhenSourcePlayerIdsIsEmpty_ReturnsError()
{
    // Arrange
    var request = new MergePlayerRequestDto
    {
        SourcePlayerIds = new List<int>(), // Empty
        TargetPlayerId = 1
    };

    // Act
    var result = await _adminPlayerService.MergePlayersAsync(request);

    // Assert
    Assert.False(result.IsSuccess);
    Assert.Equal("No source players provided.", result.Message);
}
```

### Test Case #8: Partial tournament overlap
```csharp
[Fact]
public async Task MergePlayersAsync_WhenSourceAndTargetShareTournament_SkipsConflictingRecords()
{
    // Arrange
    var targetPlayer = new Player { Id = 10, FullName = "Target Player", UserId = null };
    var sourcePlayer = new Player { Id = 1, FullName = "Source Player", UserId = null };
    
    // Source has tournaments 1, 2, 3
    var sourceHistory = new List<TournamentPlayer>
    {
        new() { PlayerId = 1, TournamentId = 1 },
        new() { PlayerId = 1, TournamentId = 2 }, // CONFLICT
        new() { PlayerId = 1, TournamentId = 3 }
    };
    
    // Target already has tournament 2
    var targetTournaments = new List<TournamentPlayer>
    {
        new() { PlayerId = 10, TournamentId = 2 } // CONFLICT
    };
    
    var request = new MergePlayerRequestDto
    {
        SourcePlayerIds = new List<int> { 1 },
        TargetPlayerId = 10
    };
    
    // Setup mocks...

    // Act
    var result = await _adminPlayerService.MergePlayersAsync(request);

    // Assert
    Assert.True(result.IsSuccess);
    var data = (dynamic)result.Data;
    Assert.Equal(2, data.MovedTournamentRecords); // Only 2 transferred, 1 skipped
}
```

### Test Case #12: User conflict - different users
```csharp
[Fact]
public async Task MergePlayersAsync_WhenTargetHasUserAndSourceHasDifferentUser_ReturnsErrorAndRollbacks()
{
    // Arrange
    var targetPlayer = new Player 
    { 
        Id = 10, 
        FullName = "Target", 
        UserId = "user-A" // Has user A
    };
    var sourcePlayer = new Player 
    { 
        Id = 1, 
        FullName = "Source", 
        UserId = "user-B" // Has DIFFERENT user B
    };
    
    var request = new MergePlayerRequestDto
    {
        SourcePlayerIds = new List<int> { 1 },
        TargetPlayerId = 10
    };
    
    // Setup mocks...

    // Act
    var result = await _adminPlayerService.MergePlayersAsync(request);

    // Assert
    Assert.False(result.IsSuccess);
    Assert.Contains("Cannot merge", result.Message);
    Assert.Contains("different User account", result.Message);
    
    // Verify rollback was called
    _mockTransaction.Verify(x => x.RollbackAsync(It.IsAny<CancellationToken>()), Times.Once);
}
```

### Test Case #17: Exception handling
```csharp
[Fact]
public async Task MergePlayersAsync_WhenExceptionThrown_RollbacksAndReturnsError()
{
    // Arrange
    var targetPlayer = new Player { Id = 10, FullName = "Target", UserId = null };
    var sourcePlayer = new Player { Id = 1, FullName = "Source", UserId = null };
    
    var request = new MergePlayerRequestDto
    {
        SourcePlayerIds = new List<int> { 1 },
        TargetPlayerId = 10
    };
    
    // Setup mocks - Make SaveChangesAsync throw
    _mockDbContext.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
        .ThrowsAsync(new Exception("Database connection lost"));

    // Act
    var result = await _adminPlayerService.MergePlayersAsync(request);

    // Assert
    Assert.False(result.IsSuccess);
    Assert.Contains("Merge failed:", result.Message);
    Assert.Contains("Database connection lost", result.Message);
    
    // Verify rollback was called
    _mockTransaction.Verify(x => x.RollbackAsync(It.IsAny<CancellationToken>()), Times.Once);
    
    // Verify commit was NOT called
    _mockTransaction.Verify(x => x.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
}
```

---

## Tổng kết Coverage

| Nhánh Code | Test Case IDs |
|:-----------|:--------------|
| `SourcePlayerIds == null` | #1 |
| `!SourcePlayerIds.Any()` | #2 |
| `SourcePlayerIds.Contains(TargetPlayerId)` | #3 |
| `targetPlayer == null` | #4 |
| `sourcePlayers.Count != SourcePlayerIds.Count` | #5 |
| `targetTournamentIdSet.Contains(record.TournamentId)` = true (skip) | #8, #9 |
| `targetTournamentIdSet.Contains(record.TournamentId)` = false (transfer) | #7, #8 |
| `targetPlayer.UserId == null` && `sourceWithUser != null` | #10, #18 |
| `targetPlayer.UserId == null` && `sourceWithUser == null` | #11 |
| `targetPlayer.UserId != null` && source has different user | #12 |
| `targetPlayer.UserId != null` && source has same user | #13 |
| `targetPlayer.UserId != null` && sources have no user | #14 |
| `_db.Players.RemoveRange()` called | #15 |
| `transaction.CommitAsync()` called | #16 |
| `catch` block - exception handling | #17 |
| No tournament history to transfer | #19 |
| Response statistics verification | #6, #20 |

---

## Thống kê

- **Tổng số Test Cases:** 20
- **Happy Path:** 10 (ID #6, #7, #10, #13, #14, #15, #16, #19, #20)
- **Error Cases:** 6 (ID #1, #2, #3, #4, #5, #12, #17)
- **Edge Cases:** 4 (ID #8, #9, #11, #18)

**Code Coverage dự kiến:** 100% cho hàm `MergePlayersAsync`

---

## ⚠️ Lưu ý về Logic

### 1. Transaction Safety
- Hàm sử dụng database transaction để đảm bảo atomicity
- Nếu bất kỳ bước nào fail, toàn bộ operation được rollback

### 2. Tournament Conflict Resolution
- Khi Source và Target cùng tham gia một tournament, record của Source sẽ bị **BỎ QUA**
- Record này sẽ có `PlayerId = NULL` khi Source bị xóa (do `OnDelete.SetNull`)

### 3. User Link Priority
- Chỉ transfer UserId khi Target chưa có User
- Sử dụng `FirstOrDefault` nên nếu nhiều sources có User, chỉ lấy cái đầu tiên

### 4. Cascade Delete Behavior
- Các TournamentPlayer records không được transfer sẽ có `PlayerId = NULL`
- Match data vẫn tồn tại nhưng không link tới Player nào

---

## Đề xuất cải thiện Code

1. **Null check cho request:**
   ```csharp
   ArgumentNullException.ThrowIfNull(request);
   ```

2. **Logging cho debugging:**
   ```csharp
   _logger.LogInformation("Merging {SourceCount} players into target {TargetId}", 
       request.SourcePlayerIds.Count, request.TargetPlayerId);
   ```

3. **Xử lý multiple sources with different users:**
   Hiện tại nếu nhiều source có cùng UserId (khác target), code chỉ check first. Có thể cần validate tất cả.

4. **Return detailed skip reasons:**
   ```csharp
   var skippedRecords = sourceHistory.Count - movedCount;
   // Include in response
   ```

