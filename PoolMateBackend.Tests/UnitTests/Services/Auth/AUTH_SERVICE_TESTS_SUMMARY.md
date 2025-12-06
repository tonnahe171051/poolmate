# AUTH SERVICE - UNIT TEST CASES SUMMARY

## Tổng quan

| Method | Số Test Cases | File |
|--------|---------------|------|
| `LoginAsync` | 8 | `AuthServiceLoginTests.cs` |
| `RegisterAsync` | 4 | `AuthServiceRegisterTests.cs` |
| `ChangePasswordAsync` | 4 | `AuthServiceChangePasswordTests.cs` |
| **Tổng cộng** | **16** | |

---

## 1. LoginAsync Tests

**File:** `AuthServiceLoginTests.cs`

### 1.1. Login_UserNotFound_ThrowsException
| Thuộc tính | Giá trị |
|------------|---------|
| **Loại** | Invalid Case |
| **Mô tả** | User không tồn tại trong hệ thống |
| **Setup** | `FindByNameAsync` trả về `null` |
| **Expected** | Throw `InvalidOperationException` với message "Invalid username or password." |

### 1.2. Login_AccountLocked_ThrowsException
| Thuộc tính | Giá trị |
|------------|---------|
| **Loại** | Invalid Case |
| **Mô tả** | Tài khoản bị khóa (LockoutEnd > Now) |
| **Setup** | User có `LockoutEnd = DateTimeOffset.UtcNow.AddDays(1)` |
| **Expected** | Throw `InvalidOperationException` với message "This account has been locked. Please contact administrator." |

### 1.3. Login_LockoutExpired_ReturnsToken
| Thuộc tính | Giá trị |
|------------|---------|
| **Loại** | Edge Case (Boundary) |
| **Mô tả** | Tài khoản đã từng bị khóa nhưng thời gian khóa đã hết hạn |
| **Setup** | User có `LockoutEnd = DateTimeOffset.UtcNow.AddMinutes(-10)` (quá khứ) |
| **Expected** | Đăng nhập thành công, trả về Token |

### 1.4. Login_WrongPassword_ThrowsException
| Thuộc tính | Giá trị |
|------------|---------|
| **Loại** | Invalid Case |
| **Mô tả** | Sai mật khẩu |
| **Setup** | `CheckPasswordAsync` trả về `false` |
| **Expected** | Throw `InvalidOperationException` với message "Invalid username or password." |

### 1.5. Login_EmailNotConfirmed_ThrowsException
| Thuộc tính | Giá trị |
|------------|---------|
| **Loại** | Invalid Case |
| **Mô tả** | Email chưa được xác thực |
| **Setup** | User có `EmailConfirmed = false` |
| **Expected** | Throw `InvalidOperationException` với message "Email is not confirmed." |

### 1.6. Login_UsernameWithSpaces_ReturnsToken
| Thuộc tính | Giá trị |
|------------|---------|
| **Loại** | Edge Case (Input Boundary) |
| **Mô tả** | Username có khoảng trắng đầu/cuối |
| **Setup** | Input `"  testuser  "`, Mock tìm `"testuser"` |
| **Expected** | Hệ thống tự trim, đăng nhập thành công |

### 1.7. Login_NullInput_ThrowsArgumentNullException
| Thuộc tính | Giá trị |
|------------|---------|
| **Loại** | Invalid Case (Null Input) |
| **Mô tả** | Input model là null |
| **Setup** | Gọi `LoginAsync(null)` |
| **Expected** | Throw `ArgumentNullException` |

### 1.8. Login_Success_ReturnsToken ⭐ Happy Path
| Thuộc tính | Giá trị |
|------------|---------|
| **Loại** | Happy Path |
| **Mô tả** | Đăng nhập thành công với thông tin hợp lệ |
| **Setup** | User hợp lệ, password đúng, email đã confirm |
| **Expected** | Trả về Token, UserId, UserName, Email, Roles |

---

## 2. RegisterAsync Tests

**File:** `AuthServiceRegisterTests.cs`

### 2.1. Register_UserAlreadyExists_ReturnsError
| Thuộc tính | Giá trị |
|------------|---------|
| **Loại** | Invalid Case |
| **Mô tả** | Username đã tồn tại trong hệ thống |
| **Setup** | `FindByNameAsync` trả về User object |
| **Expected** | Trả về `Response.Error("User already exists!")` |
| **Verify** | `CreateAsync` KHÔNG được gọi |

### 2.2. Register_CreateFailed_ReturnsIdentityErrors
| Thuộc tính | Giá trị |
|------------|---------|
| **Loại** | Invalid Case |
| **Mô tả** | Tạo user thất bại (ví dụ: password yếu) |
| **Setup** | `CreateAsync` trả về `IdentityResult.Failed` với errors |
| **Expected** | Trả về Error chứa message từ Identity errors |
| **Verify** | `AddToRoleAsync` KHÔNG được gọi |

