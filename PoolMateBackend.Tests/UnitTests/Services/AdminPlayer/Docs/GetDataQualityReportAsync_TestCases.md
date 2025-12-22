# Test Cases cho `GetDataQualityReportAsync` - AdminPlayerService

## Thông tin hàm

**File:** `PoolMateBackend/Services/AdminPlayerService.cs`

**Signature:**
```csharp
public async Task<DataQualityReportDto> GetDataQualityReportAsync(CancellationToken ct = default)
```

---

## Phân tích Control Flow

Hàm `GetDataQualityReportAsync` là một hàm phức tạp với nhiều nhánh điều kiện:

```
┌─────────────────────────────────────────────────────────────┐
│                  GetDataQualityReportAsync                   │
└─────────────────────────────────────────────────────────────┘
                              │
                              ▼
              ┌───────────────────────────────┐
              │ Calculate oneYearAgo =        │
              │ now.AddYears(-1)              │
              └───────────────────────────────┘
                              │
        ┌─────────────────────┼─────────────────────┐
        ▼                     ▼                     ▼
┌───────────────┐   ┌───────────────┐    ┌───────────────┐
│ Query A:      │   │ Query B:      │    │ Query C:      │
│ Inactive      │   │ NeverPlayed   │    │ Load all      │
│ Players       │   │ Tournament    │    │ Players       │
└───────────────┘   └───────────────┘    └───────────────┘
        │                     │                     │
        └─────────────────────┼─────────────────────┘
                              ▼
              ┌───────────────────────────────┐
              │ foreach (var p in players)    │
              └───────────────────────────────┘
                              │
        ┌─────────────────────┼─────────────────────┐
        │                     │                     │
        ▼                     ▼                     ▼
┌───────────────┐   ┌───────────────┐    ┌───────────────┐
│ Check Missing │   │ Check Invalid │    │ Track Email   │
│ Fields        │   │ Formats       │    │ Frequency     │
└───────────────┘   └───────────────┘    └───────────────┘
        │                     │                     │
        │    ┌────────────────┴────────────────┐    │
        │    │                                 │    │
        ▼    ▼                                 ▼    ▼
┌─────────────────────────────────────────────────────────┐
│ Missing Checks:                                          │
│ - Email: IsNullOrWhiteSpace(p.Email)                    │
│ - Phone: IsNullOrWhiteSpace(p.Phone)                    │
│ - Skill: p.SkillLevel == null                           │
│ - Location: IsNullOrWhiteSpace(Country) OR City         │
└─────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────┐
│ Invalid Checks (only if field has value):               │
│ - Email: !IsValidEmail(p.Email)                         │
│ - Phone: !IsValidPhone(p.Phone)                         │
│ - Skill: !IsValidSkillLevel(p.SkillLevel.Value)         │
└─────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────┐
│ If any issue found: playersWithIssuesCount++            │
└─────────────────────────────────────────────────────────┘
                              │
                              ▼
              ┌───────────────────────────────┐
              │ Calculate Overview:           │
              │ - IssuePercentage             │
              │ - HealthyPercentage           │
              │ (Check: totalPlayers > 0)     │
              └───────────────────────────────┘
                              │
                              ▼
              ┌───────────────────────────────┐
              │ Generate Recommendations:     │
              │ - potentialDuplicates > 0     │
              │ - MissingPhone > 50%          │
              │ - InactivePlayers > 30%       │
              └───────────────────────────────┘
```

---

## Boundary Analysis

### 1. Tournament Activity (oneYearAgo comparison: `>=`)

| Vị trí | Tournament.StartUtc | So sánh với oneYearAgo | Player Status |
|:-------|:--------------------|:-----------------------|:--------------|
| Dưới biên | `now.AddYears(-1).AddDays(1)` | > oneYearAgo | ✅ Active |
| Ngay biên | `now.AddYears(-1)` (exactly) | = oneYearAgo | ✅ Active (>= passes) |
| Trên biên | `now.AddYears(-1).AddDays(-1)` | < oneYearAgo | ❌ Inactive |

### 2. Recommendations Thresholds

#### MissingPhone > 50% (`> totalPlayers * 0.5`)

