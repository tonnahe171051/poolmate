# Test Cases cho `CreatePlayerProfileAsync` - PlayerProfileService

## Thông tin hàm

**File:** `PoolMateBackend/Services/PlayerProfileService.cs`

**Signature:**
```csharp
public async Task<CreatePlayerProfileResponseDto?> CreatePlayerProfileAsync(
    string userId,
    ApplicationUser user,
    CancellationToken ct = default)
```

---

## Phân tích Control Flow

Hàm `CreatePlayerProfileAsync` có các nhánh điều kiện sau:

```
┌─────────────────────────────────────────────────────────────┐
│                  CreatePlayerProfileAsync                    │
└─────────────────────────────────────────────────────────────┘
                              │
                              ▼
              ┌───────────────────────────────┐
              │ string.IsNullOrWhiteSpace     │
              │ (userId) ?                    │
              └───────────────────────────────┘
                    │YES              │NO
                    ▼                 ▼
              ┌──────────┐   ┌─────────────────────────────┐
              │ return   │   │ Check if player exists      │
              │ null     │   │ in database                 │
              └──────────┘   └─────────────────────────────┘
                                          │
                              ┌───────────┴───────────┐
                              │YES                    │NO
                              ▼                       ▼
                    ┌──────────────────┐    ┌─────────────────────┐
                    │ THROW            │    │ Build FullName      │
                    │ InvalidOperation │    │ from FirstName +    │
                    │ Exception        │    │ LastName            │
                    └──────────────────┘    └─────────────────────┘
                                                      │
                                                      ▼
                                      ┌───────────────────────────────┐
                                      │ string.IsNullOrWhiteSpace     │
                                      │ (fullNameMap) ?               │
                                      └───────────────────────────────┘
                                            │YES              │NO
                                            ▼                 ▼
                                ┌────────────────────┐  ┌─────────────┐
                                │ Use UserName ??    │  │ Use         │
                                │ "Unknown Player"   │  │ fullNameMap │
                                └────────────────────┘  └─────────────┘
                                            │                 │
                                            └────────┬────────┘
                                                     ▼
                                      ┌───────────────────────────────┐
                                      │ Generate Slug from fullName   │
                                      └───────────────────────────────┘
                                                     │
                                                     ▼
                                      ┌───────────────────────────────┐
                                      │ while (slug exists in DB)     │◄──┐
                                      └───────────────────────────────┘   │
                                            │YES              │NO         │
                                            ▼                 │           │
                                ┌────────────────────┐        │           │
                                │ slug = baseSlug +  │────────┼───────────┘
                                │ "-" + count++      │        │
                                └────────────────────┘        │
                                                              ▼
                                      ┌───────────────────────────────┐
                                      │ Create Player entity          │
                                      │ Add to DbContext              │
                                      │ SaveChangesAsync              │
                                      │ Return DTO                    │
                                      └───────────────────────────────┘
```

---

## Boundary Analysis

### 1. Slug Collision Handling (while loop)

| Số lần trùng | Slugs đã tồn tại trong DB | finalSlug kết quả |
|:-------------|:--------------------------|:------------------|
| 0 lần | (không có) | `john-doe` |
| 1 lần | `john-doe` | `john-doe-1` |
| 2 lần | `john-doe`, `john-doe-1` | `john-doe-2` |
| 3 lần | `john-doe`, `john-doe-1`, `john-doe-2` | `john-doe-3` |
| n lần | `john-doe` đến `john-doe-(n-1)` | `john-doe-n` |

### 2. FullName Fallback Logic

| FirstName | LastName | Trim Result | UserName | FullName kết quả |
|:----------|:---------|:------------|:---------|:-----------------|
| "John" | "Doe" | "John Doe" | any | "John Doe" |
| "John" | null | "John" | any | "John" |
| null | "Doe" | "Doe" | any | "Doe" |
| " " | " " | "" | "johndoe" | "johndoe" |
| null | null | "" | "johndoe" | "johndoe" |
| "" | "" | "" | null | "Unknown Player" |
| null | null | "" | null | "Unknown Player" |

### 3. userId Validation

| userId value | IsNullOrWhiteSpace | Result |
|:-------------|:-------------------|:-------|
| `null` | true | return null |
| `""` | true | return null |
| `"   "` | true | return null |
| `"user-123"` | false | continue processing |