### 2.3. Register_Success_RoleDoesNotExist_CreatesRoleThenSendsEmail
| Thuộc tính | Giá trị |
|------------|---------|
| **Loại** | Edge Case |
| **Mô tả** | Role "Player" chưa tồn tại trong DB |
| **Setup** | `RoleExistsAsync` trả về `false` |
| **Expected** | Trả về Success |
| **Verify** | `RoleManager.CreateAsync` được gọi 1 lần, `AddToRoleAsync` được gọi 1 lần, Email được gửi |

### 2.4. Register_Success_RoleExists_SendsEmail ⭐ Happy Path
| Thuộc tính | Giá trị |
|------------|---------|
| **Loại** | Happy Path |
| **Mô tả** | Đăng ký thành công với role đã tồn tại |
| **Setup** | `RoleExistsAsync` trả về `true` |
| **Expected** | Trả về `Response.Ok("User created. Please check your email to confirm.")` |
| **Verify** | `RoleManager.CreateAsync` KHÔNG được gọi, `AddToRoleAsync` được gọi 1 lần, Email được gửi 1 lần |

---

## 3. ChangePasswordAsync Tests

**File:** `AuthServiceChangePasswordTests.cs`

### 3.1. ChangePassword_UserNotFound_ReturnsError
| Thuộc tính | Giá trị |
|------------|---------|
| **Loại** | Invalid Case |
| **Mô tả** | User không tồn tại |
| **Setup** | `FindByIdAsync` trả về `null` |
| **Expected** | Trả về `Response.Error("User not found")` |
| **Verify** | `CheckPasswordAsync` KHÔNG được gọi |

### 3.2. ChangePassword_WrongCurrentPassword_ReturnsError
| Thuộc tính | Giá trị |
|------------|---------|
| **Loại** | Invalid Case |
| **Mô tả** | Mật khẩu hiện tại không đúng |
| **Setup** | `CheckPasswordAsync` trả về `false` |
| **Expected** | Trả về `Response.Error("Current password is incorrect")` |
| **Verify** | `ChangePasswordAsync` KHÔNG được gọi |

### 3.3. ChangePassword_IdentityFailed_ReturnsError
| Thuộc tính | Giá trị |
|------------|---------|
| **Loại** | Edge Case |
| **Mô tả** | Mật khẩu mới không đạt yêu cầu (quá ngắn, thiếu số...) |
| **Setup** | `ChangePasswordAsync` trả về `IdentityResult.Failed` |
| **Expected** | Trả về Error chứa message lỗi từ Identity |

### 3.4. ChangePassword_Success_ReturnsOk ⭐ Happy Path
| Thuộc tính | Giá trị |
|------------|---------|
| **Loại** | Happy Path |
| **Mô tả** | Đổi mật khẩu thành công |
| **Setup** | Tất cả các bước đều thành công |
| **Expected** | Trả về `Response.Ok("Password changed successfully")` |
| **Verify** | `ChangePasswordAsync` được gọi đúng 1 lần |

---

## Test Coverage Matrix

### Theo loại Test Case

| Loại | Số lượng | Tỷ lệ |
|------|----------|-------|
| Happy Path | 3 | 18.75% |
| Invalid Case | 9 | 56.25% |
| Edge Case / Boundary | 4 | 25% |
| **Tổng** | **16** | **100%** |

### Theo Method

```
LoginAsync:       ████████ 8 tests
RegisterAsync:    ████ 4 tests  
ChangePassword:   ████ 4 tests
```

---

## Cấu trúc thư mục

```
PoolMateBackend.Tests/
└── UnitTests/
    └── Services/
        └── Auth/
            ├── AuthServiceTestBase.cs      (Base class với Mocks)
            ├── AuthServiceLoginTests.cs    (8 tests)
            ├── AuthServiceRegisterTests.cs (4 tests)
            └── AuthServiceChangePasswordTests.cs (4 tests)
```

---

## Cách chạy Tests

```powershell
# Chạy tất cả Auth tests
dotnet test --filter "FullyQualifiedName~AuthService"

# Chạy riêng Login tests
dotnet test --filter "AuthServiceLoginTests"

# Chạy riêng Register tests
dotnet test --filter "AuthServiceRegisterTests"

# Chạy riêng ChangePassword tests
dotnet test --filter "AuthServiceChangePasswordTests"

# Chạy với verbose output
dotnet test --filter "AuthService" --verbosity normal
```

---

## Ghi chú

- Tất cả tests đều sử dụng **Moq** để mock dependencies
- Tuân thủ mô hình **AAA** (Arrange-Act-Assert)
- Base class `AuthServiceTestBase` chứa helper methods để mock `UserManager` và `RoleManager`
- Sử dụng `Times.Never` và `Times.Once` để verify behavior

---

**Cập nhật lần cuối:** 6/12/2025  
**Tổng số Test Cases:** 16

