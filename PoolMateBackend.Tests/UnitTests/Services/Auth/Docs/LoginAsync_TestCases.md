# Test Cases cho `LoginAsync` - AuthService

## Thông tin hàm

**File:** `PoolMateBackend/Services/AuthService.cs`

**Signature:**
```csharp
public async Task<(string Token, DateTime Exp, string UserId, string? UserName, string? Email, IList<string> Roles)?>
    LoginAsync(LoginModel model, CancellationToken ct = default)
```

---

## Phân tích Control Flow

Hàm `LoginAsync` có các nhánh điều kiện sau:

1. **Null check**: `ArgumentNullException.ThrowIfNull(model)` - throw nếu model null
2. **User not found**: `user is null` → throw InvalidOperationException
3. **Account locked**: `user.LockoutEnd.HasValue && user.LockoutEnd.Value > DateTimeOffset.UtcNow` → throw
4. **Wrong password**: `!CheckPasswordAsync()` → throw
5. **Email not confirmed**: `!user.EmailConfirmed` → throw
6. **Happy path**: Tất cả pass → return token

---

## Boundary Analysis

- **LockoutEnd**: So sánh `>` với `DateTimeOffset.UtcNow`
  - **Trên biên:** `LockoutEnd = UtcNow + 1 phút` (locked)
  - **Ngay biên:** `LockoutEnd = UtcNow` (NOT locked - vì dùng `>`, không phải `>=`)
  - **Dưới biên:** `LockoutEnd = UtcNow - 1 phút` (NOT locked)

---

## Bảng Test Cases

| ID | Loại | Tên Test Case (Gợi ý đặt tên) | Input Giả định (Mock Data) | Kết quả Mong đợi | Ghi chú |
|:---|:-----|:------------------------------|:---------------------------|:-----------------|:--------|
| 1 | Error | LoginAsync_WhenModelIsNull_ThrowsArgumentNullException | `model = null` | Throw `ArgumentNullException` | [Fact] |
| 2 | Error | LoginAsync_WhenUsernameIsNullOrEmpty_ThrowsInvalidOperationException | `model.Username = null` hoặc `""`, `FindByNameAsync` returns `null` | Throw `InvalidOperationException("Invalid username or password.")` | [Theory] với `[InlineData(null)]`, `[InlineData("")]`, `[InlineData("   ")]` |
| 3 | Error | LoginAsync_WhenUserNotFound_ThrowsInvalidOperationException | `model.Username = "nonexistent"`, `FindByNameAsync` returns `null` | Throw `InvalidOperationException("Invalid username or password.")` | [Fact] |
| 4 | Boundary | LoginAsync_WhenLockoutEndIsInFuture_ThrowsInvalidOperationException | `user.LockoutEnd = DateTimeOffset.UtcNow.AddMinutes(1)` | Throw `InvalidOperationException("This account has been locked...")` | [Fact] - Trên biên (locked) |
| 5 | Boundary | LoginAsync_WhenLockoutEndEqualsNow_ReturnsToken | `user.LockoutEnd = DateTimeOffset.UtcNow` (exactly), password correct, email confirmed | Return valid token tuple | [Fact] - Ngay biên (NOT locked vì `>` not `>=`) |
| 6 | Boundary | LoginAsync_WhenLockoutEndIsInPast_ContinuesLogin | `user.LockoutEnd = DateTimeOffset.UtcNow.AddMinutes(-1)`, password correct, email confirmed | Return valid token tuple | [Fact] - Dưới biên |
| 7 | Happy | LoginAsync_WhenLockoutEndIsNull_ContinuesLogin | `user.LockoutEnd = null`, password correct, email confirmed | Return valid token tuple | [Fact] - LockoutEnd không có giá trị |
| 8 | Error | LoginAsync_WhenPasswordIsIncorrect_ThrowsInvalidOperationException | `CheckPasswordAsync` returns `false` | Throw `InvalidOperationException("Invalid username or password.")` | [Fact] |
| 9 | Error | LoginAsync_WhenEmailNotConfirmed_ThrowsInvalidOperationException | `user.EmailConfirmed = false`, password correct | Throw `InvalidOperationException("Email is not confirmed.")` | [Fact] |
| 10 | Happy | LoginAsync_WhenAllValid_ReturnsTokenWithCorrectData | User exists, not locked, password correct, email confirmed, có roles | Return tuple với Token, Exp, UserId, UserName, Email, Roles đầy đủ | [Fact] |
| 11 | Happy | LoginAsync_WhenUserHasNoRoles_ReturnsTokenWithEmptyRoles | User hợp lệ, `GetRolesAsync` returns empty list | Return tuple với `Roles = []` | [Fact] |
| 12 | Happy | LoginAsync_WhenUserHasMultipleRoles_ReturnsAllRoles | User hợp lệ, `GetRolesAsync` returns `["Admin", "Player"]` | Return tuple với tất cả roles trong claims | [Fact] |
| 13 | Edge | LoginAsync_WhenUsernameHasWhitespace_TrimsAndFindsUser | `model.Username = "  validuser  "`, user exists | Trim username trước khi tìm, return token | [Fact] - Test `.Trim()` |
| 14 | Edge | LoginAsync_WhenUserNameOrEmailIsNull_ReturnsEmptyStringInClaims | `user.UserName = null`, `user.Email = null` (but Id exists) | Return token với claims chứa empty string cho Name và Email | [Fact] - Test null-coalescing `??` |

