# Test Cases cho `GetPlayerStatsAsync` - PlayerProfileService

## Thông tin hàm

**File:** `PoolMateBackend/Services/PlayerProfileService.cs`

**Signature:**
```csharp
public async Task<PlayerStatsDto> GetPlayerStatsAsync(int playerId, CancellationToken ct = default)
```

---

## Phân tích Control Flow

Hàm `GetPlayerStatsAsync` có các nhánh điều kiện sau:

```
┌─────────────────────────────────────────────────────────────┐
│                    GetPlayerStatsAsync                       │
└─────────────────────────────────────────────────────────────┘
                              │
                              ▼
              ┌───────────────────────────────┐
              │ Query TournamentPlayers       │
              │ where PlayerId = playerId     │
              │ Select tp.Id -> tpIds         │
              └───────────────────────────────┘
                              │
                              ▼
              ┌───────────────────────────────┐
              │     tpIds.Count == 0 ?        │
              └───────────────────────────────┘
                    │YES              │NO
                    ▼                 ▼
              ┌──────────────┐  ┌─────────────────────────────┐
              │ return new   │  │ Query Matches where:        │
              │ PlayerStats  │  │ - Status = Completed        │
              │ Dto()        │  │ - Player1TpId OR Player2TpId│
              │ (all empty)  │  │   in tpIds                  │
              └──────────────┘  └─────────────────────────────┘
                                              │
                                              ▼
                                ┌─────────────────────────────┐
                                │ foreach (var m in matches)  │
                                └─────────────────────────────┘
                                              │
                    ┌─────────────────────────┴─────────────────────────┐
                    │                                                   │
                    ▼                                                   ▼
          ┌─────────────────────┐                         ┌─────────────────────┐
          │ isWin = WinnerTpId  │                         │ recentForm.Count < 5│
          │ in tpIds?           │                         │ ?                   │
          └─────────────────────┘                         └─────────────────────┘
                │                                               │YES     │NO
        ┌───────┴───────┐                                       ▼        ▼
        │YES           │NO                              Add to    Skip
        ▼              ▼                               recentForm
   totalWins++   totalLosses++
                                              │
                                              ▼
                                ┌─────────────────────────────┐
                                │ gameTypeStats[GameType]     │
                                │ exists?                     │
                                └─────────────────────────────┘
                                      │NO            │YES
                                      ▼              ▼
                                 Create new    Update existing
                                 entry         entry
                                              │
                                              ▼
                                ┌─────────────────────────────┐
                                │ totalMatches > 0 ?          │
                                └─────────────────────────────┘
                                    │YES            │NO
                                    ▼               ▼
                              Calculate        winRate = 0
                              winRate
                                              │
                                              ▼
                                ┌─────────────────────────────┐
                                │ Map gameTypeStats to DTOs   │
                                │ (with WinRate calculation)  │
                                └─────────────────────────────┘
                                              │
                                              ▼
                                       Return PlayerStatsDto
```

---

## Boundary Analysis

### 1. tpIds.Count (so sánh `== 0`)

| Vị trí | Số TournamentPlayers | Kết quả |
|:-------|:---------------------|:--------|
| Ngay biên | 0 | Return empty `PlayerStatsDto()` |
| Trên biên | 1+ | Tiếp tục query matches |

### 2. recentForm.Count (so sánh `< 5`)

| Vị trí | Số trận matches | RecentForm length | Kết quả |
|:-------|:----------------|:------------------|:--------|
| Dưới biên | 0 | 0 | RecentForm = [] |
| Trong biên | 3 | 3 | RecentForm = 3 items |
| Ngay biên | 5 | 5 | RecentForm = 5 items |
| Trên biên | 7+ | 5 | RecentForm capped at 5 |

### 3. totalMatches (so sánh `> 0`)

