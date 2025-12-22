# Test Cases cho `DeactivateUserAsync` - AdminUserService

## Thông tin hàm

**File:** `PoolMateBackend/Services/AdminUserService.cs`

**Signature:**
```csharp
public async Task<Response> DeactivateUserAsync(
    string userId, 
    string adminId, 
    CancellationToken ct)
```

---

## Phân tích Control Flow

Hàm `DeactivateUserAsync` có các nhánh điều kiện sau:

```
┌─────────────────────────────────────────────────────────────┐
│                    DeactivateUserAsync                       │
└─────────────────────────────────────────────────────────────┘
                              │
                              ▼
              ┌───────────────────────────────┐
              │         TRY BLOCK             │
              └───────────────────────────────┘
                              │
                              ▼
              ┌───────────────────────────────┐
              │   userId == adminId ?         │
              │   (Self-deactivation check)   │
              └───────────────────────────────┘
                    │YES              │NO
                    ▼                 ▼
          ┌──────────────────┐  ┌─────────────────────────────┐
          │ Return Error:    │  │ FindByIdAsync(userId)       │
          │ "You cannot      │  └─────────────────────────────┘
          │ deactivate your  │                │
          │ own account."    │        ┌───────┴───────┐
          └──────────────────┘        │NULL           │NOT NULL
                                      ▼               ▼
                          ┌──────────────────┐  ┌─────────────────┐
                          │ Return Error:    │  │ Check LockoutEnd│
                          │ "User not found" │  │ already locked? │
                          └──────────────────┘  └─────────────────┘
                                                        │
                                        ┌───────────────┴───────────────┐
                                        │                               │
                              LockoutEnd.HasValue &&              Otherwise
                              LockoutEnd > UtcNow ?               (not locked)
                                        │YES                            │
                                        ▼                               ▼
                          ┌──────────────────┐            ┌─────────────────────┐
                          │ Return Error:    │            │ Check LockoutEnabled│
                          │ "User is already │            │ (Protected account?)│
                          │ deactivated"     │            └─────────────────────┘
                          └──────────────────┘                      │
                                                        ┌───────────┴───────────┐
                                                        │FALSE                  │TRUE
                                                        ▼                       ▼
                                          ┌──────────────────┐    ┌─────────────────────┐
                                          │ Return Error:    │    │ Set LockoutEnd =    │
                                          │ "Cannot deactiv- │    │ DateTimeOffset.Max  │
                                          │ ate protected    │    │ UpdateSecurityStamp │
                                          │ account..."      │    └─────────────────────┘
                                          └──────────────────┘              │
                                                              ┌─────────────┴─────────────┐
                                                              │                           │
                                                        result.Succeeded?           !Succeeded
                                                              │YES                        │
                                                              ▼                           ▼
                                                ┌──────────────────┐        ┌──────────────────┐
                                                │ GetRolesAsync    │        │ Return Error:    │
                                                │ Log success      │        │ "Failed to       │
                                                │ Return Ok(...)   │        │ deactivate..."   │
                                                └──────────────────┘        └──────────────────┘

              ┌───────────────────────────────┐
              │         CATCH BLOCK           │
              │ Return Error: "Error          │
              │ deactivating user"            │
              └───────────────────────────────┘
```

---

## Boundary Analysis

### 1. LockoutEnd Time Comparison (`> DateTimeOffset.UtcNow`)

| Vị trí | LockoutEnd Value | Condition Result | Flow |
|:-------|:-----------------|:-----------------|:-----|
| Dưới biên | `UtcNow - 1 minute` | `> UtcNow` = false | ✅ Continue processing |
| Ngay biên | `UtcNow` (exactly) | `> UtcNow` = false | ✅ Continue processing |
| Trên biên | `UtcNow + 1 minute` | `> UtcNow` = true | ❌ Already deactivated |

### 2. LockoutEnd Null Handling

| LockoutEnd Value | HasValue | Time Check | Result |
|:-----------------|:---------|:-----------|:-------|
| `null` | `false` | N/A (short-circuit) | ✅ Continue |
| Past time | `true` | `<= UtcNow` | ✅ Continue |
| Future time | `true` | `> UtcNow` | ❌ Already deactivated |
| `MaxValue` | `true` | `> UtcNow` | ❌ Already deactivated |

### 3. String Comparison (userId == adminId)

| userId | adminId | Result |
|:-------|:--------|:-------|
| "user-123" | "user-123" | ❌ Cannot deactivate self |
| "user-123" | "admin-456" | ✅ Continue |
| "" | "" | ❌ Cannot deactivate self |
| null | null | ❌ (equals, but likely throws before) |

---

## Bảng Test Cases