---

## Chi tiết Mock Setup cho từng Test Case

### Test Case #1: Model is null
```csharp
// Arrange
LoginModel model = null;

// Act & Assert
await Assert.ThrowsAsync<ArgumentNullException>(() => _authService.LoginAsync(model));
```

### Test Case #2: Username null/empty/whitespace (Theory)
```csharp
[Theory]
[InlineData(null)]
[InlineData("")]
[InlineData("   ")]
public async Task LoginAsync_WhenUsernameIsNullOrEmpty_ThrowsInvalidOperationException(string username)
{
    // Arrange
    var model = new LoginModel { Username = username, Password = "any" };
    _mockUserManager.Setup(x => x.FindByNameAsync(It.IsAny<string>()))
        .ReturnsAsync((ApplicationUser)null);

    // Act & Assert
    var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => _authService.LoginAsync(model));
    Assert.Equal("Invalid username or password.", ex.Message);
}
```

### Test Case #4: Lockout in future (Boundary - Trên biên)
```csharp
// Arrange
var user = new ApplicationUser 
{ 
    Id = "user-id",
    UserName = "testuser",
    LockoutEnd = DateTimeOffset.UtcNow.AddMinutes(1) // LOCKED
};
_mockUserManager.Setup(x => x.FindByNameAsync("testuser")).ReturnsAsync(user);

// Act & Assert
var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => _authService.LoginAsync(model));
Assert.Equal("This account has been locked. Please contact administrator.", ex.Message);
```

### Test Case #5: Lockout equals now (Boundary - Ngay biên)
```csharp
// Arrange
var now = DateTimeOffset.UtcNow;
var user = new ApplicationUser 
{ 
    Id = "user-id",
    UserName = "testuser",
    LockoutEnd = now, // NOT LOCKED (vì dùng >, không phải >=)
    EmailConfirmed = true
};
// ... setup password check returns true, roles, config
```

### Test Case #10: Happy path with all data
```csharp
// Arrange
var user = new ApplicationUser 
{ 
    Id = "user-123",
    UserName = "testuser",
    Email = "test@example.com",
    LockoutEnd = null,
    EmailConfirmed = true
};
var roles = new List<string> { "Player" };

_mockUserManager.Setup(x => x.FindByNameAsync("testuser")).ReturnsAsync(user);
_mockUserManager.Setup(x => x.CheckPasswordAsync(user, "password123")).ReturnsAsync(true);
_mockUserManager.Setup(x => x.GetRolesAsync(user)).ReturnsAsync(roles);
// Setup JWT config

// Act
var result = await _authService.LoginAsync(new LoginModel { Username = "testuser", Password = "password123" });

// Assert
Assert.NotNull(result);
Assert.Equal("user-123", result.Value.UserId);
Assert.Equal("testuser", result.Value.UserName);
Assert.Equal("test@example.com", result.Value.Email);
Assert.Contains("Player", result.Value.Roles);
Assert.True(result.Value.Exp > DateTime.UtcNow);
```

---

## Tổng kết Coverage

| Nhánh Code | Test Case IDs |
|:-----------|:--------------|
| `ArgumentNullException.ThrowIfNull` | #1 |
| `user is null` | #2, #3 |
| `LockoutEnd.HasValue && > UtcNow` (true) | #4 |
| `LockoutEnd.HasValue && > UtcNow` (false - equal) | #5 |
| `LockoutEnd.HasValue && > UtcNow` (false - past) | #6 |
| `LockoutEnd.HasValue` (false) | #7 |
| `!CheckPasswordAsync` (true) | #8 |
| `!EmailConfirmed` (true) | #9 |
| Happy path + Roles loop | #10, #11, #12 |
| `Username?.Trim()` | #13 |
| `user.UserName ?? string.Empty` | #14 |

---

## Thống kê

- **Tổng số Test Cases:** 14
- **Happy Path:** 4 (ID #7, #10, #11, #12)
- **Error Cases:** 5 (ID #1, #2, #3, #8, #9)
- **Boundary Cases:** 3 (ID #4, #5, #6)
- **Edge Cases:** 2 (ID #13, #14)

**Code Coverage dự kiến:** 100% cho hàm `LoginAsync`