| Vị trí | Số trận | WinRate calculation |
|:-------|:--------|:--------------------|
| Ngay biên | 0 | WinRate = 0 (skip calculation) |
| Trên biên | 1+ | WinRate = (wins/total) * 100 |

### 4. totalGameMatches (so sánh `> 0` trong GameType)

| Vị trí | Số trận per GameType | GameType WinRate |
|:-------|:---------------------|:-----------------|
| Ngay biên | 0 | WinRate = 0 |
| Trên biên | 1+ | WinRate calculated |

---

## Bảng Test Cases

| ID | Loại | Tên Test Case (Gợi ý đặt tên) | Input Giả định (Mock Data) | Kết quả Mong đợi | Ghi chú |
|:---|:-----|:------------------------------|:---------------------------|:-----------------|:--------|
| 1 | Error | GetPlayerStatsAsync_WhenPlayerHasNoTournamentPlayers_ReturnsEmptyStats | `playerId = 999`, DB không có TournamentPlayer nào với PlayerId này | Return `PlayerStatsDto` với `TotalMatches = 0`, `TotalWins = 0`, `TotalLosses = 0`, `WinRate = 0`, `TotalTournaments = 0`, `RecentForm = []`, `StatsByGameType = []` | [Fact] |
| 2 | Happy | GetPlayerStatsAsync_WhenPlayerHasTournamentsButNoCompletedMatches_ReturnsEmptyMatchStats | Player có 2 TournamentPlayer nhưng không có Match nào với `Status = Completed` | `TotalTournaments = 2`, `TotalMatches = 0`, `WinRate = 0`, `RecentForm = []` | [Fact] |
| 3 | Happy | GetPlayerStatsAsync_WhenPlayerWinsMatch_CountsAsWin | Player có 1 match với `WinnerTpId = player's TpId` | `TotalWins = 1`, `TotalLosses = 0`, `RecentForm = ["W"]` | [Fact] |
| 4 | Happy | GetPlayerStatsAsync_WhenPlayerLosesMatch_CountsAsLoss | Player có 1 match với `WinnerTpId = opponent's TpId` (không trong tpIds) | `TotalWins = 0`, `TotalLosses = 1`, `RecentForm = ["L"]` | [Fact] |
| 5 | Edge | GetPlayerStatsAsync_WhenWinnerTpIdIsNull_CountsAsLoss | Match có `WinnerTpId = null` | `TotalLosses++` vì `isWin = false`, `RecentForm = ["L"]` | [Fact] |
| 6 | Boundary | GetPlayerStatsAsync_WhenPlayerHasExactly5Matches_RecentFormHas5Items | 5 trận completed với results W, L, W, L, W (theo thứ tự sort) | `RecentForm = ["W", "L", "W", "L", "W"]` với Count = 5 | [Fact] - Ngay biên |
| 7 | Boundary | GetPlayerStatsAsync_WhenPlayerHasMoreThan5Matches_RecentFormCappedAt5 | 7 trận completed | `RecentForm.Count = 5` (chỉ 5 trận đầu tiên sau sort) | [Fact] - Trên biên |
| 8 | Boundary | GetPlayerStatsAsync_WhenPlayerHasLessThan5Matches_RecentFormHasAllMatches | 3 trận completed | `RecentForm.Count = 3` | [Fact] - Dưới biên |
| 9 | Happy | GetPlayerStatsAsync_WhenPlayerHasMultipleGameTypes_GroupsStatsByGameType | 3 trận NineBall (2W, 1L), 2 trận EightBall (1W, 1L) | `StatsByGameType` có 2 entries: NineBall {Wins:2, Losses:1}, EightBall {Wins:1, Losses:1} | [Fact] |
| 10 | Happy | GetPlayerStatsAsync_CalculatesOverallWinRateCorrectly | 3 wins, 2 losses (5 total) | `WinRate = 60.0` (Math.Round(3/5 * 100, 1)) | [Fact] |
| 11 | Edge | GetPlayerStatsAsync_WhenAllWins_WinRateIs100 | 5 wins, 0 losses | `WinRate = 100.0` | [Fact] |
| 12 | Edge | GetPlayerStatsAsync_WhenAllLosses_WinRateIs0 | 0 wins, 5 losses | `WinRate = 0.0` | [Fact] |
| 13 | Happy | GetPlayerStatsAsync_CalculatesGameTypeWinRateCorrectly | NineBall: 2W, 2L (4 total) | NineBall `WinRate = 50.0` | [Fact] |
| 14 | Edge | GetPlayerStatsAsync_WhenPlayerIsPlayer1_MatchIsIncluded | Match có `Player1TpId = player's TpId`, `Player2TpId = other` | Match được query và đếm | [Fact] |
| 15 | Edge | GetPlayerStatsAsync_WhenPlayerIsPlayer2_MatchIsIncluded | Match có `Player1TpId = other`, `Player2TpId = player's TpId` | Match được query và đếm | [Fact] |
| 16 | Happy | GetPlayerStatsAsync_TotalTournamentsEqualsUniqueTpIdCount | Player có 3 TournamentPlayer entries (3 giải khác nhau) | `TotalTournaments = 3` | [Fact] |
| 17 | Edge | GetPlayerStatsAsync_MatchesSortedByTournamentDateThenScheduled | Matches từ Tournament A (Jan 2024) và Tournament B (Feb 2024) | RecentForm lấy từ Tournament B trước (mới nhất) | [Fact] |
| 18 | Edge | GetPlayerStatsAsync_OnlyCompletedMatchesAreCounted | 3 Completed, 2 InProgress, 1 Scheduled | `TotalMatches = 3` (chỉ Completed) | [Fact] |

