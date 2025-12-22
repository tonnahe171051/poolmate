# Test Cases cho `GetPlayersWithIssuesAsync` - AdminPlayerService

## Thông tin hàm

**File:** `PoolMateBackend/Services/AdminPlayerService.cs`

**Signature:**
```csharp
public async Task<PlayersWithIssuesDto> GetPlayersWithIssuesAsync(
    string issueType, 
    int pageIndex = 1,
    int pageSize = 20, 
    CancellationToken ct = default)
```

---

## Phân tích Control Flow

Hàm `GetPlayersWithIssuesAsync` có cấu trúc rất phức tạp với nhiều nhánh:

```
┌─────────────────────────────────────────────────────────────┐
│                  GetPlayersWithIssuesAsync                   │
└─────────────────────────────────────────────────────────────┘
                              │
                              ▼
              ┌───────────────────────────────┐
              │ issueType.Trim().ToLower()    │
              └───────────────────────────────┘
                              │
                              ▼
              ┌───────────────────────────────┐
              │         SWITCH CASE           │
              └───────────────────────────────┘
                              │
    ┌─────────┬─────────┬─────────┬─────────┬─────────┬─────────┬─────────┬─────────┬─────────┐
    ▼         ▼         ▼         ▼         ▼         ▼         ▼         ▼         ▼
┌────────┐┌────────┐┌────────┐┌────────┐┌────────┐┌────────┐┌────────┐┌────────┐┌────────┐
│missing-││missing-││missing-││inactive││never-  ││invalid-││invalid-││potential││default │
│email   ││phone   ││skill   ││-1y     ││played  ││email   ││phone   ││duplicat││(empty) │
└────────┘└────────┘└────────┘└────────┘└────────┘└────────┘└────────┘└────────┘└────────┘
    │         │         │         │         │         │         │         │         │
    └─────────┴─────────┴─────────┴─────────┴─────────┴─────────┴─────────┴─────────┘
                              │
                              ▼
              ┌───────────────────────────────┐
              │ issueTypeLower ==             │
              │ "potential-duplicates" ?      │
              └───────────────────────────────┘
                    │YES              │NO
                    ▼                 ▼
          ┌────────────────┐  ┌─────────────────┐
          │ Special Sort:  │  │ Keep default    │
          │ Email > Phone  │  │ query order     │
          │ > FullName     │  │                 │
          └────────────────┘  └─────────────────┘
                    │                 │
                    └────────┬────────┘
                             ▼
              ┌───────────────────────────────┐
              │ issueTypeLower.StartsWith     │
              │ ("invalid-") ?                │
              └───────────────────────────────┘
                    │YES              │NO
                    ▼                 ▼
    ┌─────────────────────────┐  ┌─────────────────────────┐
    │ LUỒNG A: In-Memory      │  │ LUỒNG B: SQL Processing │
    │ - Take(2000) candidates │  │ - CountAsync()          │
    │ - Validate with Regex   │  │ - Paging at DB level    │
    │ - Paging in-memory      │  │                         │
    └─────────────────────────┘  └─────────────────────────┘
                │                         │
                ▼                         ▼
    ┌─────────────────────┐  ┌─────────────────────────────┐
    │ invalid-email       │  │ != "potential-duplicates"?  │
    │ vs invalid-phone    │  └─────────────────────────────┘
    └─────────────────────┘        │YES        │NO
                │                  ▼           ▼
                │          OrderBy(Id)    Keep special
                │                          sort order
                │                  │           │
                └──────────────────┴───────────┘
                                   │
                                   ▼
              ┌───────────────────────────────┐
              │ Return PlayersWithIssuesDto   │
              │ {Players, TotalCount}         │
              └───────────────────────────────┘
```

---

## Boundary Analysis

### 1. Tournament Activity for `inactive-1y` (comparison: `>=`)

| Vị trí | Tournament.StartUtc | So sánh với oneYearAgo | Player Status |
|:-------|:--------------------|:-----------------------|:--------------|
| Dưới biên | `now.AddYears(-1).AddDays(1)` | > oneYearAgo | ✅ Active (NOT inactive) |
| Ngay biên | `now.AddYears(-1)` (exactly) | = oneYearAgo | ✅ Active (>= passes, NOT inactive) |
| Trên biên | `now.AddYears(-1).AddDays(-1)` | < oneYearAgo | ❌ Inactive |