| ID | Loại | Tên Test Case (Gợi ý đặt tên) | Input Giả định (Mock Data) | Kết quả Mong đợi | Ghi chú |
|:---|:-----|:------------------------------|:---------------------------|:-----------------|:--------|
| 1 | Error | DeactivateUserAsync_WhenAdminDeactivatesSelf_ReturnsError | `userId = "admin-123"`, `adminId = "admin-123"` | `Response.Error("You cannot deactivate your own account.")` | [Fact] |
| 2 | Error | DeactivateUserAsync_WhenUserNotFound_ReturnsError | `userId = "non-existent"`, `FindByIdAsync` returns `null` | `Response.Error("User not found")` | [Fact] |
| 3 | Error | DeactivateUserAsync_WhenUserAlreadyDeactivated_ReturnsError | User có `LockoutEnd = DateTimeOffset.UtcNow.AddDays(1)` | `Response.Error("User is already deactivated")` | [Fact] |
| 4 | Boundary | DeactivateUserAsync_WhenLockoutEndIsExactlyNow_ContinuesProcessing | User có `LockoutEnd = DateTimeOffset.UtcNow` (exactly) | Continue processing, không return error "already deactivated" | [Fact] - Ngay biên |
| 5 | Boundary | DeactivateUserAsync_WhenLockoutEndIsInPast_ContinuesProcessing | User có `LockoutEnd = DateTimeOffset.UtcNow.AddMinutes(-1)` | Continue processing | [Fact] - Dưới biên |
| 6 | Boundary | DeactivateUserAsync_WhenLockoutEndIsInFuture_ReturnsError | User có `LockoutEnd = DateTimeOffset.UtcNow.AddMinutes(1)` | `Response.Error("User is already deactivated")` | [Fact] - Trên biên |
| 7 | Edge | DeactivateUserAsync_WhenLockoutEndIsNull_ContinuesProcessing | User có `LockoutEnd = null` | Continue processing (HasValue = false, short-circuit) | [Fact] |
| 8 | Error | DeactivateUserAsync_WhenUserIsProtected_ReturnsError | User có `LockoutEnabled = false` | `Response.Error("Cannot deactivate this user. This is a protected account...")` | [Fact] |
| 9 | Error | DeactivateUserAsync_WhenUpdateSecurityStampFails_ReturnsError | `UpdateSecurityStampAsync` returns `IdentityResult.Failed(...)` | `Response.Error("Failed to deactivate user: {errors}")` | [Fact] |
| 10 | Happy | DeactivateUserAsync_WhenAllValid_ReturnsSuccessWithData | Valid user, `LockoutEnabled = true`, `LockoutEnd = null`, `UpdateSecurityStampAsync` succeeds | `Response.Ok()` với `userId`, `userName`, `deactivatedAt`, `message` | [Fact] |
| 11 | Happy | DeactivateUserAsync_WhenSuccess_SetsLockoutEndToMaxValue | Valid deactivation | Verify `user.LockoutEnd = DateTimeOffset.MaxValue` được set trước khi gọi UpdateSecurityStampAsync | [Fact] |
| 12 | Error | DeactivateUserAsync_WhenExceptionThrown_ReturnsGenericError | `FindByIdAsync` hoặc bất kỳ operation nào throws exception | `Response.Error("Error deactivating user")` | [Fact] |

---

## Chi tiết Mock Setup cho từng Test Case

### Test Case #1: Admin deactivates self
```csharp
[Fact]
public async Task DeactivateUserAsync_WhenAdminDeactivatesSelf_ReturnsError()
{
    // Arrange
    var userId = "admin-123";
    var adminId = "admin-123"; // SAME as userId

    // Act
    var result = await _adminUserService.DeactivateUserAsync(userId, adminId, CancellationToken.None);

    // Assert
    Assert.False(result.IsSuccess);
    Assert.Equal("You cannot deactivate your own account.", result.Message);
    
    // Verify FindByIdAsync was NOT called (short-circuit)
    _mockUserManager.Verify(x => x.FindByIdAsync(It.IsAny<string>()), Times.Never);
}
```

### Test Case #3: Already deactivated
```csharp
[Fact]
public async Task DeactivateUserAsync_WhenUserAlreadyDeactivated_ReturnsError()
{
    // Arrange
    var userId = "user-123";
    var adminId = "admin-456";
    var user = new ApplicationUser
    {
        Id = userId,
        UserName = "testuser",
        LockoutEnd = DateTimeOffset.UtcNow.AddDays(1), // Future = already locked
        LockoutEnabled = true
    };
    
    _mockUserManager.Setup(x => x.FindByIdAsync(userId)).ReturnsAsync(user);

    // Act
    var result = await _adminUserService.DeactivateUserAsync(userId, adminId, CancellationToken.None);

    // Assert
    Assert.False(result.IsSuccess);
    Assert.Equal("User is already deactivated", result.Message);
    
    // Verify UpdateSecurityStampAsync was NOT called
    _mockUserManager.Verify(x => x.UpdateSecurityStampAsync(It.IsAny<ApplicationUser>()), Times.Never);
}
```