---

## Bảng Test Cases

| ID | Loại | Tên Test Case (Gợi ý đặt tên) | Input Giả định (Mock Data) | Kết quả Mong đợi | Ghi chú |
|:---|:-----|:------------------------------|:---------------------------|:-----------------|:--------|
| 1 | Error | CreatePlayerProfileAsync_WhenUserIdIsNullOrWhiteSpace_ReturnsNull | `userId = null` hoặc `""` hoặc `"   "` | Return `null` | [Theory] với `[InlineData(null)]`, `[InlineData("")]`, `[InlineData("   ")]` |
| 2 | Error | CreatePlayerProfileAsync_WhenUserAlreadyHasProfile_ThrowsInvalidOperationException | `userId = "user-123"`, DB đã có Player với UserId = "user-123" | Throw `InvalidOperationException("User already has a player profile.")` | [Fact] |
| 3 | Happy | CreatePlayerProfileAsync_WhenFirstAndLastNameExist_UsesFullName | `user.FirstName = "John"`, `user.LastName = "Doe"` | `FullName = "John Doe"` | [Fact] |
| 4 | Edge | CreatePlayerProfileAsync_WhenOnlyFirstNameExists_UsesFirstName | `user.FirstName = "John"`, `user.LastName = null` | `FullName = "John"` | [Fact] |
| 5 | Edge | CreatePlayerProfileAsync_WhenOnlyLastNameExists_UsesLastName | `user.FirstName = null`, `user.LastName = "Doe"` | `FullName = "Doe"` | [Fact] |
| 6 | Edge | CreatePlayerProfileAsync_WhenNoNameButUserNameExists_UsesUserName | `user.FirstName = ""`, `user.LastName = ""`, `user.UserName = "johndoe"` | `FullName = "johndoe"` | [Fact] |
| 7 | Edge | CreatePlayerProfileAsync_WhenNoNameAndNoUserName_UsesUnknownPlayer | `user.FirstName = null`, `user.LastName = null`, `user.UserName = null` | `FullName = "Unknown Player"` | [Fact] |
| 8 | Happy | CreatePlayerProfileAsync_WhenSlugIsUnique_UsesBaseSlug | Không có Player nào với slug "john-doe" | `Slug = "john-doe"` (baseSlug) | [Fact] |
| 9 | Boundary | CreatePlayerProfileAsync_WhenSlugCollides1Time_AppendsNumber1 | Đã có Player với slug "john-doe" | `Slug = "john-doe-1"` | [Fact] |
| 10 | Boundary | CreatePlayerProfileAsync_WhenSlugCollidesMultipleTimes_AppendsIncrementingNumber | Đã có "john-doe", "john-doe-1", "john-doe-2" | `Slug = "john-doe-3"` | [Fact] |
| 11 | Happy | CreatePlayerProfileAsync_WhenAllValid_SavesPlayerToDatabase | Valid user data | Verify `_db.Players.Add()` được gọi 1 lần, `SaveChangesAsync()` được gọi 1 lần | [Fact] |
| 12 | Happy | CreatePlayerProfileAsync_WhenAllValid_ReturnsCorrectDto | Valid user data với đầy đủ fields | Return DTO với: `FullName`, `Nickname`, `Email`, `Phone`, `Country`, `City`, `CreatedAt` đúng, `Message = "Player profile created automatically from account info"` | [Fact] |
| 13 | Edge | CreatePlayerProfileAsync_VerifyPlayerEntityMappedCorrectly | `user` với `Email`, `Nickname`, `PhoneNumber`, `Country`, `City` | Verify entity có: `UserId = userId`, `Email = user.Email`, `Nickname = user.Nickname`, `Phone = user.PhoneNumber`, `Country = user.Country`, `City = user.City`, `SkillLevel = null` | [Fact] |

---

## Chi tiết Mock Setup cho từng Test Case