### 2. Paging Parameters

| Scenario | pageIndex | pageSize | Total | Skip | Take | Items returned |
|:---------|:----------|:---------|:------|:-----|:-----|:---------------|
| First page | 1 | 20 | 50 | 0 | 20 | 20 |
| Second page | 2 | 20 | 50 | 20 | 20 | 20 |
| Last partial | 3 | 20 | 50 | 40 | 20 | 10 |
| Beyond total | 5 | 20 | 50 | 80 | 20 | 0 |

### 3. In-Memory Limit (invalid-* types)

| Total candidates | Limit | Actual checked |
|:-----------------|:------|:---------------|
| 1000 | 2000 | 1000 |
| 2000 | 2000 | 2000 |
| 3000 | 2000 | 2000 (first 2000 only) |

---

## Bảng Test Cases

| ID | Loại | Tên Test Case (Gợi ý đặt tên) | Input Giả định (Mock Data) | Kết quả Mong đợi | Ghi chú |
|:---|:-----|:------------------------------|:---------------------------|:-----------------|:--------|
| 1 | Error | GetPlayersWithIssuesAsync_WhenUnknownIssueType_ReturnsEmptyDto | `issueType = "unknown-type"` | Return `PlayersWithIssuesDto` với `Players = []`, `TotalCount = 0` | [Fact] |
| 2 | Happy | GetPlayersWithIssuesAsync_WhenMissingEmail_ReturnsPlayersWithNullOrEmptyEmail | `issueType = "missing-email"`, Players có `Email = null` hoặc `""` | Return matching players, Issues contains "Missing email" | [Fact] |
| 3 | Happy | GetPlayersWithIssuesAsync_WhenMissingPhone_ReturnsPlayersWithNullOrEmptyPhone | `issueType = "missing-phone"`, Players có `Phone = null` hoặc `""` | Return matching players, Issues contains "Missing phone" | [Fact] |
| 4 | Happy | GetPlayersWithIssuesAsync_WhenMissingSkill_ReturnsPlayersWithNullSkillLevel | `issueType = "missing-skill"`, Players có `SkillLevel = null` | Return matching players, Issues contains "Missing skill level" | [Fact] |
| 5 | Happy | GetPlayersWithIssuesAsync_WhenInactive1y_ReturnsInactivePlayers | `issueType = "inactive-1y"`, Player có tournament > 1 năm trước | Return matching players, Issues contains "Inactive > 1 year" | [Fact] |
| 6 | Boundary | GetPlayersWithIssuesAsync_WhenTournamentExactlyOneYearAgo_PlayerIsNotInactive | Tournament.StartUtc = `now.AddYears(-1)` (exactly) | Player NOT in result list (>= passes) | [Fact] - Ngay biên |
| 7 | Boundary | GetPlayersWithIssuesAsync_WhenTournamentOverOneYearAgo_PlayerIsInactive | Tournament.StartUtc = `now.AddYears(-1).AddDays(-1)` | Player IS in result list | [Fact] - Trên biên |
| 8 | Happy | GetPlayersWithIssuesAsync_WhenNeverPlayed_ReturnsPlayersWithNoTournaments | `issueType = "never-played"`, Player không có TournamentPlayers | Return matching players, Issues contains "Never played" | [Fact] |
| 9 | Happy | GetPlayersWithIssuesAsync_WhenInvalidEmail_ValidatesEmailInMemory | `issueType = "invalid-email"`, Player có `Email = "not-valid-email"` | Return players with invalid format, Issues contains "Invalid email format" | [Fact] |
| 10 | Happy | GetPlayersWithIssuesAsync_WhenInvalidPhone_ValidatesPhoneInMemory | `issueType = "invalid-phone"`, Player có `Phone = "abc"` | Return players with invalid format, Issues contains "Invalid phone format" | [Fact] |
| 11 | Happy | GetPlayersWithIssuesAsync_WhenPotentialDuplicates_FindsDuplicateEmails | `issueType = "potential-duplicates"`, 2 Players cùng Email | Return both players, Issues contains "Potential duplicate" | [Fact] |
| 12 | Happy | GetPlayersWithIssuesAsync_WhenPotentialDuplicates_FindsDuplicatePhones | `issueType = "potential-duplicates"`, 2 Players cùng Phone | Return both players | [Fact] |
| 13 | Happy | GetPlayersWithIssuesAsync_WhenPotentialDuplicates_FindsDuplicateContext | `issueType = "potential-duplicates"`, 2 Players cùng FullName + City + SkillLevel | Return both players | [Fact] |
| 14 | Edge | GetPlayersWithIssuesAsync_WhenPotentialDuplicates_SortsByEmailPhoneName | `issueType = "potential-duplicates"` | Result sorted by Email → Phone → FullName | [Fact] |
| 15 | Edge | GetPlayersWithIssuesAsync_WhenNotDuplicates_SortsById | `issueType = "missing-email"` | Result sorted by Id (default) | [Fact] |
| 16 | Boundary | GetPlayersWithIssuesAsync_WhenPageIndex1_ReturnsFirstPage | `pageIndex = 1`, `pageSize = 5`, 12 matching players | Return 5 items, TotalCount = 12 | [Fact] |
| 17 | Boundary | GetPlayersWithIssuesAsync_WhenPageIndex2_ReturnsSecondPage | `pageIndex = 2`, `pageSize = 5`, 12 matching players | Return 5 items (6-10), TotalCount = 12 | [Fact] |
| 18 | Boundary | GetPlayersWithIssuesAsync_WhenPageExceedsTotalPages_ReturnsEmptyList | `pageIndex = 10`, `pageSize = 5`, 12 matching players | Return 0 items, TotalCount = 12 | [Fact] |
| 19 | Edge | GetPlayersWithIssuesAsync_WhenInvalidEmail_InMemoryPagingWorks | `issueType = "invalid-email"`, 50 invalid emails, page 2 | Return page 2 correctly with in-memory paging | [Fact] |
| 20 | Edge | GetPlayersWithIssuesAsync_WhenInactive1y_IncludesLastTournamentDate | `issueType = "inactive-1y"` | `LastTournamentDate` is populated (not null) | [Fact] |
| 21 | Edge | GetPlayersWithIssuesAsync_WhenOtherTypes_LastTournamentDateIsNull | `issueType = "missing-email"` | `LastTournamentDate = null` for all results | [Fact] |
| 22 | Happy | GetPlayersWithIssuesAsync_WhenNoMatchingPlayers_ReturnsEmptyList | `issueType = "missing-email"`, all players have email | `Players = []`, `TotalCount = 0` | [Fact] |
| 23 | Edge | GetPlayersWithIssuesAsync_IssueTypeIsCaseInsensitive | `issueType = "MISSING-EMAIL"` hoặc `"Missing-Email"` | Xử lý đúng, return matching players | [Theory] với `[InlineData("MISSING-EMAIL")]`, `[InlineData("Missing-Email")]`, `[InlineData("mIsSiNg-EmAiL")]` |
| 24 | Edge | GetPlayersWithIssuesAsync_IssueTypeWithWhitespace_IsTrimmed | `issueType = "  missing-email  "` | Trim thành "missing-email", xử lý đúng | [Fact] |
| 25 | Edge | GetPlayersWithIssuesAsync_WhenInvalidEmail_LimitedTo2000Candidates | 3000 players với email, 100 có format invalid | Chỉ check 2000 đầu tiên, return those invalid in limit | [Fact] |

