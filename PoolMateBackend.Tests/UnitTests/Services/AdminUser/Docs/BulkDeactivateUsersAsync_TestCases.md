# Test Cases cho `BulkDeactivateUsersAsync` - AdminUserService

## Thông tin hàm

**File:** `PoolMateBackend/Services/AdminUserService.cs`

**Signature:**
```csharp
public async Task<Response> BulkDeactivateUsersAsync(
    BulkDeactivateUsersDto request, 
    string adminId, 
    CancellationToken ct)
```

---

## Phân tích Control Flow

Hàm `BulkDeactivateUsersAsync` có cấu trúc loop phức tạp với nhiều nhánh điều kiện:

```
┌─────────────────────────────────────────────────────────────┐
│                  BulkDeactivateUsersAsync                    │
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
              │   userId == adminId ?         │                       │
              │   (Self-deactivation check)   │                       │
              └───────────────────────────────┘                       │
                    │YES              │NO                             │
                    ▼                 ▼                               │
          ┌──────────────────┐  ┌─────────────────────────────┐       │
          │ Add to results:  │  │ FindByIdAsync(userId)       │       │
          │ Status=Skipped   │  └─────────────────────────────┘       │
          │ skippedCount++   │                │                       │
          │ continue;        │──────┐ ┌───────┴───────┐               │
          └──────────────────┘      │ │NULL           │NOT NULL       │
                                    │ ▼               ▼               │
                                    │ ┌──────────────────┐  ┌─────────┴───────┐
                                    │ │ Add to results:  │  │ Check LockoutEnd│
                                    │ │ Status=Failed    │  │ already locked? │
                                    │ │ "User not found" │  └─────────────────┘
                                    │ │ failedCount++    │          │
                                    │ │ continue;        │──┐       │
                                    │ └──────────────────┘  │       │
                                    │                       │       │
                                    │       ┌───────────────┴───────┴──────────────┐
                                    │       │                                      │
                                    │   LockoutEnd.HasValue &&              Otherwise
                                    │   LockoutEnd > UtcNow ?               (not locked)
                                    │       │YES                                   │
                                    │       ▼                                      ▼
                                    │ ┌──────────────────┐            ┌─────────────────────┐
                                    │ │ Add to results:  │            │ Check LockoutEnabled│
                                    │ │ Status=Skipped   │            │ and Force flag      │
                                    │ │ "Already deact." │            └─────────────────────┘
                                    │ │ skippedCount++   │                      │
                                    │ │ continue;        │──┐   ┌───────────────┴───────────────┐
                                    │ └──────────────────┘  │   │                               │
                                    │                       │ !LockoutEnabled               Otherwise
                                    │                       │ && !Force ?                  (can proceed)
                                    │                       │   │YES                             │
                                    │                       │   ▼                                ▼
                                    │                       │ ┌──────────────────┐  ┌─────────────────────┐
                                    │                       │ │ Add to results:  │  │ Set LockoutEnd =    │
                                    │                       │ │ Status=Skipped   │  │ DateTimeOffset.Max  │
                                    │                       │ │ "Protected acct" │  │ UpdateSecurityStamp │
                                    │                       │ │ skippedCount++   │  └─────────────────────┘
                                    │                       │ │ continue;        │──┐         │
                                    │                       │ └──────────────────┘  │         │
                                    │                       │                       │ ┌───────┴───────┐
                                    │                       │                       │ │               │
                                    │                       │                       │ Succeeded?   Failed
                                    │                       │                       │ │YES           │
                                    │                       │                       │ ▼             ▼
                                    │                       │                       │ Success    Failed
                                    │                       │                       │ count++    count++
                                    │                       │                       │   │           │
                                    └───────────────────────┴───────────────────────┴───┴───────────┘
                                                                                            │
              ┌───────────────────────────────┐                                             │
              │       INNER CATCH BLOCK       │◄────────────────────────────────────────────┘
              │ Add to results: Status=Failed │     (on any exception in inner block)
              │ "Internal error"              │
              │ failedCount++                 │
              └───────────────────────────────┘
                              │
                              ▼
              ┌───────────────────────────────┐
              │ Create BulkDeactivateResultDto│
              │ Return Response.Ok(result)    │
              └───────────────────────────────┘

              ┌───────────────────────────────┐
              │       OUTER CATCH BLOCK       │
              │ Return Response.Error(...)    │
              └───────────────────────────────┘
```

