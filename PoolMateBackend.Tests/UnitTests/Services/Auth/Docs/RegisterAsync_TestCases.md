# Test Cases cho `RegisterAsync` - AuthService

## Thông tin hàm

**File:** `PoolMateBackend/Services/AuthService.cs`

**Signature:**
```csharp
public async Task<Response> RegisterAsync(RegisterModel model, string baseUri, CancellationToken ct = default)
```

---

## Phân tích Control Flow

Hàm `RegisterAsync` có các nhánh điều kiện sau:

1. **User already exists**: `FindByNameAsync(model.Username) is not null` → return Error
2. **Create user failed**: `!result.Succeeded` → return Error với danh sách lỗi
3. **Role not exists**: `!RoleExistsAsync(UserRoles.PLAYER)` → tạo role mới
4. **Role exists**: Bỏ qua tạo role, tiếp tục add role cho user
5. **Happy path**: Tất cả pass → gửi email, return Ok

---

## Defensive Coding Analysis

⚠️ **Lưu ý quan trọng:** Hàm này **KHÔNG có null check** cho `model`. Nếu `model = null`, sẽ throw `NullReferenceException` khi truy cập `model.Username`.

**Đề xuất:** Thêm `ArgumentNullException.ThrowIfNull(model)` ở đầu hàm để nhất quán với `LoginAsync`.

---

## Bảng Test Cases

| ID | Loại | Tên Test Case (Gợi ý đặt tên) | Input Giả định (Mock Data) | Kết quả Mong đợi | Ghi chú |
|:---|:-----|:------------------------------|:---------------------------|:-----------------|:--------|
| 1 | Error | RegisterAsync_WhenUserAlreadyExists_ReturnsError | `FindByNameAsync("existinguser")` returns existing user | `Response.Error("User already exists!")` | [Fact] |
| 2 | Error | RegisterAsync_WhenCreateUserFails_ReturnsErrorWithDescription | `CreateAsync` returns `IdentityResult.Failed(new IdentityError { Description = "Password too weak" })` | `Response.Error("Password too weak")` | [Fact] |
| 3 | Error | RegisterAsync_WhenCreateUserFailsWithMultipleErrors_ReturnsJoinedErrors | `CreateAsync` returns failed với 2+ errors: `["Error1", "Error2"]` | `Response.Error("Error1; Error2")` | [Fact] - Test `string.Join("; ", ...)` logic |
| 4 | Happy | RegisterAsync_WhenRoleNotExists_CreatesRoleAndReturnsOk | `RoleExistsAsync` returns `false`, tất cả khác thành công | `Response.Ok(...)`, verify `_roles.CreateAsync(IdentityRole)` được gọi | [Fact] |
| 5 | Happy | RegisterAsync_WhenRoleExists_SkipsRoleCreationAndReturnsOk | `RoleExistsAsync` returns `true`, tất cả khác thành công | `Response.Ok(...)`, verify `_roles.CreateAsync(IdentityRole)` **KHÔNG** được gọi | [Fact] |
| 6 | Happy | RegisterAsync_WhenAllValid_ReturnsOkMessage | User mới, password hợp lệ, role exists | `Response.Ok("User created. Please check your email to confirm.")` | [Fact] |
| 7 | Edge | RegisterAsync_VerifyUserCreatedWithCorrectProperties | Valid model: `{ Username = "newuser", Email = "new@test.com", Password = "Pass123!" }` | Verify `ApplicationUser` được tạo với `UserName`, `Email`, `SecurityStamp` không null | [Fact] |
| 8 | Edge | RegisterAsync_VerifyAddToRoleAsyncCalledWithPlayerRole | Valid registration | Verify `AddToRoleAsync(user, UserRoles.PLAYER)` được gọi đúng | [Fact] |
| 9 | Edge | RegisterAsync_VerifySendEmailConfirmationAsyncCalled | Valid registration với `baseUri = "https://example.com"` | Verify email confirmation được gửi với đúng user và baseUri | [Fact] |

---

## Chi tiết Mock Setup cho từng Test Case