---

## Chi tiết Mock Setup cho từng Test Case

### Test Case #1: Unknown issue type
```csharp
[Fact]
public async Task GetPlayersWithIssuesAsync_WhenUnknownIssueType_ReturnsEmptyDto()
{
    // Arrange
    var issueType = "unknown-type";
    
    // No mock needed - goes to default case

    // Act
    var result = await _adminPlayerService.GetPlayersWithIssuesAsync(issueType);

    // Assert
    Assert.NotNull(result);
    Assert.Empty(result.Players);
    Assert.Equal(0, result.TotalCount);
}
```

### Test Case #2: Missing email
```csharp
[Fact]
public async Task GetPlayersWithIssuesAsync_WhenMissingEmail_ReturnsPlayersWithNullOrEmptyEmail()
{
    // Arrange
    var players = new List<Player>
    {
        new() { Id = 1, FullName = "Player 1", Email = null },      // Should match
        new() { Id = 2, FullName = "Player 2", Email = "" },         // Should match
        new() { Id = 3, FullName = "Player 3", Email = "test@test.com" } // Should NOT match
    };
    
    _mockDbContext.Setup(x => x.Players).Returns(MockDbSet(players));

    // Act
    var result = await _adminPlayerService.GetPlayersWithIssuesAsync("missing-email");

    // Assert
    Assert.Equal(2, result.TotalCount);
    Assert.All(result.Players, p => Assert.Contains("Missing email", p.Issues));
}
```

