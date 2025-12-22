# Decision Table - AuthService.RegisterAsync

**Phương thức kiểm thử:** Solitary Unit Testing với Moq  
**SUT (System Under Test):** `AuthService.RegisterAsync(RegisterModel model, string baseUri)`  
**Mục đích:** Đăng ký tài khoản người dùng mới và gửi email xác nhận

---

## Bảng Quyết Định (Decision Table)

| # | Test Case Name | Type | Inputs (Conditions) | Expected Return | Expected Exception | Mock Verification |
|---|----------------|------|---------------------|-----------------|-------------------|-------------------|
| 1 | `RegisterAsync_WhenUserAlreadyExists_ReturnsError` | **A** | `Username = "existinguser"`, `Email = "test@test.com"`, `Password = "Pass123!"`, `FindByNameAsync returns existing user` | `Response.Error` với `Status = "Error"`, `Message = "User already exists!"` | - | `CreateAsync` **NOT** called |
| 2 | `RegisterAsync_WhenCreateUserFails_ReturnsErrorWithDescription` | **A** | `Username = "newuser"`, `Email = "new@test.com"`, `Password = "weak"`, `User not exist`, `CreateAsync returns Failed with error "Password too weak"` | `Response.Error` với `Status = "Error"`, `Message = "Password too weak"` | - | `AddToRoleAsync` **NOT** called |
| 3 | `RegisterAsync_WhenCreateUserFailsWithMultipleErrors_ReturnsJoinedErrors` | **A** | `Username = "newuser"`, `Email = "invalid"`, `Password = "weak"`, `User not exist`, `CreateAsync returns Failed with errors ["Password too weak", "Invalid email format"]` | `Response.Error` với `Status = "Error"`, `Message = "Password too weak; Invalid email format"` | - | Multiple errors joined by `;` |
| 4 | `RegisterAsync_WhenRoleNotExists_CreatesRoleAndReturnsOk` | **N** | `Username = "newuser"`, `Email = "new@test.com"`, `Password = "Pass123!"`, `User not exist`, `CreateAsync = Success`, `RoleExistsAsync = false` | `Response.Ok` với `Status = "Success"` | - | `RoleManager.CreateAsync` called **once** with PLAYER role |
| 5 | `RegisterAsync_WhenRoleExists_SkipsRoleCreationAndReturnsOk` | **N** | `Username = "newuser"`, `Email = "new@test.com"`, `Password = "Pass123!"`, `User not exist`, `CreateAsync = Success`, `RoleExistsAsync = true` | `Response.Ok` với `Status = "Success"` | - | `RoleManager.CreateAsync` **NOT** called |
| 6 | `RegisterAsync_WhenAllValid_ReturnsOkMessage` | **N** | `Username = "newuser"`, `Email = "new@test.com"`, `Password = "Pass123!"`, `User not exist`, `All operations succeed` | `Response.Ok` với `Status = "Success"`, `Message = "User created. Please check your email to confirm."` | - | Complete registration flow |

---

## Bảng Xác Minh Logic (Verification Table)

| # | Test Case Name | Type | Purpose | Mock Verification Details |
|---|----------------|------|---------|---------------------------|
| 7 | `RegisterAsync_VerifyUserCreatedWithCorrectProperties` | **N** | Xác minh `ApplicationUser` được tạo với properties đúng | `capturedUser.UserName = "newuser"`, `capturedUser.Email = "new@test.com"`, `capturedUser.SecurityStamp != null && != empty` |
| 8 | `RegisterAsync_VerifyAddToRoleAsyncCalledWithPlayerRole` | **N** | Xác minh user được gán role PLAYER | `AddToRoleAsync(user, UserRoles.PLAYER)` called **exactly once** |
| 9 | `RegisterAsync_VerifySendEmailConfirmationAsyncCalled` | **N** | Xác minh email xác nhận được gửi | `EmailSender.SendAsync` called with: `To = "new@test.com"`, `Subject = "Confirm your email"`, `Body contains "Hi newuser" and baseUri "https://example.com"` |
| 10 | `RegisterAsync_VerifyGenerateEmailConfirmationTokenAsyncCalled` | **N** | Xác minh token xác nhận email được tạo | `GenerateEmailConfirmationTokenAsync(user)` called **exactly once** |

---

## Ghi Chú Phân Loại Test Cases

### **Normal (N) - Các trường hợp hợp lệ:**
- Test Case #4-5: Đăng ký thành công với role tồn tại/không tồn tại
- Test Case #6: Happy path - toàn bộ flow đăng ký thành công
- Test Case #7-10: Verification tests - Xác minh các hành động trong quá trình đăng ký

### **Abnormal (A) - Các trường hợp bất thường:**
- Test Case #1: Username đã tồn tại trong hệ thống
- Test Case #2: Tạo user thất bại (lỗi đơn)
- Test Case #3: Tạo user thất bại (nhiều lỗi)

