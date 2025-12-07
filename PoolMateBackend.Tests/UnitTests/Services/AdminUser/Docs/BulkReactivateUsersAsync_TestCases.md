# Test Cases cho `BulkReactivateUsersAsync` - AdminUserService

## Thông tin hàm

**File:** `PoolMateBackend/Services/AdminUserService.cs`

**Signature:**
```csharp
public async Task<Response> BulkReactivateUsersAsync(
    BulkReactivateUsersDto request, 
    CancellationToken ct)
```

---

## Phân tích Control Flow

Hàm `BulkReactivateUsersAsync` có cấu trúc tương tự `BulkDeactivateUsersAsync` nhưng với logic ngược lại (mở khóa thay vì khóa):

```
┌─────────────────────────────────────────────────────────────┐
│                  BulkReactivateUsersAsync                    │
└─────────────────────────────────────────────────────────────┘
                              │
                              ▼
              ┌───────────────────────────────┐
              │       OUTER TRY BLOCK         │
              └───────────────────────────────┘
                              │
                              ▼
              ┌───────────────────────────────┐
              │ Initialize counters:          │
              │ successCount, failedCount,    │
              │ skippedCount, results list    │
              └───────────────────────────────┘
                              │
                              ▼
              ┌───────────────────────────────┐
              │ foreach (userId in UserIds)   │◄──────────────────────┐
              └───────────────────────────────┘                       │
                              │                                       │
                              ▼                                       │
              ┌───────────────────────────────┐                       │
              │       INNER TRY BLOCK         │                       │
              └───────────────────────────────┘                       │
                              │                                       │
                              ▼                                       │
              ┌───────────────────────────────┐                       │
              │ FindByIdAsync(userId)         │                       │
              └───────────────────────────────┘                       │
                              │                                       │
                      ┌───────┴───────┐                               │
                      │NULL           │NOT NULL                       │
                      ▼               ▼                               │
          ┌──────────────────┐  ┌─────────────────────────────┐       │
          │ Add to results:  │  │ Check if user is currently  │       │
          │ Status=Failed    │  │ deactivated                 │       │
          │ "User not found" │  └─────────────────────────────┘       │
          │ failedCount++    │                │                       │
          │ continue;        │──────┐         │                       │
          └──────────────────┘      │         │                       │
                                    │         ▼                       │
                                    │ ┌───────────────────────────────┐
                                    │ │ !LockoutEnd.HasValue ||       │
                                    │ │ LockoutEnd <= UtcNow ?        │
                                    │ │ (User is NOT deactivated)     │
                                    │ └───────────────────────────────┘
                                    │       │YES              │NO
                                    │       ▼                 ▼
                                    │ ┌──────────────────┐  ┌─────────────────┐
                                    │ │ Add to results:  │  │ Set LockoutEnd  │
                                    │ │ Status=Skipped   │  │ = null          │
                                    │ │ "Not deactivated"│  │ UpdateSecurity  │
                                    │ │ skippedCount++   │  │ StampAsync      │
                                    │ │ continue;        │──┤ └─────────────────┘
                                    │ └──────────────────┘  │         │
                                    │                       │ ┌───────┴───────┐
                                    │                       │ │               │
                                    │                       │ Succeeded?   Failed
                                    │                       │ │YES           │
                                    │                       │ ▼             ▼
                                    │                       │ Success    Failed
                                    │                       │ count++    count++
                                    │                       │ Log with   with errors
                                    │                       │ Reason     
                                    │                       │   │           │
                                    └───────────────────────┴───┴───────────┘
                                                                        │
              ┌───────────────────────────────┐                         │
              │       INNER CATCH BLOCK       │◄────────────────────────┘
              │ Add to results: Status=Failed │     (on any exception)
              │ ErrorMessage = ex.Message     │
              │ failedCount++                 │
              └───────────────────────────────┘
                              │
                              ▼
              ┌───────────────────────────────┐
              │ Create BulkReactivateResultDto│
              │ Return Response.Ok(result)    │
              └───────────────────────────────┘

              ┌───────────────────────────────┐
              │       OUTER CATCH BLOCK       │
              │ Return Response.Error(...)    │
              └───────────────────────────────┘
```