| Scenario | MissingPhone / Total | Condition | Recommendation? |
|:---------|:---------------------|:----------|:----------------|
| Dưới biên | 4/10 = 40% | 4 > 5 = false | ❌ NO |
| Ngay biên | 5/10 = 50% | 5 > 5 = false | ❌ NO |
| Trên biên | 6/10 = 60% | 6 > 5 = true | ✅ YES |

#### InactivePlayers > 30% (`> totalPlayers * 0.3`)

| Scenario | Inactive / Total | Condition | Recommendation? |
|:---------|:-----------------|:----------|:----------------|
| Dưới biên | 2/10 = 20% | 2 > 3 = false | ❌ NO |
| Ngay biên | 3/10 = 30% | 3 > 3 = false | ❌ NO |
| Trên biên | 4/10 = 40% | 4 > 3 = true | ✅ YES |

#### PotentialDuplicates > 0

| Scenario | Count | Recommendation? |
|:---------|:------|:----------------|
| Ngay biên | 0 | ❌ NO |
| Trên biên | 1 | ✅ YES |

### 3. Division by Zero (totalPlayers)

| TotalPlayers | IssuePercentage | HealthyPercentage |
|:-------------|:----------------|:------------------|
| 0 | 0 (guard clause) | 0 (guard clause) |
| 1+ | Calculated normally | Calculated normally |

---

## Bảng Test Cases