### Test Case #1: UserId null/empty/whitespace
```csharp
[Theory]
[InlineData(null)]
[InlineData("")]
[InlineData("   ")]
public async Task CreatePlayerProfileAsync_WhenUserIdIsNullOrWhiteSpace_ReturnsNull(string userId)
{
    // Arrange
    var user = new ApplicationUser { FirstName = "John", LastName = "Doe" };

    // Act
    var result = await _playerProfileService.CreatePlayerProfileAsync(userId, user);

    // Assert
    Assert.Null(result);
    
    // Verify DB was NOT called
    _mockDbContext.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
}
```

### Test Case #2: User already has profile
```csharp
[Fact]
public async Task CreatePlayerProfileAsync_WhenUserAlreadyHasProfile_ThrowsInvalidOperationException()
{
    // Arrange
    var userId = "user-123";
    var existingPlayer = new Player { UserId = userId, FullName = "Existing" };
    var user = new ApplicationUser { FirstName = "John", LastName = "Doe" };
    
    _mockDbContext.Setup(x => x.Players).Returns(MockDbSet(new List<Player> { existingPlayer }));

    // Act & Assert
    var ex = await Assert.ThrowsAsync<InvalidOperationException>(
        () => _playerProfileService.CreatePlayerProfileAsync(userId, user));
    Assert.Equal("User already has a player profile.", ex.Message);
}
```

### Test Case #6: Fallback to UserName
```csharp
[Fact]
public async Task CreatePlayerProfileAsync_WhenNoNameButUserNameExists_UsesUserName()
{
    // Arrange
    var userId = "user-123";
    var user = new ApplicationUser
    {
        FirstName = "",
        LastName = "",
        UserName = "johndoe"
    };
    
    _mockDbContext.Setup(x => x.Players).Returns(MockDbSet(new List<Player>()));

    // Act
    var result = await _playerProfileService.CreatePlayerProfileAsync(userId, user);

    // Assert
    Assert.NotNull(result);
    Assert.Equal("johndoe", result.FullName);
}
```

### Test Case #7: Fallback to "Unknown Player"
```csharp
[Fact]
public async Task CreatePlayerProfileAsync_WhenNoNameAndNoUserName_UsesUnknownPlayer()
{
    // Arrange
    var userId = "user-123";
    var user = new ApplicationUser
    {
        FirstName = null,
        LastName = null,
        UserName = null
    };
    
    _mockDbContext.Setup(x => x.Players).Returns(MockDbSet(new List<Player>()));

    // Act
    var result = await _playerProfileService.CreatePlayerProfileAsync(userId, user);

    // Assert
    Assert.NotNull(result);
    Assert.Equal("Unknown Player", result.FullName);
}
```

### Test Case #9: Slug collision 1 time
```csharp
[Fact]
public async Task CreatePlayerProfileAsync_WhenSlugCollides1Time_AppendsNumber1()
{
    // Arrange
    var userId = "user-new";
    var existingPlayer = new Player 
    { 
        UserId = "user-old", 
        FullName = "John Doe", 
        Slug = "john-doe" // Slug already exists
    };
    var user = new ApplicationUser { FirstName = "John", LastName = "Doe" };
    
    _mockDbContext.Setup(x => x.Players).Returns(MockDbSet(new List<Player> { existingPlayer }));
    
    Player capturedPlayer = null;
    _mockDbSet.Setup(x => x.Add(It.IsAny<Player>()))
        .Callback<Player>(p => capturedPlayer = p);

    // Act
    var result = await _playerProfileService.CreatePlayerProfileAsync(userId, user);

    // Assert
    Assert.NotNull(capturedPlayer);
    Assert.Equal("john-doe-1", capturedPlayer.Slug);
}
```

### Test Case #10: Slug collision multiple times
```csharp
[Fact]
public async Task CreatePlayerProfileAsync_WhenSlugCollidesMultipleTimes_AppendsIncrementingNumber()
{
    // Arrange
    var userId = "user-new";
    var existingPlayers = new List<Player>
    {
        new() { UserId = "user-1", Slug = "john-doe" },
        new() { UserId = "user-2", Slug = "john-doe-1" },
        new() { UserId = "user-3", Slug = "john-doe-2" }
    };
    var user = new ApplicationUser { FirstName = "John", LastName = "Doe" };
    
    _mockDbContext.Setup(x => x.Players).Returns(MockDbSet(existingPlayers));
    
    Player capturedPlayer = null;
    _mockDbSet.Setup(x => x.Add(It.IsAny<Player>()))
        .Callback<Player>(p => capturedPlayer = p);

    // Act
    var result = await _playerProfileService.CreatePlayerProfileAsync(userId, user);

    // Assert
    Assert.NotNull(capturedPlayer);
    Assert.Equal("john-doe-3", capturedPlayer.Slug);
}
```