### Test Case #6 & #7: Boundary for inactive-1y
```csharp
[Fact]
public async Task GetPlayersWithIssuesAsync_WhenTournamentExactlyOneYearAgo_PlayerIsNotInactive()
{
    // Arrange
    var now = DateTime.UtcNow;
    var exactlyOneYearAgo = now.AddYears(-1);
    
    var players = new List<Player>
    {
        new()
        {
            Id = 1,
            FullName = "Player 1",
            Email = "test@test.com",
            TournamentPlayers = new List<TournamentPlayer>
            {
                new()
                {
                    Tournament = new Tournament { StartUtc = exactlyOneYearAgo } // EXACTLY at boundary
                }
            }
        }
    };
    
    _mockDbContext.Setup(x => x.Players).Returns(MockDbSet(players));

    // Act
    var result = await _adminPlayerService.GetPlayersWithIssuesAsync("inactive-1y");

    // Assert
    Assert.Equal(0, result.TotalCount); // NOT inactive because >= passes
}

[Fact]
public async Task GetPlayersWithIssuesAsync_WhenTournamentOverOneYearAgo_PlayerIsInactive()
{
    // Arrange
    var now = DateTime.UtcNow;
    var overOneYearAgo = now.AddYears(-1).AddDays(-1); // 1 day past the 1-year mark
    
    var players = new List<Player>
    {
        new()
        {
            Id = 1,
            FullName = "Player 1",
            Email = "test@test.com",
            TournamentPlayers = new List<TournamentPlayer>
            {
                new()
                {
                    Tournament = new Tournament { StartUtc = overOneYearAgo }
                }
            }
        }
    };
    
    _mockDbContext.Setup(x => x.Players).Returns(MockDbSet(players));

    // Act
    var result = await _adminPlayerService.GetPlayersWithIssuesAsync("inactive-1y");

    // Assert
    Assert.Equal(1, result.TotalCount);
    Assert.Contains("Inactive > 1 year", result.Players[0].Issues);
}
```

### Test Case #9: Invalid email (in-memory validation)
```csharp
[Fact]
public async Task GetPlayersWithIssuesAsync_WhenInvalidEmail_ValidatesEmailInMemory()
{
    // Arrange
    var players = new List<Player>
    {
        new() { Id = 1, FullName = "P1", Email = "valid@test.com" },    // Valid - excluded
        new() { Id = 2, FullName = "P2", Email = "not-an-email" },       // Invalid - included
        new() { Id = 3, FullName = "P3", Email = "also-bad" },           // Invalid - included
        new() { Id = 4, FullName = "P4", Email = null },                  // No email - excluded from candidates
    };
    
    _mockDbContext.Setup(x => x.Players).Returns(MockDbSet(players));

    // Act
    var result = await _adminPlayerService.GetPlayersWithIssuesAsync("invalid-email");

    // Assert
    Assert.Equal(2, result.TotalCount);
    Assert.All(result.Players, p => Assert.Contains("Invalid email format", p.Issues));
}
```

### Test Case #11-13: Potential duplicates
```csharp
[Fact]
public async Task GetPlayersWithIssuesAsync_WhenPotentialDuplicates_FindsDuplicateEmails()
{
    // Arrange
    var players = new List<Player>
    {
        new() { Id = 1, FullName = "Player 1", Email = "same@test.com" },
        new() { Id = 2, FullName = "Player 2", Email = "same@test.com" },  // Duplicate email
        new() { Id = 3, FullName = "Player 3", Email = "unique@test.com" } // Unique - excluded
    };
    
    _mockDbContext.Setup(x => x.Players).Returns(MockDbSet(players));

    // Act
    var result = await _adminPlayerService.GetPlayersWithIssuesAsync("potential-duplicates");

    // Assert
    Assert.Equal(2, result.TotalCount);
    Assert.All(result.Players, p => Assert.Equal("same@test.com", p.Email));
}
```

