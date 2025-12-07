# Test Cases cho `GetMatchHistoryAsync` - PlayerProfileService

## Thông tin hàm

**File:** `PoolMateBackend/Services/PlayerProfileService.cs`

**Signature:**
```csharp
public async Task<PagingList<MatchHistoryDto>> GetMatchHistoryAsync(
    int playerId,
    int pageIndex = 1,
    int pageSize = 20,
    CancellationToken ct = default)
```

---

## Phân tích Control Flow

Hàm `GetMatchHistoryAsync` có các nhánh điều kiện phức tạp trong projection:

```
┌─────────────────────────────────────────────────────────────┐
│                    GetMatchHistoryAsync                      │
└─────────────────────────────────────────────────────────────┘
                              │
                              ▼
              ┌───────────────────────────────┐
              │ Query Matches where:          │
              │ - Status = Completed          │
              │ - Player1Tp.PlayerId = id OR  │
              │   Player2Tp.PlayerId = id     │
              └───────────────────────────────┘
                              │
                              ▼
              ┌───────────────────────────────┐
              │ Count total matches           │
              └───────────────────────────────┘
                              │
                              ▼
              ┌───────────────────────────────┐
              │ Order by Tournament.StartUtc  │
              │ desc, then by RoundNo desc    │
              └───────────────────────────────┘
                              │
                              ▼
              ┌───────────────────────────────┐
              │ Skip((pageIndex-1)*pageSize)  │
              │ Take(pageSize)                │
              └───────────────────────────────┘
                              │
                              ▼
              ┌───────────────────────────────┐
              │ Projection with IsPlayer1     │
              │ determination                 │
              └───────────────────────────────┘
                              │
        ┌─────────────────────┴─────────────────────┐
        │ IsPlayer1 = true                          │ IsPlayer1 = false
        ▼                                           ▼
┌─────────────────────┐                   ┌─────────────────────┐
│ OpponentName =      │                   │ OpponentName =      │
│   Player2Tp?.Name   │                   │   Player1Tp?.Name   │
│   ?? "Bye"          │                   │   ?? "Bye"          │
├─────────────────────┤                   ├─────────────────────┤
│ OpponentId =        │                   │ OpponentId =        │
│   Player2Tp?.Id     │                   │   Player1Tp?.Id     │
│   ?? null           │                   │   ?? null           │
├─────────────────────┤                   ├─────────────────────┤
│ Score =             │                   │ Score =             │
│   "P1Score - P2Score│                   │   "P2Score - P1Score│
├─────────────────────┤                   ├─────────────────────┤
│ Result = WinnerTpId │                   │ Result = WinnerTpId │
│   == Player1TpId    │                   │   == Player2TpId    │
│   ? "Win" : "Loss"  │                   │   ? "Win" : "Loss"  │
└─────────────────────┘                   └─────────────────────┘
                              │
                              ▼
              ┌───────────────────────────────┐
              │ Return PagingList<>           │
              └───────────────────────────────┘
```

---

## Boundary Analysis

### 1. Paging Parameters

| Scenario | pageIndex | pageSize | totalCount | Skip | Take | Items returned |
|:---------|:----------|:---------|:-----------|:-----|:-----|:---------------|
| First page | 1 | 20 | 50 | 0 | 20 | 20 items |
| Second page | 2 | 20 | 50 | 20 | 20 | 20 items |
| Last partial page | 3 | 20 | 50 | 40 | 20 | 10 items |
| Page exceeds total | 4 | 20 | 50 | 60 | 20 | 0 items |
| Small page size | 1 | 5 | 50 | 0 | 5 | 5 items |
| Empty result | 1 | 20 | 0 | 0 | 20 | 0 items |

### 2. Score Null Handling

| ScoreP1 | ScoreP2 | IsPlayer1 | Score Output |
|:--------|:--------|:----------|:-------------|
| null | null | true | "0 - 0" |
| null | null | false | "0 - 0" |
| 5 | null | true | "5 - 0" |
| null | 3 | true | "0 - 3" |
| 5 | 3 | true | "5 - 3" |
| 5 | 3 | false | "3 - 5" (swapped) |

### 3. RaceTo Null Handling

| RaceTo | Output |
|:-------|:-------|
| null | 0 |
| 5 | 5 |

---

## Bảng Test Cases