### Test Case #12: Full DTO mapping verification
```csharp
[Fact]
public async Task CreatePlayerProfileAsync_WhenAllValid_ReturnsCorrectDto()
{
    // Arrange
    var userId = "user-123";
    var user = new ApplicationUser
    {
        FirstName = "John",
        LastName = "Doe",
        Email = "john@example.com",
        Nickname = "JD",
        PhoneNumber = "1234567890",
        Country = "Vietnam",
        City = "Ho Chi Minh"
    };
    
    _mockDbContext.Setup(x => x.Players).Returns(MockDbSet(new List<Player>()));

    // Act
    var result = await _playerProfileService.CreatePlayerProfileAsync(userId, user);

    // Assert
    Assert.NotNull(result);
    Assert.Equal("John Doe", result.FullName);
    Assert.Equal("JD", result.Nickname);
    Assert.Equal("john@example.com", result.Email);
    Assert.Equal("1234567890", result.Phone);
    Assert.Equal("Vietnam", result.Country);
    Assert.Equal("Ho Chi Minh", result.City);
    Assert.Equal("Player profile created automatically from account info", result.Message);
    Assert.True(result.CreatedAt <= DateTime.UtcNow);
    Assert.True(result.CreatedAt > DateTime.UtcNow.AddSeconds(-5)); // Created within last 5 seconds
}
```

---

## Tổng kết Coverage

| Nhánh Code | Test Case IDs |
|:-----------|:--------------|
| `string.IsNullOrWhiteSpace(userId)` = true | #1 |
| `string.IsNullOrWhiteSpace(userId)` = false | #2, #3, #4, #5, #6, #7, #8, #9, #10, #11, #12, #13 |
| `exists == true` (user has profile) | #2 |
| `exists == false` | #3, #4, #5, #6, #7, #8, #9, #10, #11, #12, #13 |
| `fullNameMap` có giá trị từ FirstName + LastName | #3, #4, #5 |
| `fullNameMap` rỗng → dùng UserName | #6 |
| `fullNameMap` rỗng, UserName null → "Unknown Player" | #7 |
| Slug không trùng (while loop không chạy) | #8 |
| Slug trùng 1 lần (while loop chạy 1 lần) | #9 |
| Slug trùng nhiều lần (while loop chạy n lần) | #10 |
| Entity creation & DB save | #11, #12, #13 |
| DTO mapping | #12 |
| Entity property mapping | #13 |

---

## Thống kê

- **Tổng số Test Cases:** 13
- **Happy Path:** 4 (ID #3, #8, #11, #12)
- **Error Cases:** 2 (ID #1, #2)
- **Boundary Cases:** 2 (ID #9, #10)
- **Edge Cases:** 5 (ID #4, #5, #6, #7, #13)

**Code Coverage dự kiến:** 100% cho hàm `CreatePlayerProfileAsync`

---

## ⚠️ Đề xuất cải thiện Code

1. **Thêm null check cho `user` parameter:**
   ```csharp
   ArgumentNullException.ThrowIfNull(user);
   ```
   
   Hiện tại nếu `user = null`, code sẽ crash khi truy cập `user.FirstName`.

2. **Giới hạn số lần retry slug:**
   ```csharp
   int maxRetries = 100;
   while (await _db.Players.AnyAsync(p => p.Slug == finalSlug, ct) && count < maxRetries)
   {
       finalSlug = $"{baseSlug}-{count}";
       count++;
   }
   if (count >= maxRetries)
       throw new InvalidOperationException("Unable to generate unique slug");
   ```
   
   Tránh infinite loop trong trường hợp cực kỳ hiếm.

3. **Consider using GUIDs for slug uniqueness:**
   ```csharp
   finalSlug = $"{baseSlug}-{Guid.NewGuid().ToString("N").Substring(0, 6)}";
   ```
   
   Giảm số lần query DB để check collision.