### Test Case #23: Case insensitivity
```csharp
[Theory]
[InlineData("MISSING-EMAIL")]
[InlineData("Missing-Email")]
[InlineData("mIsSiNg-EmAiL")]
public async Task GetPlayersWithIssuesAsync_IssueTypeIsCaseInsensitive(string issueType)
{
    // Arrange
    var players = new List<Player>
    {
        new() { Id = 1, FullName = "Player 1", Email = null }
    };
    
    _mockDbContext.Setup(x => x.Players).Returns(MockDbSet(players));

    // Act
    var result = await _adminPlayerService.GetPlayersWithIssuesAsync(issueType);

    // Assert
    Assert.Equal(1, result.TotalCount);
}
```

---

## Tổng kết Coverage

| Nhánh Code | Test Case IDs |
|:-----------|:--------------|
| Switch case: `missing-email` | #2, #15, #21, #22, #23, #24 |
| Switch case: `missing-phone` | #3 |
| Switch case: `missing-skill` | #4 |
| Switch case: `inactive-1y` | #5, #6, #7, #20 |
| Switch case: `never-played` | #8 |
| Switch case: `invalid-email` | #9, #19, #25 |
| Switch case: `invalid-phone` | #10 |
| Switch case: `potential-duplicates` | #11, #12, #13, #14 |
| Switch case: `default` | #1 |
| `issueTypeLower == "potential-duplicates"` (special sort) | #14 |
| `issueTypeLower != "potential-duplicates"` (default sort) | #15 |
| `issueTypeLower.StartsWith("invalid-")` = true (Luồng A) | #9, #10, #19, #25 |
| `issueTypeLower.StartsWith("invalid-")` = false (Luồng B) | #2, #3, #4, #5, #8, #11 |
| `issueTypeLower == "invalid-email"` | #9, #19 |
| `issueTypeLower == "invalid-phone"` | #10 |
| Paging logic | #16, #17, #18, #19 |
| `LastTournamentDate` populated (inactive-1y) | #20 |
| `LastTournamentDate` = null (other types) | #21 |
| Case insensitivity | #23 |
| Whitespace trimming | #24 |
| Take(2000) limit | #25 |

---

## Thống kê

- **Tổng số Test Cases:** 25
- **Happy Path:** 10 (ID #2, #3, #4, #5, #8, #9, #10, #11, #12, #13, #22)
- **Error Cases:** 1 (ID #1)
- **Boundary Cases:** 5 (ID #6, #7, #16, #17, #18)
- **Edge Cases:** 9 (ID #14, #15, #19, #20, #21, #23, #24, #25)

**Code Coverage dự kiến:** 100% cho hàm `GetPlayersWithIssuesAsync`

---

## ⚠️ Lưu ý về Logic

### 1. In-Memory Validation Limit
- Với `invalid-email` và `invalid-phone`, code chỉ lấy tối đa **2000 records** để validate in-memory
- Nếu có nhiều hơn 2000 records, những records sau sẽ không được check
- Đây là trade-off giữa performance và completeness

### 2. Potential Duplicates Logic
- Tìm duplicates dựa trên 3 criteria:
  1. Email trùng lặp
  2. Phone trùng lặp
  3. Combo (FullName + City + SkillLevel) trùng lặp
- Player chỉ cần match 1 trong 3 criteria là được include

### 3. LastTournamentDate
- Chỉ được populate khi `issueType = "inactive-1y"`
- Các loại khác sẽ có `LastTournamentDate = null`

### 4. Sorting
- `potential-duplicates`: Sort by Email → Phone → FullName
- Các loại khác: Sort by Id (default)

---

## Đề xuất cải thiện Code

1. **Null check cho issueType:**
   ```csharp
   if (string.IsNullOrWhiteSpace(issueType))
       return new PlayersWithIssuesDto();
   ```
   
   Hiện tại nếu `issueType = null`, code sẽ crash ở `.Trim()`.

2. **Configurable limit cho in-memory validation:**
   ```csharp
   private const int MaxInMemoryValidationRecords = 2000;
   ```

3. **Async stream cho xử lý lớn:**
   ```csharp
   await foreach (var batch in GetPlayerBatchesAsync())
   {
       // Process in batches
   }
   ```