| ID | Loại | Tên Test Case (Gợi ý đặt tên) | Input Giả định (Mock Data) | Kết quả Mong đợi | Ghi chú |
|:---|:-----|:------------------------------|:---------------------------|:-----------------|:--------|
| 1 | Happy | GetMatchHistoryAsync_WhenPlayerHasNoMatches_ReturnsEmptyPagingList | `playerId = 999`, DB không có match nào cho player này | Return `PagingList` với `Items = []`, `TotalCount = 0`, `PageIndex = 1`, `PageSize = 20` | [Fact] |
| 2 | Happy | GetMatchHistoryAsync_WhenPlayerIsPlayer1_ReturnsCorrectPerspective | Player là Player1Tp trong match, ScoreP1=5, ScoreP2=3 | `Score = "5 - 3"`, `OpponentName = Player2's DisplayName` | [Fact] |
| 3 | Happy | GetMatchHistoryAsync_WhenPlayerIsPlayer2_ReturnsSwappedScore | Player là Player2Tp trong match, ScoreP1=5, ScoreP2=3 | `Score = "3 - 5"` (swapped), `OpponentName = Player1's DisplayName` | [Fact] |
| 4 | Edge | GetMatchHistoryAsync_WhenPlayer1HasByeOpponent_ReturnsOpponentNameBye | `Player1Tp = player`, `Player2Tp = null` | `OpponentName = "Bye"`, `OpponentId = null` | [Fact] |
| 5 | Edge | GetMatchHistoryAsync_WhenPlayer2HasByeOpponent_ReturnsOpponentNameBye | `Player1Tp = null`, `Player2Tp = player` | `OpponentName = "Bye"`, `OpponentId = null` | [Fact] |
| 6 | Happy | GetMatchHistoryAsync_WhenPlayerWins_ResultIsWin | `WinnerTpId = player's TpId` | `Result = "Win"` | [Fact] |
| 7 | Happy | GetMatchHistoryAsync_WhenPlayerLoses_ResultIsLoss | `WinnerTpId = opponent's TpId` | `Result = "Loss"` | [Fact] |
| 8 | Edge | GetMatchHistoryAsync_WhenScoresAreNull_DefaultsToZero | `ScoreP1 = null`, `ScoreP2 = null` | `Score = "0 - 0"` | [Fact] |
| 9 | Edge | GetMatchHistoryAsync_WhenScoreP1IsNullOnly_DefaultsP1ToZero | `ScoreP1 = null`, `ScoreP2 = 3` | `Score = "0 - 3"` (if Player1) | [Fact] |
| 10 | Edge | GetMatchHistoryAsync_WhenRaceToIsNull_DefaultsToZero | `RaceTo = null` | `RaceTo = 0` | [Fact] |
| 11 | Happy | GetMatchHistoryAsync_OnlyIncludesCompletedMatches | DB có 3 Completed, 2 InProgress, 1 Scheduled cho player | `TotalCount = 3`, Items chỉ chứa Completed matches | [Fact] |
| 12 | Happy | GetMatchHistoryAsync_SortsByTournamentDateDescThenRoundNoDesc | 3 Matches: T1(Jan, R2), T1(Jan, R1), T2(Feb, R1) | Order: T2-R1, T1-R2, T1-R1 | [Fact] |
| 13 | Boundary | GetMatchHistoryAsync_WhenPageIndex1_ReturnsFirstPageItems | `pageIndex = 1`, `pageSize = 5`, 12 matches | Return items 1-5, `TotalCount = 12` | [Fact] |
| 14 | Boundary | GetMatchHistoryAsync_WhenPageIndex2_ReturnsSecondPageItems | `pageIndex = 2`, `pageSize = 5`, 12 matches | Return items 6-10, `TotalCount = 12` | [Fact] |
| 15 | Boundary | GetMatchHistoryAsync_WhenPageIndexExceedsTotalPages_ReturnsEmptyItems | `pageIndex = 10`, `pageSize = 5`, 12 matches | Return `Items = []`, `TotalCount = 12` | [Fact] |
| 16 | Boundary | GetMatchHistoryAsync_WhenPageSizeExceedsTotalCount_ReturnsAllItems | `pageSize = 100`, 12 matches | Return all 12 items | [Fact] |
| 17 | Happy | GetMatchHistoryAsync_MapsAllFieldsCorrectly | Complete match with all data | Verify mapping: `MatchId`, `TournamentId`, `TournamentName`, `TournamentDate`, `GameType`, `StageType`, `BracketSide`, `RoundName`, `OpponentName`, `OpponentId`, `Score`, `RaceTo`, `Result`, `MatchDate` | [Fact] |
| 18 | Happy | GetMatchHistoryAsync_RoundNameFormattedCorrectly | `RoundNo = 3` | `RoundName = "Round 3"` | [Fact] |