| ID | Loại | Tên Test Case (Gợi ý đặt tên) | Input Giả định (Mock Data) | Kết quả Mong đợi | Ghi chú |
|:---|:-----|:------------------------------|:---------------------------|:-----------------|:--------|
| 1 | Edge | GetDataQualityReportAsync_WhenNoPlayers_ReturnsZeroStats | DB không có Player nào | `TotalPlayers = 0`, `IssuePercentage = 0`, `HealthyPercentage = 0`, `Recommendations = []` | [Fact] |
| 2 | Happy | GetDataQualityReportAsync_WhenAllPlayersHealthy_ReturnsNoIssues | Players với đầy đủ valid data, có tournament gần đây | `PlayersWithIssues = 0`, `HealthyPlayers = TotalPlayers`, `HealthyPercentage = 100` | [Fact] |
| 3 | Happy | GetDataQualityReportAsync_WhenPlayerMissingEmail_CountsMissingEmail | 1 Player có `Email = null` | `MissingEmail = 1`, `PlayersWithIssues = 1` | [Fact] |
| 4 | Happy | GetDataQualityReportAsync_WhenPlayerMissingPhone_CountsMissingPhone | 1 Player có `Phone = ""` | `MissingPhone = 1` | [Fact] |
| 5 | Happy | GetDataQualityReportAsync_WhenPlayerMissingSkillLevel_CountsMissingSkill | 1 Player có `SkillLevel = null` | `MissingSkillLevel = 1` | [Fact] |
| 6 | Happy | GetDataQualityReportAsync_WhenPlayerMissingCountryOnly_CountsMissingLocation | Player có `Country = null`, `City = "HCM"` | `MissingLocation = 1` | [Fact] |
| 7 | Happy | GetDataQualityReportAsync_WhenPlayerMissingCityOnly_CountsMissingLocation | Player có `Country = "VN"`, `City = null` | `MissingLocation = 1` | [Fact] |
| 8 | Happy | GetDataQualityReportAsync_WhenPlayerHasInvalidEmail_CountsInvalidEmail | Player có `Email = "not-an-email"` | `InvalidEmail = 1` | [Fact] |
| 9 | Happy | GetDataQualityReportAsync_WhenPlayerHasInvalidPhone_CountsInvalidPhone | Player có `Phone = "abc123"` (invalid format) | `InvalidPhone = 1` | [Fact] |
| 10 | Happy | GetDataQualityReportAsync_WhenPlayerHasInvalidSkillLevel_CountsInvalidSkill | Player có `SkillLevel = 999` (ngoài valid range) | `InvalidSkillLevel = 1` | [Fact] |
| 11 | Happy | GetDataQualityReportAsync_WhenDuplicateEmails_CountsPotentialDuplicates | 2 Players có cùng email `test@test.com` | `PotentialDuplicates = 1` (1 group) | [Fact] |
| 12 | Edge | GetDataQualityReportAsync_WhenDuplicateEmailsDifferentCase_CountsAsSameGroup | Player1: `Test@Test.COM`, Player2: `test@test.com` | `PotentialDuplicates = 1` (case-insensitive comparison) | [Fact] |
| 13 | Boundary | GetDataQualityReportAsync_WhenTournamentExactlyOneYearAgo_PlayerIsActive | Tournament.StartUtc = `now.AddYears(-1)` exactly | Player NOT counted in `InactivePlayers` (>= passes) | [Fact] - Ngay biên |
| 14 | Boundary | GetDataQualityReportAsync_WhenTournamentOneYearPlusOneDay_PlayerIsInactive | Tournament.StartUtc = `now.AddYears(-1).AddDays(-1)` | Player IS counted in `InactivePlayers` | [Fact] - Trên biên |
| 15 | Happy | GetDataQualityReportAsync_WhenPlayerNeverPlayed_CountsNeverPlayedTournament | Player không có TournamentPlayers nào | `NeverPlayedTournament = 1` | [Fact] |
| 16 | Boundary | GetDataQualityReportAsync_WhenMissingPhoneExactly50Percent_NoRecommendation | 5/10 players missing phone (exactly 50%) | Recommendations KHÔNG chứa "missing phone numbers" | [Fact] - Ngay biên |
| 17 | Boundary | GetDataQualityReportAsync_WhenMissingPhoneOver50Percent_AddsRecommendation | 6/10 players missing phone (60%) | Recommendations chứa "High rate of missing phone numbers - Consider making Phone required." | [Fact] - Trên biên |
| 18 | Boundary | GetDataQualityReportAsync_WhenInactiveExactly30Percent_NoRecommendation | 3/10 players inactive (exactly 30%) | Recommendations KHÔNG chứa "inactive players" | [Fact] - Ngay biên |
| 19 | Boundary | GetDataQualityReportAsync_WhenInactiveOver30Percent_AddsRecommendation | 4/10 players inactive (40%) | Recommendations chứa "inactive players - Consider archiving old data." | [Fact] - Trên biên |
| 20 | Happy | GetDataQualityReportAsync_WhenHasDuplicates_AddsRecommendation | 2 email groups are duplicates | Recommendations chứa "2 duplicate email groups found - Use 'Merge' tool to fix." | [Fact] |
| 21 | Edge | GetDataQualityReportAsync_WhenNoDuplicates_NoRecommendationForDuplicates | Tất cả emails unique | Recommendations KHÔNG chứa "duplicate" message | [Fact] |
| 22 | Happy | GetDataQualityReportAsync_CalculatesIssuePercentageCorrectly | 3 players with issues / 10 total | `IssuePercentage = 30.0` (Math.Round(..., 2)) | [Fact] |
| 23 | Happy | GetDataQualityReportAsync_CalculatesHealthyPercentageCorrectly | 7 healthy players / 10 total | `HealthyPercentage = 70.0` | [Fact] |
| 24 | Happy | GetDataQualityReportAsync_WhenPlayerHasMultipleIssues_CountsOnceInPlayersWithIssues | Player missing email AND phone AND skill | `PlayersWithIssues = 1` (not 3), but individual counts are 1 each | [Fact] |
| 25 | Happy | GetDataQualityReportAsync_SetsGeneratedAtToCurrentTime | Any data | `GeneratedAt` is approximately `DateTime.UtcNow` (within seconds) | [Fact] |

---

## Chi tiết Mock Setup cho từng Test Case

### Test Case #1: No players
```csharp
[Fact]
public async Task GetDataQualityReportAsync_WhenNoPlayers_ReturnsZeroStats()
{
    // Arrange
    _mockDbContext.Setup(x => x.Players).Returns(MockDbSet(new List<Player>()));

    // Act
    var result = await _adminPlayerService.GetDataQualityReportAsync();

    // Assert
    Assert.Equal(0, result.Overview.TotalPlayers);
    Assert.Equal(0, result.Overview.IssuePercentage);
    Assert.Equal(0, result.Overview.HealthyPercentage);
    Assert.Empty(result.Recommendations);
}
```

