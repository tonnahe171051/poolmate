# Test Cases cho `UpdatePlayerFromUserAsync` - PlayerProfileService

## Thông tin hàm

**File:** `PoolMateBackend/Services/PlayerProfileService.cs`

**Signature:**
```csharp
public async Task UpdatePlayerFromUserAsync(
    ApplicationUser user, 
    CancellationToken ct = default)
```

---

## Phân tích Control Flow

Hàm `UpdatePlayerFromUserAsync` có các nhánh điều kiện sau:

```
┌─────────────────────────────────────────────────────────────┐
│                  UpdatePlayerFromUserAsync                   │
└─────────────────────────────────────────────────────────────┘
                              │
                              ▼
              ┌───────────────────────────────┐
              │ Query Player by user.Id       │
              │ (FirstOrDefaultAsync)         │
              └───────────────────────────────┘
                              │
                              ▼
              ┌───────────────────────────────┐
              │     player == null ?          │
              └───────────────────────────────┘
                    │YES              │NO
                    ▼                 ▼
              ┌──────────┐   ┌─────────────────────────────┐
              │ return   │   │ Build FullName from         │
              │ (no-op)  │   │ FirstName + LastName        │
              └──────────┘   └─────────────────────────────┘
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
                          │ Update player fields:         │
                          │ FullName, Nickname, Email,    │
                          │ Phone, Country, City          │
                          └───────────────────────────────┘
                                         │
                                         ▼
                          ┌───────────────────────────────┐
                          │ Generate baseSlug from        │
                          │ fullNameMap                   │
                          └───────────────────────────────┘
                                         │
                                         ▼
                          ┌───────────────────────────────┐
                          │ player.Slug != baseSlug ?     │
                          └───────────────────────────────┘
                                │YES              │NO
                                ▼                 ▼
              ┌─────────────────────────────┐  ┌─────────────┐
              │ Check slug collision        │  │ Keep old    │
              │ (while loop)                │  │ slug        │
              └─────────────────────────────┘  └─────────────┘
                          │
                          ▼
              ┌───────────────────────────────────┐
              │ while (slug exists in DB          │◄──┐
              │        AND p.Id != player.Id)     │   │
              └───────────────────────────────────┘   │
                    │YES              │NO             │
                    ▼                 │               │
          ┌────────────────────┐      │               │
          │ slug = baseSlug +  │──────┼───────────────┘
          │ "-" + count++      │      │
          └────────────────────┘      │
                                      ▼
              ┌───────────────────────────────┐
              │ player.Slug = finalSlug       │
              └───────────────────────────────┘
                                      │
                                      ▼
              ┌───────────────────────────────┐
              │ SaveChangesAsync              │
              └───────────────────────────────┘
```

---

## Boundary Analysis

### 1. Slug Collision Handling (while loop với điều kiện `p.Id != player.Id`)

| Số lần trùng | Slugs đã tồn tại (của player KHÁC) | finalSlug kết quả |
|:-------------|:-----------------------------------|:------------------|
| 0 lần | (không có slug trùng) | `new-name` |
| 1 lần | `new-name` (player khác) | `new-name-1` |
| 2 lần | `new-name`, `new-name-1` | `new-name-2` |
| n lần | `new-name` đến `new-name-(n-1)` | `new-name-n` |

**Lưu ý quan trọng:** Điều kiện `p.Id != player.Id` đảm bảo player có thể giữ slug của chính mình nếu tên không thay đổi về mặt slug.

### 2. Slug Change Detection

| Old Slug | New FullName | baseSlug (generated) | Slug Changes? |
|:---------|:-------------|:---------------------|:--------------|
| `john-doe` | "John Doe" | `john-doe` | ❌ NO (same) |
| `old-name` | "New Name" | `new-name` | ✅ YES |
| `john-doe` | "JOHN DOE" | `john-doe` | ❌ NO (slug is case-insensitive) |

### 3. FullName Fallback Logic

| FirstName | LastName | Trim Result | UserName | FullName kết quả |
|:----------|:---------|:------------|:---------|:-----------------|
| "John" | "Doe" | "John Doe" | any | "John Doe" |
| "John" | null | "John" | any | "John" |
| null | "Doe" | "Doe" | any | "Doe" |
| " " | " " | "" | "johndoe" | "johndoe" |
| null | null | "" | null | "Unknown Player" |