---

## Chi tiết Mock Setup cho từng Test Case

### Test Case #1: No TournamentPlayers
```csharp
[Fact]
public async Task GetPlayerStatsAsync_WhenPlayerHasNoTournamentPlayers_ReturnsEmptyStats()
{
    // Arrange
    var playerId = 999;
    
    // Mock: Empty TournamentPlayers
    _mockDbContext.Setup(x => x.TournamentPlayers)
        .Returns(MockDbSet(new List<TournamentPlayer>()));

    // Act
    var result = await _playerProfileService.GetPlayerStatsAsync(playerId);

    // Assert
    Assert.NotNull(result);
    Assert.Equal(0, result.TotalMatches);
    Assert.Equal(0, result.TotalWins);
    Assert.Equal(0, result.TotalLosses);
    Assert.Equal(0, result.WinRate);
    Assert.Equal(0, result.TotalTournaments);
    Assert.Empty(result.RecentForm);
    Assert.Empty(result.StatsByGameType);
}
```

### Test Case #3: Player wins a match
```csharp
[Fact]
public async Task GetPlayerStatsAsync_WhenPlayerWinsMatch_CountsAsWin()
{
    // Arrange
    var playerId = 1;
    var playerTpId = 100;
    
    var tournamentPlayers = new List<TournamentPlayer>
    {
        new() { Id = playerTpId, PlayerId = playerId, TournamentId = 1 }
    };
    
    var matches = new List<Match>
    {
        new()
        {
            Id = 1,
            Status = MatchStatus.Completed,
            Player1TpId = playerTpId,
            Player2TpId = 200, // Opponent
            WinnerTpId = playerTpId, // Player wins!
            Tournament = new Tournament { StartUtc = DateTime.UtcNow, GameType = GameType.NineBall }
        }
    };
    
    _mockDbContext.Setup(x => x.TournamentPlayers).Returns(MockDbSet(tournamentPlayers));
    _mockDbContext.Setup(x => x.Matches).Returns(MockDbSet(matches));

    // Act
    var result = await _playerProfileService.GetPlayerStatsAsync(playerId);

    // Assert
    Assert.Equal(1, result.TotalWins);
    Assert.Equal(0, result.TotalLosses);
    Assert.Equal(100.0, result.WinRate);
    Assert.Single(result.RecentForm);
    Assert.Equal("W", result.RecentForm[0]);
}
```