---

## Chi tiết Mock Setup cho từng Test Case

### Test Case #1: No matches for player
```csharp
[Fact]
public async Task GetMatchHistoryAsync_WhenPlayerHasNoMatches_ReturnsEmptyPagingList()
{
    // Arrange
    var playerId = 999;
    
    // Mock: Empty matches or no matches for this player
    _mockDbContext.Setup(x => x.Matches).Returns(MockDbSet(new List<Match>()));

    // Act
    var result = await _playerProfileService.GetMatchHistoryAsync(playerId);

    // Assert
    Assert.NotNull(result);
    Assert.Empty(result.Items);
    Assert.Equal(0, result.TotalCount);
    Assert.Equal(1, result.PageIndex);
    Assert.Equal(20, result.PageSize);
}
```

### Test Case #2 & #3: Player1 vs Player2 perspective
```csharp
[Fact]
public async Task GetMatchHistoryAsync_WhenPlayerIsPlayer1_ReturnsCorrectPerspective()
{
    // Arrange
    var playerId = 1;
    var player1TpId = 100;
    var player2TpId = 200;
    
    var matches = new List<Match>
    {
        new()
        {
            Id = 1,
            Status = MatchStatus.Completed,
            Player1TpId = player1TpId,
            Player2TpId = player2TpId,
            Player1Tp = new TournamentPlayer { Id = player1TpId, PlayerId = playerId, DisplayName = "Player One" },
            Player2Tp = new TournamentPlayer { Id = player2TpId, PlayerId = 2, DisplayName = "Player Two" },
            ScoreP1 = 5,
            ScoreP2 = 3,
            WinnerTpId = player1TpId,
            Tournament = new Tournament { StartUtc = DateTime.UtcNow, GameType = GameType.NineBall, Name = "Test" },
            Stage = new TournamentStage { Type = StageType.SingleElimination },
            Bracket = BracketSide.Winners,
            RoundNo = 1
        }
    };
    
    _mockDbContext.Setup(x => x.Matches).Returns(MockDbSet(matches));

    // Act
    var result = await _playerProfileService.GetMatchHistoryAsync(playerId);

    // Assert
    Assert.Single(result.Items);
    var item = result.Items[0];
    Assert.Equal("5 - 3", item.Score); // Player1's perspective
    Assert.Equal("Player Two", item.OpponentName);
    Assert.Equal(2, item.OpponentId);
    Assert.Equal("Win", item.Result);
}

[Fact]
public async Task GetMatchHistoryAsync_WhenPlayerIsPlayer2_ReturnsSwappedScore()
{
    // Arrange - Player is Player2
    var playerId = 2;
    var player1TpId = 100;
    var player2TpId = 200;
    
    var matches = new List<Match>
    {
        new()
        {
            Id = 1,
            Status = MatchStatus.Completed,
            Player1TpId = player1TpId,
            Player2TpId = player2TpId,
            Player1Tp = new TournamentPlayer { Id = player1TpId, PlayerId = 1, DisplayName = "Player One" },
            Player2Tp = new TournamentPlayer { Id = player2TpId, PlayerId = playerId, DisplayName = "Player Two" },
            ScoreP1 = 5,
            ScoreP2 = 3,
            WinnerTpId = player1TpId, // Player1 wins
            Tournament = new Tournament { StartUtc = DateTime.UtcNow, GameType = GameType.NineBall, Name = "Test" },
            Stage = new TournamentStage { Type = StageType.SingleElimination },
            Bracket = BracketSide.Winners,
            RoundNo = 1
        }
    };
    
    _mockDbContext.Setup(x => x.Matches).Returns(MockDbSet(matches));

    // Act
    var result = await _playerProfileService.GetMatchHistoryAsync(playerId);

    // Assert
    Assert.Single(result.Items);
    var item = result.Items[0];
    Assert.Equal("3 - 5", item.Score); // Swapped! Player2's perspective
    Assert.Equal("Player One", item.OpponentName);
    Assert.Equal(1, item.OpponentId);
    Assert.Equal("Loss", item.Result); // Player2 lost
}
```