---

## Bảng Test Cases

| ID | Loại | Tên Test Case (Gợi ý đặt tên) | Input Giả định (Mock Data) | Kết quả Mong đợi | Ghi chú |
|:---|:-----|:------------------------------|:---------------------------|:-----------------|:--------|
| 1 | Edge | UpdatePlayerFromUserAsync_WhenPlayerNotFound_ReturnsWithoutAction | `user.Id = "user-999"`, DB không có Player với UserId = "user-999" | Return ngay, `SaveChangesAsync()` KHÔNG được gọi | [Fact] |
| 2 | Happy | UpdatePlayerFromUserAsync_WhenFirstAndLastNameExist_UsesFullName | `user.FirstName = "John"`, `user.LastName = "Doe"` | `player.FullName = "John Doe"` | [Fact] |
| 3 | Edge | UpdatePlayerFromUserAsync_WhenOnlyFirstNameExists_UsesFirstName | `user.FirstName = "John"`, `user.LastName = null` | `player.FullName = "John"` | [Fact] |
| 4 | Edge | UpdatePlayerFromUserAsync_WhenOnlyLastNameExists_UsesLastName | `user.FirstName = null`, `user.LastName = "Doe"` | `player.FullName = "Doe"` | [Fact] |
| 5 | Edge | UpdatePlayerFromUserAsync_WhenNoNameButUserNameExists_UsesUserName | `user.FirstName = ""`, `user.LastName = ""`, `user.UserName = "johndoe"` | `player.FullName = "johndoe"` | [Fact] |
| 6 | Edge | UpdatePlayerFromUserAsync_WhenNoNameAndNoUserName_UsesUnknownPlayer | `user.FirstName = null`, `user.LastName = null`, `user.UserName = null` | `player.FullName = "Unknown Player"` | [Fact] |
| 7 | Happy | UpdatePlayerFromUserAsync_WhenSlugNotChanged_KeepsOldSlug | Player có `Slug = "john-doe"`, `fullNameMap = "John Doe"` → baseSlug = "john-doe" (same) | `player.Slug` giữ nguyên "john-doe", while loop KHÔNG chạy | [Fact] |
| 8 | Happy | UpdatePlayerFromUserAsync_WhenSlugChangedAndUnique_UsesNewSlug | Player có `Slug = "old-name"`, update thành `fullNameMap = "New Name"` → baseSlug = "new-name" (unique) | `player.Slug = "new-name"` | [Fact] |
| 9 | Boundary | UpdatePlayerFromUserAsync_WhenSlugChangedAndCollides1Time_AppendsNumber1 | Player đổi tên, đã có Player KHÁC với slug "new-name" | `player.Slug = "new-name-1"` | [Fact] |
| 10 | Boundary | UpdatePlayerFromUserAsync_WhenSlugChangedAndCollidesMultipleTimes_AppendsIncrementingNumber | Đã có "new-name", "new-name-1", "new-name-2" của các player KHÁC | `player.Slug = "new-name-3"` | [Fact] |
| 11 | Edge | UpdatePlayerFromUserAsync_WhenNewSlugMatchesOwnOldSlug_DoesNotTriggerCollision | Player có `Id = 1`, `Slug = "john-doe"`, update tên nhưng baseSlug vẫn = "john-doe" | While loop không chạy vì `p.Id != player.Id` loại trừ chính nó | [Fact] |
| 12 | Happy | UpdatePlayerFromUserAsync_UpdatesAllFieldsCorrectly | Valid user với: `Nickname`, `Email`, `PhoneNumber`, `Country`, `City` | Verify tất cả fields của player được cập nhật đúng | [Fact] |
| 13 | Happy | UpdatePlayerFromUserAsync_CallsSaveChangesAsync | Valid update (player found) | Verify `SaveChangesAsync()` được gọi đúng 1 lần | [Fact] |

---

## Chi tiết Mock Setup cho từng Test Case