---

## Boundary Analysis

### 1. LockoutEnd Check: `!LockoutEnd.HasValue || LockoutEnd.Value <= DateTimeOffset.UtcNow`

Điều kiện này xác định user có đang bị deactivate hay không:
- **TRUE** = User KHÔNG đang bị deactivate → Skip
- **FALSE** = User ĐANG bị deactivate → Tiến hành reactivate

| Vị trí | LockoutEnd Value | HasValue | `<= UtcNow` | Condition Result | User Status |
|:-------|:-----------------|:---------|:------------|:-----------------|:------------|
| Null | `null` | `false` | N/A | **TRUE** (short-circuit) | ❌ NOT deactivated → Skip |
| Dưới biên | `UtcNow - 1 min` | `true` | `true` | **TRUE** | ❌ Lockout expired → Skip |
| Ngay biên | `UtcNow` exactly | `true` | `true` | **TRUE** | ❌ Just expired → Skip |
| Trên biên | `UtcNow + 1 min` | `true` | `false` | **FALSE** | ✅ Currently locked → Reactivate |
| MaxValue | `MaxValue` | `true` | `false` | **FALSE** | ✅ Permanently locked → Reactivate |

### 2. Counter Logic

| Scenario | successCount | failedCount | skippedCount |
|:---------|:-------------|:------------|:-------------|
| User not found | 0 | +1 | 0 |
| User not deactivated | 0 | 0 | +1 |
| Update succeeded | +1 | 0 | 0 |
| Update failed | 0 | +1 | 0 |
| Inner exception | 0 | +1 | 0 |

### 3. Reason Logging

| request.Reason | Logged Value |
|:---------------|:-------------|
| `"Some reason"` | `"Some reason"` |
| `null` | `"No reason provided"` |
| `""` | `""` (empty string) |

---

## Bảng Test Cases