### **Boundary (B) - Các trường hợp biên:**
- **Không có boundary test case** trong test suite này
- **Gợi ý bổ sung:** Có thể thêm test cases cho:
  - Username/Email null hoặc empty
  - Password null hoặc empty
  - BaseUri null hoặc empty
  - Username độ dài tối thiểu/tối đa
  - Email format không hợp lệ

---

## Kỹ Thuật Tối Ưu Hóa Áp Dụng

**Equivalence Partitioning:**
- Test Case #4 và #5 kiểm tra 2 nhánh logic khác nhau (Role exists vs Role not exists) → **Giữ lại cả 2**
- Test Case #6-10 đều là "Normal" nhưng mỗi test có mục đích khác nhau:
  - #6: Kiểm tra response message
  - #7: Kiểm tra user properties
  - #8: Kiểm tra role assignment
  - #9: Kiểm tra email sending
  - #10: Kiểm tra token generation
  - → **Giữ lại tất cả** vì mỗi test verify một aspect khác nhau của logic

**Kết luận tối ưu hóa:**
- **Không loại bỏ test case nào** vì mỗi test đều kiểm tra một khía cạnh logic riêng biệt
- Test suite này đã được tổ chức tốt với clear separation of concerns

---

## Input Domain Analysis

### **RegisterModel.Username:**
- **Existing username:** Test Case #1 (Abnormal - returns error)
- **New username:** Test Case #2-10 (Normal flow)
- **Null/Empty:** ❌ **Missing** (should add boundary test)

### **RegisterModel.Email:**
- **Valid email:** Test Case #2-10
- **Invalid email (in combined test):** Test Case #3
- **Null/Empty:** ❌ **Missing** (should add boundary test)

### **RegisterModel.Password:**
- **Valid password:** Test Case #1, #4-10
- **Weak password:** Test Case #2-3
- **Null/Empty:** ❌ **Missing** (should add boundary test)

### **UserManager.CreateAsync Result:**
- **Success:** Test Case #4-10
- **Failed (single error):** Test Case #2
- **Failed (multiple errors):** Test Case #3

### **RoleManager.RoleExistsAsync:**
- **True (role exists):** Test Case #5
- **False (role not exists):** Test Case #4

### **UserManager.FindByNameAsync:**
- **Returns existing user:** Test Case #1
- **Returns null (user not exist):** Test Case #2-10

---

## Flow Execution Analysis

### **Complete Registration Flow (Happy Path - Test Case #6):**
```
1. FindByNameAsync("newuser") → null (user not exist) ✅
2. CreateAsync(ApplicationUser, "Pass123!") → Success ✅
3. RoleExistsAsync("Player") → true ✅
4. AddToRoleAsync(user, "Player") → Success ✅
5. GenerateEmailConfirmationTokenAsync(user) → "test-token" ✅
6. EmailSender.SendAsync("new@test.com", ...) → Task.CompletedTask ✅
7. Return Response.Ok("User created. Please check your email to confirm.") ✅
```

### **Early Exit Scenarios:**
- **User exists:** Exit tại bước 1 → Return error (Test Case #1)
- **CreateAsync fails:** Exit tại bước 2 → Return error (Test Case #2-3)

---

## Mock Setup Details

### **Successful Registration Mocks (Helper Method):**
```csharp
SetupSuccessfulRegistrationMocks(model) configures:
├── FindByNameAsync → null (user not exist)
├── CreateAsync → IdentityResult.Success
├── AddToRoleAsync → IdentityResult.Success
├── GenerateEmailConfirmationTokenAsync → "test-confirmation-token"
├── RoleExistsAsync → true
└── EmailSender.SendAsync → Task.CompletedTask
```

---

## Tổng Kết

- **Tổng số test cases:** 10 (không loại bỏ test case nào)
- **Normal:** 7 test cases (#4-10)
- **Abnormal:** 3 test cases (#1-3)
- **Boundary:** 0 test cases (thiếu - nên bổ sung)
- **Độ bao phủ:** Bao phủ tốt các luồng chính (happy path, error handling, verification)
- **Khuyến nghị:** Nên bổ sung boundary tests cho null/empty inputs và edge cases về độ dài username/email/password

---

## Khuyến Nghị Bổ Sung Test Cases

### **Boundary Tests cần thêm:**
1. `RegisterAsync_WhenUsernameIsNull_ThrowsException`
2. `RegisterAsync_WhenEmailIsNull_ThrowsException`
3. `RegisterAsync_WhenPasswordIsNull_ThrowsException`
4. `RegisterAsync_WhenBaseUriIsNull_StillWorks` (hoặc throws exception)
5. `RegisterAsync_WhenUsernameExceedsMaxLength_ReturnsError`
6. `RegisterAsync_WhenEmailIsInvalidFormat_ReturnsError`

### **Additional Normal Tests:**
7. `RegisterAsync_WhenAddToRoleFails_ReturnsError` (test AddToRoleAsync failure)
8. `RegisterAsync_WhenEmailSendingFails_StillReturnsSuccess` (test email failure handling)