### Test Case #1: Player not found
```csharp
[Fact]
public async Task UpdatePlayerFromUserAsync_WhenPlayerNotFound_ReturnsWithoutAction()
{
    // Arrange
    var user = new ApplicationUser
    {
        Id = "user-999",
        FirstName = "John",
        LastName = "Doe"
    };
    
    // Mock: No players in DB matching this user
    _mockDbContext.Setup(x => x.Players).Returns(MockDbSet(new List<Player>()));

    // Act
    await _playerProfileService.UpdatePlayerFromUserAsync(user);

    // Assert
    _mockDbContext.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
}
```

### Test Case #7: Slug not changed
```csharp
[Fact]
public async Task UpdatePlayerFromUserAsync_WhenSlugNotChanged_KeepsOldSlug()
{
    // Arrange
    var existingPlayer = new Player
    {
        Id = 1,
        UserId = "user-123",
        FullName = "John Doe",
        Slug = "john-doe"
    };
    var user = new ApplicationUser
    {
        Id = "user-123",
        FirstName = "John",
        LastName = "Doe" // Same name -> same slug
    };
    
    _mockDbContext.Setup(x => x.Players).Returns(MockDbSet(new List<Player> { existingPlayer }));

    // Act
    await _playerProfileService.UpdatePlayerFromUserAsync(user);

    // Assert
    Assert.Equal("john-doe", existingPlayer.Slug); // Unchanged
    // Verify AnyAsync for slug collision was NOT called (because slug didn't change)
}
```

### Test Case #8: Slug changed and unique
```csharp
[Fact]
public async Task UpdatePlayerFromUserAsync_WhenSlugChangedAndUnique_UsesNewSlug()
{
    // Arrange
    var existingPlayer = new Player
    {
        Id = 1,
        UserId = "user-123",
        FullName = "Old Name",
        Slug = "old-name"
    };
    var user = new ApplicationUser
    {
        Id = "user-123",
        FirstName = "New",
        LastName = "Name" // Different name -> different slug
    };
    
    _mockDbContext.Setup(x => x.Players).Returns(MockDbSet(new List<Player> { existingPlayer }));

    // Act
    await _playerProfileService.UpdatePlayerFromUserAsync(user);

    // Assert
    Assert.Equal("new-name", existingPlayer.Slug);
    Assert.Equal("New Name", existingPlayer.FullName);
}
```

### Test Case #9: Slug collision 1 time
```csharp
[Fact]
public async Task UpdatePlayerFromUserAsync_WhenSlugChangedAndCollides1Time_AppendsNumber1()
{
    // Arrange
    var currentPlayer = new Player
    {
        Id = 1,
        UserId = "user-123",
        FullName = "Old Name",
        Slug = "old-name"
    };
    var otherPlayer = new Player
    {
        Id = 2,
        UserId = "user-456",
        FullName = "New Name",
        Slug = "new-name" // This slug already exists
    };
    var user = new ApplicationUser
    {
        Id = "user-123",
        FirstName = "New",
        LastName = "Name"
    };
    
    _mockDbContext.Setup(x => x.Players)
        .Returns(MockDbSet(new List<Player> { currentPlayer, otherPlayer }));

    // Act
    await _playerProfileService.UpdatePlayerFromUserAsync(user);

    // Assert
    Assert.Equal("new-name-1", currentPlayer.Slug); // Appended -1
}
```

### Test Case #10: Slug collision multiple times
```csharp
[Fact]
public async Task UpdatePlayerFromUserAsync_WhenSlugChangedAndCollidesMultipleTimes_AppendsIncrementingNumber()
{
    // Arrange
    var currentPlayer = new Player { Id = 1, UserId = "user-123", Slug = "old-name" };
    var otherPlayers = new List<Player>
    {
        currentPlayer,
        new() { Id = 2, Slug = "new-name" },
        new() { Id = 3, Slug = "new-name-1" },
        new() { Id = 4, Slug = "new-name-2" }
    };
    var user = new ApplicationUser
    {
        Id = "user-123",
        FirstName = "New",
        LastName = "Name"
    };
    
    _mockDbContext.Setup(x => x.Players).Returns(MockDbSet(otherPlayers));

    // Act
    await _playerProfileService.UpdatePlayerFromUserAsync(user);

    // Assert
    Assert.Equal("new-name-3", currentPlayer.Slug);
}
```