### Test Case #4: LockoutEnd exactly at UtcNow (Boundary)
```csharp
[Fact]
public async Task DeactivateUserAsync_WhenLockoutEndIsExactlyNow_ContinuesProcessing()
{
    // Arrange
    var userId = "user-123";
    var adminId = "admin-456";
    var now = DateTimeOffset.UtcNow;
    var user = new ApplicationUser
    {
        Id = userId,
        UserName = "testuser",
        LockoutEnd = now, // EXACTLY at boundary - should NOT be considered "already deactivated"
        LockoutEnabled = true
    };
    
    _mockUserManager.Setup(x => x.FindByIdAsync(userId)).ReturnsAsync(user);
    _mockUserManager.Setup(x => x.UpdateSecurityStampAsync(user)).ReturnsAsync(IdentityResult.Success);
    _mockUserManager.Setup(x => x.GetRolesAsync(user)).ReturnsAsync(new List<string>());

    // Act
    var result = await _adminUserService.DeactivateUserAsync(userId, adminId, CancellationToken.None);

    // Assert
    Assert.True(result.IsSuccess); // Should continue and succeed
}
```

### Test Case #8: Protected account
```csharp
[Fact]
public async Task DeactivateUserAsync_WhenUserIsProtected_ReturnsError()
{
    // Arrange
    var userId = "user-123";
    var adminId = "admin-456";
    var user = new ApplicationUser
    {
        Id = userId,
        UserName = "superadmin",
        LockoutEnd = null,
        LockoutEnabled = false // PROTECTED!
    };
    
    _mockUserManager.Setup(x => x.FindByIdAsync(userId)).ReturnsAsync(user);

    // Act
    var result = await _adminUserService.DeactivateUserAsync(userId, adminId, CancellationToken.None);

    // Assert
    Assert.False(result.IsSuccess);
    Assert.Contains("Cannot deactivate this user", result.Message);
    Assert.Contains("protected account", result.Message);
}
```

### Test Case #9: UpdateSecurityStampAsync fails
```csharp
[Fact]
public async Task DeactivateUserAsync_WhenUpdateSecurityStampFails_ReturnsError()
{
    // Arrange
    var userId = "user-123";
    var adminId = "admin-456";
    var user = new ApplicationUser
    {
        Id = userId,
        UserName = "testuser",
        LockoutEnd = null,
        LockoutEnabled = true
    };
    
    var identityErrors = new[] { new IdentityError { Description = "Database error" } };
    
    _mockUserManager.Setup(x => x.FindByIdAsync(userId)).ReturnsAsync(user);
    _mockUserManager.Setup(x => x.UpdateSecurityStampAsync(user))
        .ReturnsAsync(IdentityResult.Failed(identityErrors));

    // Act
    var result = await _adminUserService.DeactivateUserAsync(userId, adminId, CancellationToken.None);

    // Assert
    Assert.False(result.IsSuccess);
    Assert.Contains("Failed to deactivate user", result.Message);
    Assert.Contains("Database error", result.Message);
}
```

### Test Case #10: Happy path
```csharp
[Fact]
public async Task DeactivateUserAsync_WhenAllValid_ReturnsSuccessWithData()
{
    // Arrange
    var userId = "user-123";
    var adminId = "admin-456";
    var user = new ApplicationUser
    {
        Id = userId,
        UserName = "testuser",
        LockoutEnd = null,
        LockoutEnabled = true
    };
    
    _mockUserManager.Setup(x => x.FindByIdAsync(userId)).ReturnsAsync(user);
    _mockUserManager.Setup(x => x.UpdateSecurityStampAsync(user)).ReturnsAsync(IdentityResult.Success);
    _mockUserManager.Setup(x => x.GetRolesAsync(user)).ReturnsAsync(new List<string> { "Player" });

    // Act
    var result = await _adminUserService.DeactivateUserAsync(userId, adminId, CancellationToken.None);

    // Assert
    Assert.True(result.IsSuccess);
    
    // Verify response data
    var data = (dynamic)result.Data;
    Assert.Equal(userId, data.userId);
    Assert.Equal("testuser", data.userName);
    Assert.NotNull(data.deactivatedAt);
    Assert.Contains("deactivated successfully", data.message);
}
```