### Test Case #1: User already exists
```csharp
[Fact]
public async Task RegisterAsync_WhenUserAlreadyExists_ReturnsError()
{
    // Arrange
    var existingUser = new ApplicationUser { Id = "existing-id", UserName = "existinguser" };
    var model = new RegisterModel { Username = "existinguser", Email = "test@test.com", Password = "Pass123!" };
    
    _mockUserManager.Setup(x => x.FindByNameAsync("existinguser"))
        .ReturnsAsync(existingUser);

    // Act
    var result = await _authService.RegisterAsync(model, "https://example.com");

    // Assert
    Assert.False(result.IsSuccess);
    Assert.Equal("User already exists!", result.Message);
    
    // Verify CreateAsync was NOT called
    _mockUserManager.Verify(x => x.CreateAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>()), Times.Never);
}
```

### Test Case #2: Create user fails (single error)
```csharp
[Fact]
public async Task RegisterAsync_WhenCreateUserFails_ReturnsErrorWithDescription()
{
    // Arrange
    var model = new RegisterModel { Username = "newuser", Email = "new@test.com", Password = "weak" };
    
    _mockUserManager.Setup(x => x.FindByNameAsync("newuser"))
        .ReturnsAsync((ApplicationUser)null);
    
    _mockUserManager.Setup(x => x.CreateAsync(It.IsAny<ApplicationUser>(), "weak"))
        .ReturnsAsync(IdentityResult.Failed(new IdentityError { Description = "Password too weak" }));

    // Act
    var result = await _authService.RegisterAsync(model, "https://example.com");

    // Assert
    Assert.False(result.IsSuccess);
    Assert.Equal("Password too weak", result.Message);
}
```

### Test Case #3: Create user fails (multiple errors)
```csharp
[Fact]
public async Task RegisterAsync_WhenCreateUserFailsWithMultipleErrors_ReturnsJoinedErrors()
{
    // Arrange
    var model = new RegisterModel { Username = "newuser", Email = "invalid", Password = "weak" };
    
    _mockUserManager.Setup(x => x.FindByNameAsync("newuser"))
        .ReturnsAsync((ApplicationUser)null);
    
    var errors = new[]
    {
        new IdentityError { Description = "Password too weak" },
        new IdentityError { Description = "Invalid email format" }
    };
    _mockUserManager.Setup(x => x.CreateAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>()))
        .ReturnsAsync(IdentityResult.Failed(errors));

    // Act
    var result = await _authService.RegisterAsync(model, "https://example.com");

    // Assert
    Assert.False(result.IsSuccess);
    Assert.Equal("Password too weak; Invalid email format", result.Message);
}
```

### Test Case #4: Role not exists - creates role
```csharp
[Fact]
public async Task RegisterAsync_WhenRoleNotExists_CreatesRoleAndReturnsOk()
{
    // Arrange
    var model = new RegisterModel { Username = "newuser", Email = "new@test.com", Password = "Pass123!" };
    
    _mockUserManager.Setup(x => x.FindByNameAsync("newuser"))
        .ReturnsAsync((ApplicationUser)null);
    _mockUserManager.Setup(x => x.CreateAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>()))
        .ReturnsAsync(IdentityResult.Success);
    _mockUserManager.Setup(x => x.AddToRoleAsync(It.IsAny<ApplicationUser>(), UserRoles.PLAYER))
        .ReturnsAsync(IdentityResult.Success);
    _mockUserManager.Setup(x => x.GenerateEmailConfirmationTokenAsync(It.IsAny<ApplicationUser>()))
        .ReturnsAsync("token");
    
    _mockRoleManager.Setup(x => x.RoleExistsAsync(UserRoles.PLAYER))
        .ReturnsAsync(false); // Role does NOT exist
    _mockRoleManager.Setup(x => x.CreateAsync(It.IsAny<IdentityRole>()))
        .ReturnsAsync(IdentityResult.Success);

    // Act
    var result = await _authService.RegisterAsync(model, "https://example.com");

    // Assert
    Assert.True(result.IsSuccess);
    _mockRoleManager.Verify(x => x.CreateAsync(It.Is<IdentityRole>(r => r.Name == UserRoles.PLAYER)), Times.Once);
}
```