| ID | Loại | Tên Test Case (Gợi ý đặt tên) | Input Giả định (Mock Data) | Kết quả Mong đợi | Ghi chú |
|:---|:-----|:------------------------------|:---------------------------|:-----------------|:--------|
| 1 | Happy | BulkReactivateUsersAsync_WhenAllUsersDeactivated_ReactivatesAll | 3 users có `LockoutEnd = DateTimeOffset.MaxValue` | `SuccessCount = 3`, `FailedCount = 0`, `SkippedCount = 0`, tất cả Status = "Success" | [Fact] |
| 2 | Error | BulkReactivateUsersAsync_WhenUserNotFound_MarksAsFailed | `UserIds = ["non-existent"]`, `FindByIdAsync` returns `null` | `FailedCount = 1`, ErrorMessage = "User not found", Status = "Failed" | [Fact] |
| 3 | Error | BulkReactivateUsersAsync_WhenUserNotDeactivated_SkipsUser | User có `LockoutEnd = null` | `SkippedCount = 1`, ErrorMessage = "User is not currently deactivated", Status = "Skipped" | [Fact] |
| 4 | Boundary | BulkReactivateUsersAsync_WhenLockoutEndIsNull_SkipsUser | User có `LockoutEnd = null` (HasValue = false) | `SkippedCount = 1`, short-circuit at `!HasValue` | [Fact] - Null case |
| 5 | Boundary | BulkReactivateUsersAsync_WhenLockoutEndExactlyNow_SkipsUser | User có `LockoutEnd = DateTimeOffset.UtcNow` (exactly) | `SkippedCount = 1` (`<=` passes at boundary) | [Fact] - Ngay biên |
| 6 | Boundary | BulkReactivateUsersAsync_WhenLockoutEndInPast_SkipsUser | User có `LockoutEnd = DateTimeOffset.UtcNow.AddMinutes(-1)` | `SkippedCount = 1` (expired lockout) | [Fact] - Dưới biên |
| 7 | Boundary | BulkReactivateUsersAsync_WhenLockoutEndInFuture_ReactivatesUser | User có `LockoutEnd = DateTimeOffset.UtcNow.AddMinutes(1)` | `SuccessCount = 1` (currently locked → reactivate) | [Fact] - Trên biên |
| 8 | Error | BulkReactivateUsersAsync_WhenUpdateSecurityStampFails_MarksAsFailed | `UpdateSecurityStampAsync` returns `IdentityResult.Failed(...)` | `FailedCount = 1`, ErrorMessage contains error descriptions, Status = "Failed" | [Fact] |
| 9 | Error | BulkReactivateUsersAsync_WhenInnerExceptionThrown_MarksAsFailedAndContinues | `FindByIdAsync` throws exception for user-1, user-2 có LockoutEnd valid | user-1: Failed với ErrorMessage = ex.Message; user-2: Success | [Fact] |
| 10 | Error | BulkReactivateUsersAsync_WhenOuterExceptionThrown_ReturnsError | Exception thrown trước loop (mocking issue) | `Response.Error("Error processing bulk reactivate operation")` | [Fact] |
| 11 | Happy | BulkReactivateUsersAsync_WhenMixedResults_ReturnsCorrectCounts | 4 users: 1 deactivated (success), 1 not found, 1 not deactivated, 1 update fails | `SuccessCount = 1`, `FailedCount = 2`, `SkippedCount = 1`, `TotalRequested = 4` | [Fact] |
| 12 | Happy | BulkReactivateUsersAsync_ReturnsCorrectBulkResultStructure | Valid request với `Reason = "Batch reactivation"` | Verify `TotalRequested`, `Reason = "Batch reactivation"`, `ProcessedAt` is recent, `Results` list has correct length | [Fact] |
| 13 | Edge | BulkReactivateUsersAsync_WhenEmptyUserIdsList_ReturnsEmptyResult | `request.UserIds = []` (empty list) | `TotalRequested = 0`, `SuccessCount = 0`, `FailedCount = 0`, `SkippedCount = 0`, `Results = []` | [Fact] |
| 14 | Happy | BulkReactivateUsersAsync_SetsLockoutEndToNullOnSuccess | Valid deactivated user | Verify `user.LockoutEnd = null` sau khi reactivate thành công | [Fact] |
| 15 | Happy | BulkReactivateUsersAsync_WhenReasonProvided_LogsReason | `request.Reason = "Test reason"` | Verify logger được gọi với reason = "Test reason" | [Fact] |
| 16 | Edge | BulkReactivateUsersAsync_WhenReasonNull_LogsNoReasonProvided | `request.Reason = null` | Verify logger được gọi với message chứa "No reason provided" | [Fact] |

---

## Chi tiết Mock Setup cho từng Test Case

### Test Case #1: All users deactivated - success
```csharp
[Fact]
public async Task BulkReactivateUsersAsync_WhenAllUsersDeactivated_ReactivatesAll()
{
    // Arrange
    var users = new List<ApplicationUser>
    {
        new() { Id = "user-1", UserName = "user1", LockoutEnd = DateTimeOffset.MaxValue },
        new() { Id = "user-2", UserName = "user2", LockoutEnd = DateTimeOffset.MaxValue },
        new() { Id = "user-3", UserName = "user3", LockoutEnd = DateTimeOffset.MaxValue }
    };
    var request = new BulkReactivateUsersDto
    {
        UserIds = new List<string> { "user-1", "user-2", "user-3" },
        Reason = "Batch reactivation"
    };
    
    foreach (var user in users)
    {
        _mockUserManager.Setup(x => x.FindByIdAsync(user.Id)).ReturnsAsync(user);
        _mockUserManager.Setup(x => x.UpdateSecurityStampAsync(user)).ReturnsAsync(IdentityResult.Success);
    }

    // Act
    var result = await _adminUserService.BulkReactivateUsersAsync(request, CancellationToken.None);

    // Assert
    Assert.True(result.IsSuccess);
    var data = (BulkReactivateResultDto)result.Data;
    Assert.Equal(3, data.TotalRequested);
    Assert.Equal(3, data.SuccessCount);
    Assert.Equal(0, data.FailedCount);
    Assert.Equal(0, data.SkippedCount);
    Assert.All(data.Results, r => Assert.Equal("Success", r.Status));
    
    // Verify LockoutEnd was set to null
    foreach (var user in users)
    {
        Assert.Null(user.LockoutEnd);
    }
}
```