### Test Case #4 & #5: Bye opponent
```csharp
[Fact]
public async Task GetMatchHistoryAsync_WhenPlayer1HasByeOpponent_ReturnsOpponentNameBye()
{
    // Arrange
    var playerId = 1;
    var player1TpId = 100;
    
    var matches = new List<Match>
    {
        new()
        {
            Id = 1,
            Status = MatchStatus.Completed,
            Player1TpId = player1TpId,
            Player2TpId = null, // BYE
            Player1Tp = new TournamentPlayer { Id = player1TpId, PlayerId = playerId, DisplayName = "Player One" },
            Player2Tp = null,
            WinnerTpId = player1TpId,
            Tournament = new Tournament { StartUtc = DateTime.UtcNow, GameType = GameType.NineBall, Name = "Test" },
            Stage = new TournamentStage { Type = StageType.SingleElimination },
            Bracket = BracketSide.Winners,
            RoundNo = 1
        }
    };
    
    _mockDbContext.Setup(x => x.Matches).Returns(MockDbSet(matches));

    // Act
    var result = await _playerProfileService.GetMatchHistoryAsync(playerId);

    // Assert
    var item = result.Items[0];
    Assert.Equal("Bye", item.OpponentName);
    Assert.Null(item.OpponentId);
}
```

### Test Case #11: Only Completed matches
```csharp
[Fact]
public async Task GetMatchHistoryAsync_OnlyIncludesCompletedMatches()
{
    // Arrange
    var playerId = 1;
    var playerTpId = 100;
    
    var baseTp = new TournamentPlayer { Id = playerTpId, PlayerId = playerId, DisplayName = "Player" };
    var tournament = new Tournament { StartUtc = DateTime.UtcNow, GameType = GameType.NineBall, Name = "Test" };
    var stage = new TournamentStage { Type = StageType.SingleElimination };
    
    var matches = new List<Match>
    {
        CreateMatch(1, MatchStatus.Completed, playerTpId, baseTp, tournament, stage),
        CreateMatch(2, MatchStatus.Completed, playerTpId, baseTp, tournament, stage),
        CreateMatch(3, MatchStatus.Completed, playerTpId, baseTp, tournament, stage),
        CreateMatch(4, MatchStatus.InProgress, playerTpId, baseTp, tournament, stage), // NOT included
        CreateMatch(5, MatchStatus.InProgress, playerTpId, baseTp, tournament, stage), // NOT included
        CreateMatch(6, MatchStatus.Scheduled, playerTpId, baseTp, tournament, stage),  // NOT included
    };
    
    _mockDbContext.Setup(x => x.Matches).Returns(MockDbSet(matches));

    // Act
    var result = await _playerProfileService.GetMatchHistoryAsync(playerId);

    // Assert
    Assert.Equal(3, result.TotalCount); // Only completed
    Assert.Equal(3, result.Items.Count);
}
```

### Test Case #12: Sorting verification
```csharp
[Fact]
public async Task GetMatchHistoryAsync_SortsByTournamentDateDescThenRoundNoDesc()
{
    // Arrange
    var playerId = 1;
    var playerTpId = 100;
    
    var jan2024 = new DateTime(2024, 1, 15);
    var feb2024 = new DateTime(2024, 2, 15);
    
    var tournamentJan = new Tournament { Id = 1, StartUtc = jan2024, GameType = GameType.NineBall, Name = "Jan Tournament" };
    var tournamentFeb = new Tournament { Id = 2, StartUtc = feb2024, GameType = GameType.NineBall, Name = "Feb Tournament" };
    
    var baseTp = new TournamentPlayer { Id = playerTpId, PlayerId = playerId };
    var stage = new TournamentStage { Type = StageType.SingleElimination };
    
    var matches = new List<Match>
    {
        CreateMatch(1, jan2024, 2, playerTpId, baseTp, tournamentJan, stage), // Jan, Round 2
        CreateMatch(2, jan2024, 1, playerTpId, baseTp, tournamentJan, stage), // Jan, Round 1
        CreateMatch(3, feb2024, 1, playerTpId, baseTp, tournamentFeb, stage), // Feb, Round 1
    };
    
    _mockDbContext.Setup(x => x.Matches).Returns(MockDbSet(matches));

    // Act
    var result = await _playerProfileService.GetMatchHistoryAsync(playerId);

    // Assert - Expected order: Feb-R1, Jan-R2, Jan-R1
    Assert.Equal(3, result.Items.Count);
    Assert.Equal("Feb Tournament", result.Items[0].TournamentName);
    Assert.Equal("Round 1", result.Items[0].RoundName);
    Assert.Equal("Jan Tournament", result.Items[1].TournamentName);
    Assert.Equal("Round 2", result.Items[1].RoundName);
    Assert.Equal("Jan Tournament", result.Items[2].TournamentName);
    Assert.Equal("Round 1", result.Items[2].RoundName);
}
```