---

## Boundary Analysis

### 1. LockoutEnd Time Comparison (`> DateTimeOffset.UtcNow`)

| Vị trí | LockoutEnd Value | HasValue | Condition Result | Flow |
|:-------|:-----------------|:---------|:-----------------|:-----|
| Null | `null` | `false` | Short-circuit | ✅ Continue to next check |
| Dưới biên | `UtcNow - 1 minute` | `true` | `> UtcNow` = false | ✅ Continue |
| Ngay biên | `UtcNow` (exactly) | `true` | `> UtcNow` = false | ✅ Continue |
| Trên biên | `UtcNow + 1 minute` | `true` | `> UtcNow` = true | ❌ Skipped |
| MaxValue | `MaxValue` | `true` | `> UtcNow` = true | ❌ Skipped |

### 2. Protected Account Logic (`!LockoutEnabled && !Force`)

| LockoutEnabled | Force | Condition Result | Flow |
|:---------------|:------|:-----------------|:-----|
| `true` | `false` | `!true && !false` = false | ✅ Continue |
| `true` | `true` | `!true && !true` = false | ✅ Continue |
| `false` | `false` | `!false && !false` = true | ❌ Skipped (Protected) |
| `false` | `true` | `!false && !true` = false | ✅ Continue (Force override) |

### 3. Counter Increments

| Scenario | successCount | failedCount | skippedCount |
|:---------|:-------------|:------------|:-------------|
| Self-deactivation | 0 | 0 | +1 |
| User not found | 0 | +1 | 0 |
| Already deactivated | 0 | 0 | +1 |
| Protected (no force) | 0 | 0 | +1 |
| Update failed | 0 | +1 | 0 |
| Update succeeded | +1 | 0 | 0 |
| Inner exception | 0 | +1 | 0 |

---

## Bảng Test Cases