### Test Case #5: Role exists - skips creation
```csharp
[Fact]
public async Task RegisterAsync_WhenRoleExists_SkipsRoleCreationAndReturnsOk()
{
    // Arrange
    var model = new RegisterModel { Username = "newuser", Email = "new@test.com", Password = "Pass123!" };
    
    // ... setup user manager mocks ...
    
    _mockRoleManager.Setup(x => x.RoleExistsAsync(UserRoles.PLAYER))
        .ReturnsAsync(true); // Role EXISTS

    // Act
    var result = await _authService.RegisterAsync(model, "https://example.com");

    // Assert
    Assert.True(result.IsSuccess);
    _mockRoleManager.Verify(x => x.CreateAsync(It.IsAny<IdentityRole>()), Times.Never);
}
```

### Test Case #6: Happy path - complete flow
```csharp
[Fact]
public async Task RegisterAsync_WhenAllValid_ReturnsOkMessage()
{
    // Arrange
    var model = new RegisterModel { Username = "newuser", Email = "new@test.com", Password = "Pass123!" };
    
    // Setup all mocks for success path
    _mockUserManager.Setup(x => x.FindByNameAsync("newuser")).ReturnsAsync((ApplicationUser)null);
    _mockUserManager.Setup(x => x.CreateAsync(It.IsAny<ApplicationUser>(), "Pass123!")).ReturnsAsync(IdentityResult.Success);
    _mockUserManager.Setup(x => x.AddToRoleAsync(It.IsAny<ApplicationUser>(), UserRoles.PLAYER)).ReturnsAsync(IdentityResult.Success);
    _mockUserManager.Setup(x => x.GenerateEmailConfirmationTokenAsync(It.IsAny<ApplicationUser>())).ReturnsAsync("token");
    _mockRoleManager.Setup(x => x.RoleExistsAsync(UserRoles.PLAYER)).ReturnsAsync(true);

    // Act
    var result = await _authService.RegisterAsync(model, "https://example.com");

    // Assert
    Assert.True(result.IsSuccess);
    Assert.Equal("User created. Please check your email to confirm.", result.Message);
}
```

### Test Case #7: Verify user properties
```csharp
[Fact]
public async Task RegisterAsync_VerifyUserCreatedWithCorrectProperties()
{
    // Arrange
    var model = new RegisterModel { Username = "newuser", Email = "new@test.com", Password = "Pass123!" };
    ApplicationUser capturedUser = null;
    
    _mockUserManager.Setup(x => x.FindByNameAsync("newuser")).ReturnsAsync((ApplicationUser)null);
    _mockUserManager.Setup(x => x.CreateAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>()))
        .Callback<ApplicationUser, string>((user, _) => capturedUser = user)
        .ReturnsAsync(IdentityResult.Success);
    // ... other setups ...

    // Act
    await _authService.RegisterAsync(model, "https://example.com");

    // Assert
    Assert.NotNull(capturedUser);
    Assert.Equal("newuser", capturedUser.UserName);
    Assert.Equal("new@test.com", capturedUser.Email);
    Assert.NotNull(capturedUser.SecurityStamp);
    Assert.NotEmpty(capturedUser.SecurityStamp);
}
```

---

## Tổng kết Coverage

| Nhánh Code | Test Case IDs |
|:-----------|:--------------|
| `FindByNameAsync is not null` (user exists) | #1 |
| `!result.Succeeded` (single error) | #2 |
| `!result.Succeeded` (multiple errors) | #3 |
| `!RoleExistsAsync` (role not exists) | #4 |
| `RoleExistsAsync` (role exists) | #5 |
| Happy path complete | #6 |
| User properties verification | #7 |
| `AddToRoleAsync` called | #8 |
| `SendEmailConfirmationAsync` called | #9 |

---

## Thống kê

- **Tổng số Test Cases:** 9
- **Happy Path:** 4 (ID #4, #5, #6)
- **Error Cases:** 3 (ID #1, #2, #3)
- **Edge Cases (Verification):** 3 (ID #7, #8, #9)

**Code Coverage dự kiến:** 100% cho hàm `RegisterAsync`

---

## ⚠️ Đề xuất cải thiện Code

1. **Thêm null check cho `model`:**
   ```csharp
   ArgumentNullException.ThrowIfNull(model);
   ```

2. **Thêm null check cho `baseUri`:**
   ```csharp
   ArgumentException.ThrowIfNullOrWhiteSpace(baseUri);
   ```

3. **Handle exception từ `SendEmailConfirmationAsync`:** Hiện tại nếu gửi email thất bại, user đã được tạo nhưng không có email xác nhận.