### Test Case #11: Verify LockoutEnd is set
```csharp
[Fact]
public async Task DeactivateUserAsync_WhenSuccess_SetsLockoutEndToMaxValue()
{
    // Arrange
    var userId = "user-123";
    var adminId = "admin-456";
    var user = new ApplicationUser
    {
        Id = userId,
        UserName = "testuser",
        LockoutEnd = null,
        LockoutEnabled = true
    };
    
    _mockUserManager.Setup(x => x.FindByIdAsync(userId)).ReturnsAsync(user);
    _mockUserManager.Setup(x => x.UpdateSecurityStampAsync(It.IsAny<ApplicationUser>()))
        .Callback<ApplicationUser>(u => 
        {
            // Verify LockoutEnd is set BEFORE UpdateSecurityStampAsync is called
            Assert.Equal(DateTimeOffset.MaxValue, u.LockoutEnd);
        })
        .ReturnsAsync(IdentityResult.Success);
    _mockUserManager.Setup(x => x.GetRolesAsync(user)).ReturnsAsync(new List<string>());

    // Act
    var result = await _adminUserService.DeactivateUserAsync(userId, adminId, CancellationToken.None);

    // Assert
    Assert.True(result.IsSuccess);
    Assert.Equal(DateTimeOffset.MaxValue, user.LockoutEnd);
}
```

### Test Case #12: Exception handling
```csharp
[Fact]
public async Task DeactivateUserAsync_WhenExceptionThrown_ReturnsGenericError()
{
    // Arrange
    var userId = "user-123";
    var adminId = "admin-456";
    
    _mockUserManager.Setup(x => x.FindByIdAsync(userId))
        .ThrowsAsync(new Exception("Database connection failed"));

    // Act
    var result = await _adminUserService.DeactivateUserAsync(userId, adminId, CancellationToken.None);

    // Assert
    Assert.False(result.IsSuccess);
    Assert.Equal("Error deactivating user", result.Message);
}
```

---

## Tổng kết Coverage

| Nhánh Code | Test Case IDs |
|:-----------|:--------------|
| `userId == adminId` = true | #1 |
| `userId == adminId` = false | #2, #3, #4, #5, #6, #7, #8, #9, #10, #11, #12 |
| `user == null` | #2 |
| `user != null` | #3, #4, #5, #6, #7, #8, #9, #10, #11 |
| `LockoutEnd.HasValue && LockoutEnd > UtcNow` = true | #3, #6 |
| `LockoutEnd.HasValue && LockoutEnd > UtcNow` = false (HasValue = false) | #7, #10, #11 |
| `LockoutEnd.HasValue && LockoutEnd > UtcNow` = false (past time) | #5 |
| `LockoutEnd.HasValue && LockoutEnd > UtcNow` = false (exactly now) | #4 |
| `!user.LockoutEnabled` = true | #8 |
| `!user.LockoutEnabled` = false | #9, #10, #11 |
| `!result.Succeeded` = true | #9 |
| `result.Succeeded` = true | #10, #11 |
| `catch (Exception)` | #12 |

---

## Thống kê

- **Tổng số Test Cases:** 12
- **Happy Path:** 2 (ID #10, #11)
- **Error Cases:** 6 (ID #1, #2, #3, #8, #9, #12)
- **Boundary Cases:** 3 (ID #4, #5, #6)
- **Edge Cases:** 1 (ID #7)

**Code Coverage dự kiến:** 100% cho hàm `DeactivateUserAsync`

---

## ⚠️ Lưu ý về Logic

### 1. Self-Deactivation Protection
- Admin không thể tự khóa chính mình
- Check này được thực hiện ĐẦU TIÊN trước mọi database operation

### 2. Protected Account (LockoutEnabled = false)
- Một số account VIP/Super Admin có `LockoutEnabled = false`
- Những account này KHÔNG THỂ bị deactivate

### 3. LockoutEnd = MaxValue
- Khi deactivate, `LockoutEnd` được set thành `DateTimeOffset.MaxValue`
- Đây là giá trị "vĩnh viễn" - user bị khóa cho đến khi admin reactivate

### 4. Security Stamp Update
- `UpdateSecurityStampAsync` được gọi để invalidate tất cả active sessions
- Điều này đảm bảo user bị log out ngay lập tức từ tất cả devices

---

## Đề xuất cải thiện Code

1. **Null check cho userId và adminId:**
   ```csharp
   if (string.IsNullOrWhiteSpace(userId))
       return Response.Error("UserId is required");
   if (string.IsNullOrWhiteSpace(adminId))
       return Response.Error("AdminId is required");
   ```

2. **Separate UpdateAsync và UpdateSecurityStampAsync:**
   Hiện tại code chỉ gọi `UpdateSecurityStampAsync` nhưng không gọi `UpdateAsync` để lưu `LockoutEnd`. Có thể cần verify logic này.

3. **Thêm audit trail:**
   ```csharp
   user.LastModifiedBy = adminId;
   user.LastModifiedAt = DateTimeOffset.UtcNow;
   ```