### Test Case #3: User not deactivated
```csharp
[Fact]
public async Task BulkReactivateUsersAsync_WhenUserNotDeactivated_SkipsUser()
{
    // Arrange
    var user = new ApplicationUser
    {
        Id = "user-1",
        UserName = "user1",
        LockoutEnd = null // NOT deactivated
    };
    var request = new BulkReactivateUsersDto
    {
        UserIds = new List<string> { "user-1" }
    };
    
    _mockUserManager.Setup(x => x.FindByIdAsync("user-1")).ReturnsAsync(user);

    // Act
    var result = await _adminUserService.BulkReactivateUsersAsync(request, CancellationToken.None);

    // Assert
    var data = (BulkReactivateResultDto)result.Data;
    Assert.Equal(1, data.SkippedCount);
    Assert.Equal(0, data.SuccessCount);
    Assert.Equal("Skipped", data.Results[0].Status);
    Assert.Equal("User is not currently deactivated", data.Results[0].ErrorMessage);
    
    // Verify UpdateSecurityStampAsync was NOT called
    _mockUserManager.Verify(x => x.UpdateSecurityStampAsync(It.IsAny<ApplicationUser>()), Times.Never);
}
```

### Test Case #5: LockoutEnd exactly at UtcNow (Boundary)
```csharp
[Fact]
public async Task BulkReactivateUsersAsync_WhenLockoutEndExactlyNow_SkipsUser()
{
    // Arrange
    var now = DateTimeOffset.UtcNow;
    var user = new ApplicationUser
    {
        Id = "user-1",
        UserName = "user1",
        LockoutEnd = now // EXACTLY at boundary
    };
    var request = new BulkReactivateUsersDto { UserIds = new List<string> { "user-1" } };
    
    _mockUserManager.Setup(x => x.FindByIdAsync("user-1")).ReturnsAsync(user);

    // Act
    var result = await _adminUserService.BulkReactivateUsersAsync(request, CancellationToken.None);

    // Assert
    var data = (BulkReactivateResultDto)result.Data;
    Assert.Equal(1, data.SkippedCount); // Should skip because <= passes at exact boundary
    Assert.Equal(0, data.SuccessCount);
}
```

### Test Case #7: LockoutEnd in future (Boundary - valid for reactivation)
```csharp
[Fact]
public async Task BulkReactivateUsersAsync_WhenLockoutEndInFuture_ReactivatesUser()
{
    // Arrange
    var user = new ApplicationUser
    {
        Id = "user-1",
        UserName = "user1",
        LockoutEnd = DateTimeOffset.UtcNow.AddMinutes(1) // Future = currently locked
    };
    var request = new BulkReactivateUsersDto { UserIds = new List<string> { "user-1" } };
    
    _mockUserManager.Setup(x => x.FindByIdAsync("user-1")).ReturnsAsync(user);
    _mockUserManager.Setup(x => x.UpdateSecurityStampAsync(user)).ReturnsAsync(IdentityResult.Success);

    // Act
    var result = await _adminUserService.BulkReactivateUsersAsync(request, CancellationToken.None);

    // Assert
    var data = (BulkReactivateResultDto)result.Data;
    Assert.Equal(1, data.SuccessCount);
    Assert.Equal(0, data.SkippedCount);
    Assert.Null(user.LockoutEnd); // Verify it was set to null
}
```