---

## Tổng kết Coverage

| Nhánh Code | Test Case IDs |
|:-----------|:--------------|
| `Status == Completed` filter | #11 |
| `Player1Tp.PlayerId == playerId` | #2, #4, #6 |
| `Player2Tp.PlayerId == playerId` | #3, #5, #7 |
| `IsPlayer1 = true` path | #2, #4, #6, #8 |
| `IsPlayer1 = false` path | #3, #5, #7 |
| `OpponentName = "Bye"` (Player2Tp null) | #4 |
| `OpponentName = "Bye"` (Player1Tp null) | #5 |
| `OpponentName = DisplayName` | #2, #3 |
| `Score` calculation (Player1 perspective) | #2, #8, #9 |
| `Score` calculation (Player2 perspective) | #3 |
| `ScoreP1 ?? 0`, `ScoreP2 ?? 0` | #8, #9 |
| `RaceTo ?? 0` | #10 |
| `Result = "Win"` | #6 |
| `Result = "Loss"` | #7 |
| Sorting | #12 |
| Paging Skip/Take | #13, #14, #15, #16 |
| Empty result | #1 |
| Field mapping | #17, #18 |

---

## Thống kê

- **Tổng số Test Cases:** 18
- **Happy Path:** 8 (ID #2, #3, #6, #7, #11, #12, #17, #18)
- **Error/Empty Cases:** 1 (ID #1)
- **Boundary Cases:** 4 (ID #13, #14, #15, #16)
- **Edge Cases:** 5 (ID #4, #5, #8, #9, #10)

**Code Coverage dự kiến:** 100% cho hàm `GetMatchHistoryAsync`

---

## ⚠️ Lưu ý về Logic

### 1. "Bye" Opponent Logic
- Khi `Player2Tp = null` và player là Player1 → OpponentName = "Bye"
- Khi `Player1Tp = null` và player là Player2 → OpponentName = "Bye"
- Đây là cách xử lý trận "walkover" hoặc "bye" trong tournament

### 2. Score Perspective
- Score luôn được hiển thị từ góc nhìn của player đang query
- Player1: "P1Score - P2Score"
- Player2: "P2Score - P1Score" (swapped)

### 3. Result Determination
- Win: `WinnerTpId == (IsPlayer1 ? Player1TpId : Player2TpId)`
- Loss: Ngược lại
- **Lưu ý:** Không có trường hợp "Draw" trong code hiện tại

---

## Đề xuất cải thiện Code

1. **Validate playerId:**
   ```csharp
   if (playerId <= 0)
       throw new ArgumentException("Invalid player ID", nameof(playerId));
   ```

2. **Validate paging parameters:**
   ```csharp
   if (pageIndex < 1) pageIndex = 1;
   if (pageSize < 1 || pageSize > 100) pageSize = 20;
   ```

3. **Handle null Tournament/Stage:**
   ```csharp
   TournamentName = x.Match.Tournament?.Name ?? "Unknown",
   StageType = x.Match.Stage?.Type.ToString() ?? "Unknown"
   ```

4. **Consider adding "Draw" result:**
   ```csharp
   Result = x.Match.WinnerTpId == null
       ? "Draw"
       : x.Match.WinnerTpId == (x.IsPlayer1 ? x.Match.Player1TpId : x.Match.Player2TpId)
           ? "Win"
           : "Loss"
   ```