| ID | Loại | Tên Test Case (Gợi ý đặt tên) | Input Giả định (Mock Data) | Kết quả Mong đợi | Ghi chú |
|:---|:-----|:------------------------------|:---------------------------|:-----------------|:--------|
| 1 | Happy | BulkDeactivateUsersAsync_WhenAllUsersValid_DeactivatesAll | 3 users hợp lệ: `LockoutEnabled = true`, `LockoutEnd = null` | `SuccessCount = 3`, `FailedCount = 0`, `SkippedCount = 0`, tất cả results có `Status = "Success"` | [Fact] |
| 2 | Error | BulkDeactivateUsersAsync_WhenAdminInList_SkipsSelfAndContinues | `UserIds = ["admin-1", "user-2"]`, `adminId = "admin-1"` | `SkippedCount = 1`, `SuccessCount = 1`, admin result có message "Cannot deactivate your own account" | [Fact] |
| 3 | Error | BulkDeactivateUsersAsync_WhenUserNotFound_MarksAsFailed | `UserIds = ["non-existent"]`, `FindByIdAsync` returns `null` | `FailedCount = 1`, result có ErrorMessage = "User not found", Status = "Failed" | [Fact] |
| 4 | Error | BulkDeactivateUsersAsync_WhenUserAlreadyDeactivated_SkipsUser | User có `LockoutEnd = DateTimeOffset.UtcNow.AddDays(1)` | `SkippedCount = 1`, ErrorMessage = "User is already deactivated", Status = "Skipped" | [Fact] |
| 5 | Boundary | BulkDeactivateUsersAsync_WhenLockoutEndExactlyNow_ContinuesDeactivation | User có `LockoutEnd = DateTimeOffset.UtcNow` (exactly) | User được deactivate, `SuccessCount = 1` (> fails at boundary) | [Fact] - Ngay biên |
| 6 | Boundary | BulkDeactivateUsersAsync_WhenLockoutEndInPast_ContinuesDeactivation | User có `LockoutEnd = DateTimeOffset.UtcNow.AddMinutes(-1)` | User được deactivate, `SuccessCount = 1` | [Fact] - Dưới biên |
| 7 | Boundary | BulkDeactivateUsersAsync_WhenLockoutEndInFuture_SkipsUser | User có `LockoutEnd = DateTimeOffset.UtcNow.AddMinutes(1)` | `SkippedCount = 1`, Status = "Skipped" | [Fact] - Trên biên |
| 8 | Error | BulkDeactivateUsersAsync_WhenProtectedAccountAndNoForce_SkipsUser | User có `LockoutEnabled = false`, `request.Force = false` | `SkippedCount = 1`, ErrorMessage = "Protected account - use Force option to deactivate" | [Fact] |
| 9 | Happy | BulkDeactivateUsersAsync_WhenProtectedAccountAndForce_DeactivatesUser | User có `LockoutEnabled = false`, `request.Force = true` | `SuccessCount = 1`, user được deactivate (Force override) | [Fact] |
| 10 | Error | BulkDeactivateUsersAsync_WhenUpdateSecurityStampFails_MarksAsFailed | `UpdateSecurityStampAsync` returns `IdentityResult.Failed(...)` | `FailedCount = 1`, ErrorMessage contains error descriptions, Status = "Failed" | [Fact] |
| 11 | Error | BulkDeactivateUsersAsync_WhenInnerExceptionThrown_MarksAsFailedAndContinues | `FindByIdAsync` throws exception for user-1, user-2 is valid | user-1: `FailedCount = 1`, ErrorMessage = "Internal error"; user-2: `SuccessCount = 1` | [Fact] |
| 12 | Error | BulkDeactivateUsersAsync_WhenOuterExceptionThrown_ReturnsError | Exception thrown trước loop (e.g., mocking issue) | `Response.Error("Error processing bulk deactivate operation")` | [Fact] |
| 13 | Happy | BulkDeactivateUsersAsync_WhenMixedResults_ReturnsCorrectCounts | 4 users: 1 valid, 1 not found, 1 already deactivated, 1 protected | `SuccessCount = 1`, `FailedCount = 1`, `SkippedCount = 2`, `TotalRequested = 4` | [Fact] |
| 14 | Happy | BulkDeactivateUsersAsync_ReturnsCorrectBulkResultStructure | Valid request với `Reason = "Cleanup"` | Verify `TotalRequested`, `Reason`, `ProcessedAt` is recent, `Results` list has correct length | [Fact] |
| 15 | Edge | BulkDeactivateUsersAsync_WhenEmptyUserIdsList_ReturnsEmptyResult | `request.UserIds = []` (empty list) | `TotalRequested = 0`, `SuccessCount = 0`, `FailedCount = 0`, `SkippedCount = 0`, `Results = []` | [Fact] |
| 16 | Edge | BulkDeactivateUsersAsync_WhenLockoutEndIsNull_ContinuesDeactivation | User có `LockoutEnd = null` | User được deactivate (HasValue = false, short-circuit) | [Fact] |
| 17 | Happy | BulkDeactivateUsersAsync_SetsLockoutEndToMaxValueOnSuccess | Valid user | Verify `user.LockoutEnd = DateTimeOffset.MaxValue` sau khi deactivate | [Fact] |

---

## Chi tiết Mock Setup cho từng Test Case

### Test Case #1: All users valid
```csharp
[Fact]
public async Task BulkDeactivateUsersAsync_WhenAllUsersValid_DeactivatesAll()
{
    // Arrange
    var adminId = "admin-999";
    var users = new List<ApplicationUser>
    {
        new() { Id = "user-1", UserName = "user1", LockoutEnabled = true, LockoutEnd = null },
        new() { Id = "user-2", UserName = "user2", LockoutEnabled = true, LockoutEnd = null },
        new() { Id = "user-3", UserName = "user3", LockoutEnabled = true, LockoutEnd = null }
    };
    var request = new BulkDeactivateUsersDto
    {
        UserIds = new List<string> { "user-1", "user-2", "user-3" },
        Reason = "Test cleanup"
    };
    
    foreach (var user in users)
    {
        _mockUserManager.Setup(x => x.FindByIdAsync(user.Id)).ReturnsAsync(user);
        _mockUserManager.Setup(x => x.UpdateSecurityStampAsync(user)).ReturnsAsync(IdentityResult.Success);
    }

    // Act
    var result = await _adminUserService.BulkDeactivateUsersAsync(request, adminId, CancellationToken.None);

    // Assert
    Assert.True(result.IsSuccess);
    var data = (BulkDeactivateResultDto)result.Data;
    Assert.Equal(3, data.TotalRequested);
    Assert.Equal(3, data.SuccessCount);
    Assert.Equal(0, data.FailedCount);
    Assert.Equal(0, data.SkippedCount);
    Assert.All(data.Results, r => Assert.Equal("Success", r.Status));
}
```