### Test Case #5: WinnerTpId is null (edge case)
```csharp
[Fact]
public async Task GetPlayerStatsAsync_WhenWinnerTpIdIsNull_CountsAsLoss()
{
    // Arrange
    var playerId = 1;
    var playerTpId = 100;
    
    var tournamentPlayers = new List<TournamentPlayer>
    {
        new() { Id = playerTpId, PlayerId = playerId }
    };
    
    var matches = new List<Match>
    {
        new()
        {
            Id = 1,
            Status = MatchStatus.Completed,
            Player1TpId = playerTpId,
            Player2TpId = 200,
            WinnerTpId = null, // No winner recorded!
            Tournament = new Tournament { StartUtc = DateTime.UtcNow, GameType = GameType.NineBall }
        }
    };
    
    _mockDbContext.Setup(x => x.TournamentPlayers).Returns(MockDbSet(tournamentPlayers));
    _mockDbContext.Setup(x => x.Matches).Returns(MockDbSet(matches));

    // Act
    var result = await _playerProfileService.GetPlayerStatsAsync(playerId);

    // Assert
    Assert.Equal(0, result.TotalWins);
    Assert.Equal(1, result.TotalLosses); // Counts as loss
    Assert.Single(result.RecentForm);
    Assert.Equal("L", result.RecentForm[0]);
}
```

### Test Case #7: RecentForm capped at 5
```csharp
[Fact]
public async Task GetPlayerStatsAsync_WhenPlayerHasMoreThan5Matches_RecentFormCappedAt5()
{
    // Arrange
    var playerId = 1;
    var playerTpId = 100;
    
    var tournamentPlayers = new List<TournamentPlayer>
    {
        new() { Id = playerTpId, PlayerId = playerId }
    };
    
    // Create 7 matches
    var baseDate = DateTime.UtcNow;
    var matches = Enumerable.Range(1, 7).Select(i => new Match
    {
        Id = i,
        Status = MatchStatus.Completed,
        Player1TpId = playerTpId,
        Player2TpId = 200,
        WinnerTpId = i % 2 == 0 ? playerTpId : 200, // Alternating wins/losses
        Tournament = new Tournament 
        { 
            StartUtc = baseDate.AddDays(-i), // Sorted by date
            GameType = GameType.NineBall 
        }
    }).ToList();
    
    _mockDbContext.Setup(x => x.TournamentPlayers).Returns(MockDbSet(tournamentPlayers));
    _mockDbContext.Setup(x => x.Matches).Returns(MockDbSet(matches));

    // Act
    var result = await _playerProfileService.GetPlayerStatsAsync(playerId);

    // Assert
    Assert.Equal(7, result.TotalMatches);
    Assert.Equal(5, result.RecentForm.Count); // Capped at 5!
}
```