### Test Case #11: Mixed results
```csharp
[Fact]
public async Task BulkReactivateUsersAsync_WhenMixedResults_ReturnsCorrectCounts()
{
    // Arrange
    var deactivatedUser = new ApplicationUser { Id = "user-1", UserName = "user1", LockoutEnd = DateTimeOffset.MaxValue };
    var notDeactivatedUser = new ApplicationUser { Id = "user-2", UserName = "user2", LockoutEnd = null };
    var failUpdateUser = new ApplicationUser { Id = "user-3", UserName = "user3", LockoutEnd = DateTimeOffset.MaxValue };
    
    var request = new BulkReactivateUsersDto
    {
        UserIds = new List<string> { "user-1", "user-not-found", "user-2", "user-3" }
    };
    
    _mockUserManager.Setup(x => x.FindByIdAsync("user-1")).ReturnsAsync(deactivatedUser);
    _mockUserManager.Setup(x => x.FindByIdAsync("user-not-found")).ReturnsAsync((ApplicationUser)null);
    _mockUserManager.Setup(x => x.FindByIdAsync("user-2")).ReturnsAsync(notDeactivatedUser);
    _mockUserManager.Setup(x => x.FindByIdAsync("user-3")).ReturnsAsync(failUpdateUser);
    
    _mockUserManager.Setup(x => x.UpdateSecurityStampAsync(deactivatedUser)).ReturnsAsync(IdentityResult.Success);
    _mockUserManager.Setup(x => x.UpdateSecurityStampAsync(failUpdateUser))
        .ReturnsAsync(IdentityResult.Failed(new IdentityError { Description = "Update failed" }));

    // Act
    var result = await _adminUserService.BulkReactivateUsersAsync(request, CancellationToken.None);

    // Assert
    var data = (BulkReactivateResultDto)result.Data;
    Assert.Equal(4, data.TotalRequested);
    Assert.Equal(1, data.SuccessCount);   // user-1
    Assert.Equal(2, data.FailedCount);    // user-not-found, user-3 (update failed)
    Assert.Equal(1, data.SkippedCount);   // user-2 (not deactivated)
}
```

### Test Case #16: Reason is null
```csharp
[Fact]
public async Task BulkReactivateUsersAsync_WhenReasonNull_LogsNoReasonProvided()
{
    // Arrange
    var user = new ApplicationUser
    {
        Id = "user-1",
        UserName = "user1",
        LockoutEnd = DateTimeOffset.MaxValue
    };
    var request = new BulkReactivateUsersDto
    {
        UserIds = new List<string> { "user-1" },
        Reason = null // NULL reason
    };
    
    _mockUserManager.Setup(x => x.FindByIdAsync("user-1")).ReturnsAsync(user);
    _mockUserManager.Setup(x => x.UpdateSecurityStampAsync(user)).ReturnsAsync(IdentityResult.Success);

    // Act
    var result = await _adminUserService.BulkReactivateUsersAsync(request, CancellationToken.None);

    // Assert
    Assert.True(result.IsSuccess);
    
    // Verify logger was called with "No reason provided"
    _mockLogger.Verify(
        x => x.Log(
            LogLevel.Information,
            It.IsAny<EventId>(),
            It.Is<It.IsAnyType>((o, t) => o.ToString().Contains("No reason provided")),
            null,
            It.IsAny<Func<It.IsAnyType, Exception?, string>>()
        ),
        Times.Once
    );
}
```

---

## Tổng kết Coverage