### Test Case #2: Admin in list
```csharp
[Fact]
public async Task BulkDeactivateUsersAsync_WhenAdminInList_SkipsSelfAndContinues()
{
    // Arrange
    var adminId = "admin-1";
    var validUser = new ApplicationUser { Id = "user-2", UserName = "user2", LockoutEnabled = true };
    var request = new BulkDeactivateUsersDto
    {
        UserIds = new List<string> { "admin-1", "user-2" } // Admin is first in list
    };
    
    _mockUserManager.Setup(x => x.FindByIdAsync("user-2")).ReturnsAsync(validUser);
    _mockUserManager.Setup(x => x.UpdateSecurityStampAsync(validUser)).ReturnsAsync(IdentityResult.Success);

    // Act
    var result = await _adminUserService.BulkDeactivateUsersAsync(request, adminId, CancellationToken.None);

    // Assert
    Assert.True(result.IsSuccess);
    var data = (BulkDeactivateResultDto)result.Data;
    Assert.Equal(1, data.SkippedCount);
    Assert.Equal(1, data.SuccessCount);
    
    var adminResult = data.Results.First(r => r.UserId == "admin-1");
    Assert.Equal("Skipped", adminResult.Status);
    Assert.Equal("Cannot deactivate your own account", adminResult.ErrorMessage);
}
```

### Test Case #5: LockoutEnd exactly at UtcNow (Boundary)
```csharp
[Fact]
public async Task BulkDeactivateUsersAsync_WhenLockoutEndExactlyNow_ContinuesDeactivation()
{
    // Arrange
    var adminId = "admin-999";
    var now = DateTimeOffset.UtcNow;
    var user = new ApplicationUser
    {
        Id = "user-1",
        UserName = "user1",
        LockoutEnabled = true,
        LockoutEnd = now // EXACTLY at boundary
    };
    var request = new BulkDeactivateUsersDto { UserIds = new List<string> { "user-1" } };
    
    _mockUserManager.Setup(x => x.FindByIdAsync("user-1")).ReturnsAsync(user);
    _mockUserManager.Setup(x => x.UpdateSecurityStampAsync(user)).ReturnsAsync(IdentityResult.Success);

    // Act
    var result = await _adminUserService.BulkDeactivateUsersAsync(request, adminId, CancellationToken.None);

    // Assert
    Assert.True(result.IsSuccess);
    var data = (BulkDeactivateResultDto)result.Data;
    Assert.Equal(1, data.SuccessCount); // Should succeed because > fails at exact boundary
    Assert.Equal(0, data.SkippedCount);
}
```

### Test Case #8 & #9: Protected account with/without Force
```csharp
[Fact]
public async Task BulkDeactivateUsersAsync_WhenProtectedAccountAndNoForce_SkipsUser()
{
    // Arrange
    var adminId = "admin-999";
    var protectedUser = new ApplicationUser
    {
        Id = "user-1",
        UserName = "superadmin",
        LockoutEnabled = false, // PROTECTED
        LockoutEnd = null
    };
    var request = new BulkDeactivateUsersDto
    {
        UserIds = new List<string> { "user-1" },
        Force = false // No force
    };
    
    _mockUserManager.Setup(x => x.FindByIdAsync("user-1")).ReturnsAsync(protectedUser);

    // Act
    var result = await _adminUserService.BulkDeactivateUsersAsync(request, adminId, CancellationToken.None);

    // Assert
    var data = (BulkDeactivateResultDto)result.Data;
    Assert.Equal(1, data.SkippedCount);
    Assert.Contains("Protected account", data.Results[0].ErrorMessage);
}

[Fact]
public async Task BulkDeactivateUsersAsync_WhenProtectedAccountAndForce_DeactivatesUser()
{
    // Arrange
    var adminId = "admin-999";
    var protectedUser = new ApplicationUser
    {
        Id = "user-1",
        UserName = "superadmin",
        LockoutEnabled = false, // PROTECTED
        LockoutEnd = null
    };
    var request = new BulkDeactivateUsersDto
    {
        UserIds = new List<string> { "user-1" },
        Force = true // FORCE override
    };
    
    _mockUserManager.Setup(x => x.FindByIdAsync("user-1")).ReturnsAsync(protectedUser);
    _mockUserManager.Setup(x => x.UpdateSecurityStampAsync(protectedUser)).ReturnsAsync(IdentityResult.Success);

    // Act
    var result = await _adminUserService.BulkDeactivateUsersAsync(request, adminId, CancellationToken.None);

    // Assert
    var data = (BulkDeactivateResultDto)result.Data;
    Assert.Equal(1, data.SuccessCount);
    Assert.Equal(0, data.SkippedCount);
}
```