### Test Case #9: Multiple GameTypes
```csharp
[Fact]
public async Task GetPlayerStatsAsync_WhenPlayerHasMultipleGameTypes_GroupsStatsByGameType()
{
    // Arrange
    var playerId = 1;
    var playerTpId = 100;
    
    var tournamentPlayers = new List<TournamentPlayer>
    {
        new() { Id = playerTpId, PlayerId = playerId }
    };
    
    var matches = new List<Match>
    {
        // NineBall: 2 wins, 1 loss
        CreateMatch(1, playerTpId, 200, playerTpId, GameType.NineBall),
        CreateMatch(2, playerTpId, 200, playerTpId, GameType.NineBall),
        CreateMatch(3, playerTpId, 200, 200, GameType.NineBall),
        // EightBall: 1 win, 1 loss
        CreateMatch(4, playerTpId, 200, playerTpId, GameType.EightBall),
        CreateMatch(5, playerTpId, 200, 200, GameType.EightBall),
    };
    
    _mockDbContext.Setup(x => x.TournamentPlayers).Returns(MockDbSet(tournamentPlayers));
    _mockDbContext.Setup(x => x.Matches).Returns(MockDbSet(matches));

    // Act
    var result = await _playerProfileService.GetPlayerStatsAsync(playerId);

    // Assert
    Assert.Equal(2, result.StatsByGameType.Count);
    
    var nineBall = result.StatsByGameType.First(x => x.GameType == "NineBall");
    Assert.Equal(2, nineBall.Wins);
    Assert.Equal(1, nineBall.Losses);
    Assert.Equal(66.7, nineBall.WinRate); // 2/3 * 100
    
    var eightBall = result.StatsByGameType.First(x => x.GameType == "EightBall");
    Assert.Equal(1, eightBall.Wins);
    Assert.Equal(1, eightBall.Losses);
    Assert.Equal(50.0, eightBall.WinRate);
}
```

---

## Tổng kết Coverage

| Nhánh Code | Test Case IDs |
|:-----------|:--------------|
| `tpIds.Count == 0` | #1 |
| `tpIds.Count > 0` | #2, #3, #4, #5, #6, #7, #8, #9, #10, #11, #12, #13, #14, #15, #16, #17, #18 |
| `isWin = true` (WinnerTpId in tpIds) | #3, #10, #11 |
| `isWin = false` (WinnerTpId not in tpIds) | #4, #12 |
| `isWin = false` (WinnerTpId is null) | #5 |
| `recentForm.Count < 5` (add item) | #3, #4, #5, #6, #8 |
| `recentForm.Count >= 5` (skip) | #7 |
| `!gameTypeStats.ContainsKey` (create new) | #3, #9 |
| `gameTypeStats.ContainsKey` (update) | #9 |
| `totalMatches > 0` (calculate winRate) | #3, #4, #9, #10, #11, #12, #13 |
| `totalMatches == 0` (winRate = 0) | #2 |
| `totalGameMatches > 0` (calculate GameType winRate) | #9, #13 |
| Player1TpId match | #14 |
| Player2TpId match | #15 |
| Sorting by Tournament.StartUtc | #17 |
| Status = Completed filter | #18 |

---

## Thống kê

- **Tổng số Test Cases:** 18
- **Happy Path:** 6 (ID #2, #3, #4, #9, #10, #13, #16)
- **Error Cases:** 1 (ID #1)
- **Boundary Cases:** 3 (ID #6, #7, #8)
- **Edge Cases:** 8 (ID #5, #11, #12, #14, #15, #17, #18)

**Code Coverage dự kiến:** 100% cho hàm `GetPlayerStatsAsync`

---

## ⚠️ Lưu ý về Logic

### Trường hợp `WinnerTpId = null`:
- Hiện tại code coi đây là **thua** (vì `isWin = false`)
- Có thể cần xem xét: nên là thua hay là "không tính"?

### Trường hợp Player tham gia cả Player1 và Player2 trong cùng 1 match:
- Code hiện tại sẽ đếm match đó (vì OR condition)
- Có thể là trường hợp không thực tế nhưng code cho phép

---

## Đề xuất cải thiện Code

1. **Xử lý `WinnerTpId = null` rõ ràng hơn:**
   ```csharp
   // Option A: Không tính trận không có winner
   if (!m.WinnerTpId.HasValue) continue;
   
   // Option B: Ghi rõ trong comment đây là "Loss by default"
   ```

2. **Validate playerId:**
   ```csharp
   if (playerId <= 0) 
       throw new ArgumentException("Invalid player ID", nameof(playerId));
   ```

3. **Thêm null check cho navigation properties:**
   ```csharp
   // Đảm bảo Tournament không null khi truy cập GameType
   GameType = m.Tournament?.GameType ?? GameType.Unknown
   ```