### Test Case #11 & #12: Duplicate emails (case sensitivity)
```csharp
[Fact]
public async Task GetDataQualityReportAsync_WhenDuplicateEmailsDifferentCase_CountsAsSameGroup()
{
    // Arrange
    var players = new List<Player>
    {
        new() { Id = 1, Email = "Test@Test.COM", Phone = "123", SkillLevel = 5, Country = "VN", City = "HCM" },
        new() { Id = 2, Email = "test@test.com", Phone = "456", SkillLevel = 5, Country = "VN", City = "HCM" }
    };
    
    _mockDbContext.Setup(x => x.Players).Returns(MockDbSet(players));

    // Act
    var result = await _adminPlayerService.GetDataQualityReportAsync();

    // Assert
    Assert.Equal(1, result.Issues.PotentialDuplicates); // Counted as 1 group (case-insensitive)
}
```

### Test Case #13 & #14: Tournament activity boundary
```csharp
[Fact]
public async Task GetDataQualityReportAsync_WhenTournamentExactlyOneYearAgo_PlayerIsActive()
{
    // Arrange
    var now = DateTime.UtcNow;
    var exactlyOneYearAgo = now.AddYears(-1);
    
    var players = new List<Player>
    {
        new()
        {
            Id = 1,
            Email = "test@test.com",
            Phone = "123",
            SkillLevel = 5,
            Country = "VN",
            City = "HCM",
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
    var result = await _adminPlayerService.GetDataQualityReportAsync();

    // Assert
    Assert.Equal(0, result.Issues.InactivePlayers); // NOT inactive because >= passes
}

[Fact]
public async Task GetDataQualityReportAsync_WhenTournamentOneYearPlusOneDay_PlayerIsInactive()
{
    // Arrange
    var now = DateTime.UtcNow;
    var moreThanOneYearAgo = now.AddYears(-1).AddDays(-1);
    
    var players = new List<Player>
    {
        new()
        {
            Id = 1,
            Email = "test@test.com",
            Phone = "123",
            SkillLevel = 5,
            Country = "VN",
            City = "HCM",
            TournamentPlayers = new List<TournamentPlayer>
            {
                new()
                {
                    Tournament = new Tournament { StartUtc = moreThanOneYearAgo } // PAST boundary
                }
            }
        }
    };
    
    _mockDbContext.Setup(x => x.Players).Returns(MockDbSet(players));

    // Act
    var result = await _adminPlayerService.GetDataQualityReportAsync();

    // Assert
    Assert.Equal(1, result.Issues.InactivePlayers); // IS inactive
}
```

### Test Case #16 & #17: MissingPhone threshold boundary
```csharp
[Fact]
public async Task GetDataQualityReportAsync_WhenMissingPhoneExactly50Percent_NoRecommendation()
{
    // Arrange - 5 out of 10 missing phone (exactly 50%)
    var players = Enumerable.Range(1, 10).Select(i => new Player
    {
        Id = i,
        Email = $"user{i}@test.com",
        Phone = i <= 5 ? null : "12345", // 5 missing
        SkillLevel = 5,
        Country = "VN",
        City = "HCM"
    }).ToList();
    
    _mockDbContext.Setup(x => x.Players).Returns(MockDbSet(players));

    // Act
    var result = await _adminPlayerService.GetDataQualityReportAsync();

    // Assert
    Assert.Equal(5, result.Issues.MissingPhone);
    Assert.DoesNotContain(result.Recommendations, r => r.Contains("missing phone"));
}

[Fact]
public async Task GetDataQualityReportAsync_WhenMissingPhoneOver50Percent_AddsRecommendation()
{
    // Arrange - 6 out of 10 missing phone (60%)
    var players = Enumerable.Range(1, 10).Select(i => new Player
    {
        Id = i,
        Email = $"user{i}@test.com",
        Phone = i <= 6 ? null : "12345", // 6 missing
        SkillLevel = 5,
        Country = "VN",
        City = "HCM"
    }).ToList();
    
    _mockDbContext.Setup(x => x.Players).Returns(MockDbSet(players));

    // Act
    var result = await _adminPlayerService.GetDataQualityReportAsync();

    // Assert
    Assert.Equal(6, result.Issues.MissingPhone);
    Assert.Contains(result.Recommendations, r => r.Contains("High rate of missing phone numbers"));
}
```