| Nhánh Code | Test Case IDs |
|:-----------|:--------------|
| Outer try block | #1-#16 (except #10) |
| Outer catch block | #10 |
| Inner try block | #1-#9, #11-#16 |
| Inner catch block | #9 |
| `user == null` | #2, #11 |
| `user != null` | #1, #3-#9, #12-#16 |
| `!LockoutEnd.HasValue` = true (null) | #3, #4 |
| `LockoutEnd.Value <= UtcNow` = true (past) | #6 |
| `LockoutEnd.Value <= UtcNow` = true (exactly now) | #5 |
| `LockoutEnd.Value <= UtcNow` = false (future) | #1, #7, #11, #14-#16 |
| `result.Succeeded` = true | #1, #7, #11, #14-#16 |
| `result.Succeeded` = false | #8, #11 |
| `request.Reason != null` | #1, #12, #15 |
| `request.Reason == null` | #16 |
| Empty UserIds list | #13 |
| BulkResult structure | #12 |

---

## So sánh với `BulkDeactivateUsersAsync`

| Khác biệt | BulkDeactivateUsersAsync | BulkReactivateUsersAsync |
|:----------|:-------------------------|:-------------------------|
| Purpose | Khóa users | Mở khóa users |
| Self-check | ✅ Có (skip self) | ❌ Không có |
| LockoutEnd condition | `> UtcNow` = already locked → Skip | `<= UtcNow` = not locked → Skip |
| Action | `LockoutEnd = MaxValue` | `LockoutEnd = null` |
| Force flag | ✅ Có (override protected) | ❌ Không có |
| Protected account check | ✅ Có | ❌ Không có |

---

## Thống kê

- **Tổng số Test Cases:** 16
- **Happy Path:** 6 (ID #1, #11, #12, #14, #15)
- **Error Cases:** 5 (ID #2, #3, #8, #9, #10)
- **Boundary Cases:** 4 (ID #4, #5, #6, #7)
- **Edge Cases:** 2 (ID #13, #16)

**Code Coverage dự kiến:** 100% cho hàm `BulkReactivateUsersAsync`

---

## ⚠️ Lưu ý về Logic

### 1. Không có Self-Check
- Khác với `BulkDeactivateUsersAsync`, hàm này **KHÔNG kiểm tra** admin có đang reactivate chính mình không
- Điều này hợp lý vì admin không thể tự khóa mình (đã bị chặn ở `Deactivate`), nên cũng không cần chặn tự mở khóa

### 2. Điều kiện "Not Deactivated"
- `!LockoutEnd.HasValue` = User chưa bao giờ bị lock
- `LockoutEnd <= UtcNow` = User từng bị lock nhưng đã hết hạn
- Cả hai trường hợp đều được coi là "not currently deactivated" → Skip

### 3. LockoutEnd = null sau Reactivate
- Thay vì set thành `UtcNow` (vẫn locked trong 1 giây), code set thành `null`
- Đây là cách clean nhất để unlock user

### 4. ErrorMessage trong Inner Catch
- Khác với `BulkDeactivateUsersAsync` (dùng "Internal error")
- Hàm này dùng `ex.Message` - có thể expose thông tin nhạy cảm

---

## Đề xuất cải thiện Code

1. **Validate request null:**
   ```csharp
   ArgumentNullException.ThrowIfNull(request);
   if (request.UserIds == null)
       return Response.Error("UserIds cannot be null");
   ```

2. **Consistent error message trong inner catch:**
   ```csharp
   // Thay vì:
   ErrorMessage = ex.Message,
   // Nên dùng:
   ErrorMessage = "Internal error", // Consistent với BulkDeactivate
   ```

3. **Thêm audit logging:**
   ```csharp
   _logger.LogInformation(
       "User {UserId} reactivated by Admin. Previous LockoutEnd: {OldLockoutEnd}",
       userId, oldLockoutEnd);
   ```

4. **Consider adding adminId parameter:**
   ```csharp
   public async Task<Response> BulkReactivateUsersAsync(
       BulkReactivateUsersDto request, 
       string adminId, // Để log ai thực hiện
       CancellationToken ct)
   ```