### Test Case #11: Inner exception handling
```csharp
[Fact]
public async Task BulkDeactivateUsersAsync_WhenInnerExceptionThrown_MarksAsFailedAndContinues()
{
    // Arrange
    var adminId = "admin-999";
    var validUser = new ApplicationUser { Id = "user-2", UserName = "user2", LockoutEnabled = true };
    var request = new BulkDeactivateUsersDto
    {
        UserIds = new List<string> { "user-1", "user-2" }
    };
    
    // user-1 throws exception
    _mockUserManager.Setup(x => x.FindByIdAsync("user-1"))
        .ThrowsAsync(new Exception("Database error"));
    
    // user-2 is valid
    _mockUserManager.Setup(x => x.FindByIdAsync("user-2")).ReturnsAsync(validUser);
    _mockUserManager.Setup(x => x.UpdateSecurityStampAsync(validUser)).ReturnsAsync(IdentityResult.Success);

    // Act
    var result = await _adminUserService.BulkDeactivateUsersAsync(request, adminId, CancellationToken.None);

    // Assert
    Assert.True(result.IsSuccess); // Overall operation succeeds
    var data = (BulkDeactivateResultDto)result.Data;
    Assert.Equal(1, data.FailedCount);
    Assert.Equal(1, data.SuccessCount);
    
    var failedResult = data.Results.First(r => r.UserId == "user-1");
    Assert.Equal("Failed", failedResult.Status);
    Assert.Equal("Internal error", failedResult.ErrorMessage);
}
```

### Test Case #13: Mixed results
```csharp
[Fact]
public async Task BulkDeactivateUsersAsync_WhenMixedResults_ReturnsCorrectCounts()
{
    // Arrange
    var adminId = "admin-999";
    
    var validUser = new ApplicationUser { Id = "user-1", UserName = "user1", LockoutEnabled = true, LockoutEnd = null };
    var alreadyDeactivated = new ApplicationUser { Id = "user-2", UserName = "user2", LockoutEnabled = true, LockoutEnd = DateTimeOffset.MaxValue };
    var protectedUser = new ApplicationUser { Id = "user-3", UserName = "superadmin", LockoutEnabled = false, LockoutEnd = null };
    
    var request = new BulkDeactivateUsersDto
    {
        UserIds = new List<string> { "user-1", "user-not-found", "user-2", "user-3" },
        Force = false
    };
    
    _mockUserManager.Setup(x => x.FindByIdAsync("user-1")).ReturnsAsync(validUser);
    _mockUserManager.Setup(x => x.FindByIdAsync("user-not-found")).ReturnsAsync((ApplicationUser)null);
    _mockUserManager.Setup(x => x.FindByIdAsync("user-2")).ReturnsAsync(alreadyDeactivated);
    _mockUserManager.Setup(x => x.FindByIdAsync("user-3")).ReturnsAsync(protectedUser);
    _mockUserManager.Setup(x => x.UpdateSecurityStampAsync(validUser)).ReturnsAsync(IdentityResult.Success);

    // Act
    var result = await _adminUserService.BulkDeactivateUsersAsync(request, adminId, CancellationToken.None);

    // Assert
    var data = (BulkDeactivateResultDto)result.Data;
    Assert.Equal(4, data.TotalRequested);
    Assert.Equal(1, data.SuccessCount);   // user-1
    Assert.Equal(1, data.FailedCount);    // user-not-found
    Assert.Equal(2, data.SkippedCount);   // user-2 (already deactivated), user-3 (protected)
}
```