### Test Case #12: All fields updated
```csharp
[Fact]
public async Task UpdatePlayerFromUserAsync_UpdatesAllFieldsCorrectly()
{
    // Arrange
    var existingPlayer = new Player
    {
        Id = 1,
        UserId = "user-123",
        FullName = "Old Name",
        Slug = "old-name",
        Nickname = "OldNick",
        Email = "old@email.com",
        Phone = "111",
        Country = "OldCountry",
        City = "OldCity"
    };
    var user = new ApplicationUser
    {
        Id = "user-123",
        FirstName = "New",
        LastName = "Name",
        Nickname = "NewNick",
        Email = "new@email.com",
        PhoneNumber = "999",
        Country = "NewCountry",
        City = "NewCity"
    };
    
    _mockDbContext.Setup(x => x.Players).Returns(MockDbSet(new List<Player> { existingPlayer }));

    // Act
    await _playerProfileService.UpdatePlayerFromUserAsync(user);

    // Assert
    Assert.Equal("New Name", existingPlayer.FullName);
    Assert.Equal("NewNick", existingPlayer.Nickname);
    Assert.Equal("new@email.com", existingPlayer.Email);
    Assert.Equal("999", existingPlayer.Phone);
    Assert.Equal("NewCountry", existingPlayer.Country);
    Assert.Equal("NewCity", existingPlayer.City);
}
```

---

## Tổng kết Coverage

| Nhánh Code | Test Case IDs |
|:-----------|:--------------|
| `player == null` (return early) | #1 |
| `player != null` (continue) | #2, #3, #4, #5, #6, #7, #8, #9, #10, #11, #12, #13 |
| `fullNameMap` có giá trị từ FirstName + LastName | #2, #3, #4 |
| `fullNameMap` rỗng → dùng UserName | #5 |
| `fullNameMap` rỗng, UserName null → "Unknown Player" | #6 |
| `player.Slug != baseSlug` = false (no change) | #7 |
| `player.Slug != baseSlug` = true (slug changes) | #8, #9, #10, #11 |
| While loop không chạy (slug unique) | #8 |
| While loop chạy 1 lần | #9 |
| While loop chạy nhiều lần | #10 |
| `p.Id != player.Id` filter | #9, #10, #11 |
| Field mapping (all properties) | #12 |
| SaveChangesAsync called | #13 |

---

## So sánh với `CreatePlayerProfileAsync`

| Khác biệt | CreatePlayerProfileAsync | UpdatePlayerFromUserAsync |
|:----------|:-------------------------|:--------------------------|
| Return type | `CreatePlayerProfileResponseDto?` | `void` (Task) |
| Player not found | Throw exception (user already has profile) | Return silently (no-op) |
| Slug collision check | `p.Slug == finalSlug` (tất cả) | `p.Slug == finalSlug && p.Id != player.Id` (loại trừ chính mình) |
| Slug change detection | N/A (always generate new) | `player.Slug != baseSlug` (chỉ update nếu khác) |

---

## Thống kê

- **Tổng số Test Cases:** 13
- **Happy Path:** 4 (ID #2, #7, #8, #12, #13)
- **Error/Edge Cases:** 6 (ID #1, #3, #4, #5, #6, #11)
- **Boundary Cases:** 2 (ID #9, #10)

**Code Coverage dự kiến:** 100% cho hàm `UpdatePlayerFromUserAsync`

---

## ⚠️ Đề xuất cải thiện Code

1. **Thêm null check cho `user` parameter:**
   ```csharp
   ArgumentNullException.ThrowIfNull(user);
   ```
   
   Hiện tại nếu `user = null`, code sẽ crash khi truy cập `user.Id`.

2. **Giới hạn số lần retry slug (giống CreatePlayerProfileAsync):**
   ```csharp
   int maxRetries = 100;
   while (await _db.Players.AnyAsync(...) && count < maxRetries)
   {
       // ...
   }
   ```

3. **Logging khi slug thay đổi:**
   ```csharp
   if (player.Slug != baseSlug)
   {
       _logger.LogInformation("Updating slug for player {PlayerId} from {OldSlug} to {NewSlug}", 
           player.Id, player.Slug, finalSlug);
       // ...
   }
   ```