### Test Case #24: Multiple issues count once
```csharp
[Fact]
public async Task GetDataQualityReportAsync_WhenPlayerHasMultipleIssues_CountsOnceInPlayersWithIssues()
{
    // Arrange - Player has ALL issues
    var players = new List<Player>
    {
        new()
        {
            Id = 1,
            Email = null,      // Missing
            Phone = null,      // Missing
            SkillLevel = null, // Missing
            Country = null,    // Missing location
            City = null        // Missing location
        }
    };
    
    _mockDbContext.Setup(x => x.Players).Returns(MockDbSet(players));

    // Act
    var result = await _adminPlayerService.GetDataQualityReportAsync();

    // Assert
    Assert.Equal(1, result.Issues.MissingEmail);
    Assert.Equal(1, result.Issues.MissingPhone);
    Assert.Equal(1, result.Issues.MissingSkillLevel);
    Assert.Equal(1, result.Issues.MissingLocation);
    Assert.Equal(1, result.Overview.PlayersWithIssues); // Only counted ONCE
}
```

---

## Tổng kết Coverage

| Nhánh Code | Test Case IDs |
|:-----------|:--------------|
| `players.Count == 0` (no players) | #1 |
| `players.Count > 0` (has players) | #2-#25 |
| `IsNullOrWhiteSpace(p.Email)` = true | #3 |
| `IsNullOrWhiteSpace(p.Phone)` = true | #4, #16, #17 |
| `p.SkillLevel == null` = true | #5 |
| `IsNullOrWhiteSpace(Country)` = true | #6 |
| `IsNullOrWhiteSpace(City)` = true | #7 |
| `!IsValidEmail(p.Email)` = true | #8 |
| `!IsValidPhone(p.Phone)` = true | #9 |
| `!IsValidSkillLevel(p.SkillLevel)` = true | #10 |
| Email frequency > 1 (duplicates) | #11, #12, #20 |
| Email frequency = 1 (unique) | #21 |
| Tournament.StartUtc >= oneYearAgo | #13 |
| Tournament.StartUtc < oneYearAgo | #14 |
| No TournamentPlayers (never played) | #15 |
| `totalPlayers > 0` = false | #1 |
| `totalPlayers > 0` = true | #22, #23 |
| `potentialDuplicates > 0` | #20 |
| `potentialDuplicates == 0` | #21 |
| `MissingPhone > totalPlayers * 0.5` = false | #16 |
| `MissingPhone > totalPlayers * 0.5` = true | #17 |
| `InactivePlayers > totalPlayers * 0.3` = false | #18 |
| `InactivePlayers > totalPlayers * 0.3` = true | #19 |
| Multiple issues per player | #24 |
| GeneratedAt assignment | #25 |

---

## Thống kê

- **Tổng số Test Cases:** 25
- **Happy Path:** 13 (ID #2, #3, #4, #5, #6, #7, #8, #9, #10, #11, #20, #22, #23, #24, #25)
- **Error/Edge Cases:** 4 (ID #1, #12, #15, #21)
- **Boundary Cases:** 8 (ID #13, #14, #16, #17, #18, #19)

**Code Coverage dự kiến:** 100% cho hàm `GetDataQualityReportAsync`

---

## ⚠️ Lưu ý về Logic

### 1. Case-Insensitive Email Comparison
Code sử dụng `StringComparer.OrdinalIgnoreCase` cho dictionary, nên `Test@Test.COM` và `test@test.com` được coi là trùng lặp.

### 2. Email Trimming
Code sử dụng `p.Email.Trim()` trước khi đưa vào dictionary, nên `"test@test.com "` và `"test@test.com"` được coi là giống nhau.

### 3. Inactive vs NeverPlayed
- **Inactive**: Đã từng chơi (có TournamentPlayers) nhưng không có tournament nào >= 1 năm trước
- **NeverPlayed**: Không có TournamentPlayers nào

### 4. Missing Location Logic
Cả Country VÀ City đều phải có giá trị. Nếu một trong hai missing → `MissingLocation++`

---

## Đề xuất cải thiện Code

1. **Thêm null check cho các navigation properties:**
   ```csharp
   // Trong LINQ query
   .Where(p => p.TournamentPlayers != null && p.TournamentPlayers.Any())
   ```

2. **Consider caching validation results:**
   Nếu list players lớn, có thể tính toán parallel:
   ```csharp
   var results = players.AsParallel().Select(p => ValidatePlayer(p)).ToList();
   ```

3. **Configurable thresholds:**
   ```csharp
   private const double InactiveThreshold = 0.3;
   private const double MissingPhoneThreshold = 0.5;
   ```