---

## Tổng kết Coverage

| Nhánh Code | Test Case IDs |
|:-----------|:--------------|
| Outer try block | #1-#16 |
| Outer catch block | #12 |
| Inner try block | #1-#11, #13-#17 |
| Inner catch block | #11 |
| `userId == adminId` = true | #2 |
| `userId == adminId` = false | #1, #3-#11, #13-#17 |
| `user == null` | #3, #13 |
| `user != null` | #1, #2, #4-#11, #14-#17 |
| `LockoutEnd.HasValue && LockoutEnd > UtcNow` = true | #4, #7 |
| `LockoutEnd.HasValue && LockoutEnd > UtcNow` = false (null) | #16 |
| `LockoutEnd.HasValue && LockoutEnd > UtcNow` = false (past) | #6 |
| `LockoutEnd.HasValue && LockoutEnd > UtcNow` = false (now) | #5 |
| `!LockoutEnabled && !Force` = true | #8 |
| `!LockoutEnabled && !Force` = false (enabled) | #1, #5, #6 |
| `!LockoutEnabled && !Force` = false (force) | #9 |
| `result.Succeeded` = true | #1, #2, #5, #6, #9, #13, #17 |
| `result.Succeeded` = false | #10 |
| Empty UserIds list | #15 |
| BulkResult structure | #14 |

---

## So sánh với `DeactivateUserAsync`

| Khác biệt | DeactivateUserAsync | BulkDeactivateUsersAsync |
|:----------|:--------------------|:-------------------------|
| Input | Single userId | List of userIds |
| Self-check | Returns error immediately | Skips and continues |
| Protected check | Always block | Can override with `Force` flag |
| Error handling | Single try-catch | Outer + Inner try-catch per user |
| Return type | Single Response | BulkDeactivateResultDto with counts |
| Transaction | Single operation | Loop with individual results |

---

## Thống kê

- **Tổng số Test Cases:** 17
- **Happy Path:** 5 (ID #1, #9, #13, #14, #17)
- **Error Cases:** 7 (ID #2, #3, #4, #8, #10, #11, #12)
- **Boundary Cases:** 3 (ID #5, #6, #7)
- **Edge Cases:** 2 (ID #15, #16)

**Code Coverage dự kiến:** 100% cho hàm `BulkDeactivateUsersAsync`

---

## ⚠️ Lưu ý về Logic

### 1. Self-Deactivation trong Bulk
- Khác với `DeactivateUserAsync`, bulk operation **không return error ngay** mà **skip và tiếp tục** xử lý các user khác
- Điều này đảm bảo một user "sai" trong list không ảnh hưởng đến toàn bộ operation

### 2. Force Flag
- `Force = true` cho phép deactivate cả tài khoản protected (`LockoutEnabled = false`)
- Đây là tính năng "override" dành cho super admin

### 3. Inner Exception Isolation
- Mỗi user được xử lý trong inner try-catch riêng
- Exception của user A không ảnh hưởng đến user B

### 4. Counter Accuracy
- `SuccessCount + FailedCount + SkippedCount` nên = `TotalRequested`
- Mỗi user chỉ thuộc 1 trong 3 categories

---

## Đề xuất cải thiện Code

1. **Validate request null:**
   ```csharp
   ArgumentNullException.ThrowIfNull(request);
   if (request.UserIds == null)
       return Response.Error("UserIds cannot be null");
   ```

2. **Parallel processing option:**
   ```csharp
   if (request.ParallelProcessing)
   {
       await Parallel.ForEachAsync(request.UserIds, ct, async (userId, token) => ...);
   }
   ```

3. **Batch transaction:**
   ```csharp
   using var transaction = await _db.Database.BeginTransactionAsync(ct);
   // Process all
   await transaction.CommitAsync(ct);
   ```

4. **Rate limiting:**
   ```csharp
   if (request.UserIds.Count > 100)
       return Response.Error("Maximum 100 users per bulk operation");
   ```

