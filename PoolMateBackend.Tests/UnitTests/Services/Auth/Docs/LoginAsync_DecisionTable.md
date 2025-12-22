# Decision Table - AuthService.LoginAsync

**Phương thức kiểm thử:** Solitary Unit Testing với Moq  
**SUT (System Under Test):** `AuthService.LoginAsync(LoginModel model)`  
**Mục đích:** Xác thực đăng nhập người dùng và tạo JWT token

---

## Bảng Quyết Định (Decision Table)

| # | Test Case Name | Type | Inputs (Conditions) | Expected Return | Expected Exception | Expected Log |
|---|----------------|------|---------------------|-----------------|-------------------|--------------|
| 1 | `LoginAsync_WhenModelIsNull_ThrowsArgumentNullException` | **A** | `model = null` | - | `ArgumentNullException` | - |
| 2 | `LoginAsync_WhenUsernameIsNullOrEmpty_ThrowsInvalidOperationException` | **A** | `Username = null/""/"   "`, `Password = "anyPassword"`, `FindByNameAsync returns null` | - | `InvalidOperationException` với message: "Invalid username or password." | - |
| 3 | `LoginAsync_WhenUserNotFound_ThrowsInvalidOperationException` | **A** | `Username = "nonexistent"`, `Password = "password123"`, `FindByNameAsync returns null` | - | `InvalidOperationException` với message: "Invalid username or password." | - |
| 4 | `LoginAsync_WhenLockoutEndIsInFuture_ThrowsInvalidOperationException` | **B** | `Username = "testuser"`, `Password = "password123"`, `LockoutEnd = UtcNow.AddMinutes(1)` (Above Boundary - LOCKED) | - | `InvalidOperationException` với message: "This account has been locked. Please contact administrator." | - |
| 5 | `LoginAsync_WhenLockoutEndEqualsNow_ReturnsToken` | **B** | `Username = "testuser"`, `Password = "password123"`, `LockoutEnd = UtcNow` (At Boundary - NOT LOCKED), `EmailConfirmed = true`, `Roles = ["Player"]` | Token tuple với `UserId = "user-123"`, `UserName = "testuser"`, `Token` không rỗng | - | - |
| 6 | `LoginAsync_WhenPasswordIsIncorrect_ThrowsInvalidOperationException` | **A** | `Username = "testuser"`, `Password = "wrongpassword"`, `User exists`, `CheckPasswordAsync returns false` | - | `InvalidOperationException` với message: "Invalid username or password." | - |
| 7 | `LoginAsync_WhenEmailNotConfirmed_ThrowsInvalidOperationException` | **A** | `Username = "testuser"`, `Password = "password123"`, `User exists`, `CheckPasswordAsync = true`, `EmailConfirmed = false` | - | `InvalidOperationException` với message: "Email is not confirmed." | - |
| 8 | `LoginAsync_WhenAllValid_ReturnsTokenWithCorrectData` | **N** | `Username = "testuser"`, `Password = "password123"`, `User exists`, `LockoutEnd = null`, `EmailConfirmed = true`, `CheckPasswordAsync = true`, `Roles = ["Player"]` | Token tuple với `UserId = "user-123"`, `UserName = "testuser"`, `Email = "test@example.com"`, `Roles = ["Player"]`, `Token` hợp lệ, `Exp > UtcNow` | - | - |
| 9 | `LoginAsync_WhenUserHasNoRoles_ReturnsTokenWithEmptyRoles` | **N** | `Username = "testuser"`, `Password = "password123"`, `User valid`, `Roles = []` (Empty list) | Token tuple với `Roles = []` (empty), `Token` không rỗng | - | - |
| 10 | `LoginAsync_WhenUserHasMultipleRoles_ReturnsAllRoles` | **N** | `Username = "adminuser"`, `Password = "password123"`, `User valid`, `Roles = ["Admin", "Player"]` | Token tuple với `Roles.Count = 2`, chứa "Admin" và "Player", JWT claims chứa cả 2 roles | - | - |
| 11 | `LoginAsync_WhenUsernameHasWhitespace_TrimsAndFindsUser` | **N** | `Username = "  validuser  "` (có whitespace), `Password = "password123"`, `User exists với username = "validuser"` | Token tuple với `UserId = "user-123"`, `FindByNameAsync` được gọi với "validuser" (đã trim) | - | - |
| 12 | `LoginAsync_WhenUserNameOrEmailIsNull_ReturnsTokenWithEmptyStringsInClaims` | **B** | `Username = "testuser"`, `Password = "password123"`, `User.UserName = null`, `User.Email = null`, `User valid` | Token tuple với `UserName = null`, `Email = null`, nhưng JWT claims chứa empty string ("") cho Name và Email | - | - |

---

## Ghi Chú Phân Loại Test Cases

### **Normal (N) - Các trường hợp hợp lệ:**
- Test Case #8: Đăng nhập thành công với đầy đủ thông tin hợp lệ (Happy Path)
- Test Case #9-11: Các biến thể của trường hợp hợp lệ (user có/không có roles, username có whitespace)

### **Abnormal (A) - Các trường hợp bất thường:**
- Test Case #1: Model null
- Test Case #2-3: Username null/empty/không tồn tại
- Test Case #6: Mật khẩu sai
- Test Case #7: Email chưa xác nhận

### **Boundary (B) - Các trường hợp biên:**
- Test Case #4: `LockoutEnd` trong tương lai (Above Boundary - bị khóa)
- Test Case #5: `LockoutEnd` đúng bằng thời gian hiện tại (At Boundary - không bị khóa, vì dùng `>` không phải `>=`)
- Test Case #12: UserName và Email là null (Boundary của null value handling)

---

## Kỹ Thuật Tối Ưu Hóa Áp Dụng

**Equivalence Partitioning:**
- Đã loại bỏ Test Case #6 (`LoginAsync_WhenLockoutEndIsInPast_ContinuesLogin`) vì nó tương đương với Test Case #7 (`LoginAsync_WhenLockoutEndIsNull_ContinuesLogin`) - cả hai đều kiểm tra trường hợp "Không bị khóa và đăng nhập thành công"
- Giữ lại Test Case #2 (Theory với 3 InlineData) vì nó kiểm tra nhiều giá trị tương đương trong 1 test

---

## Input Domain Analysis

### **Username:**
- **Null/Empty/Whitespace:** Test Case #2
- **Không tồn tại trong DB:** Test Case #3
- **Có whitespace leading/trailing:** Test Case #11
- **Valid:** Test Case #5, #8-10, #12

### **Password:**
- **Đúng:** Test Case #5, #8-12
- **Sai:** Test Case #6
- **Any:** Test Case #2

### **User.LockoutEnd:**
- **Null:** Test Case #8-11 (không bị khóa)
- **In Future (> UtcNow):** Test Case #4 (bị khóa)
- **Equals UtcNow:** Test Case #5 (không bị khóa, boundary)
- **In Past (< UtcNow):** (Đã loại bỏ - tương đương null)

### **User.EmailConfirmed:**
- **True:** Test Case #5, #8-12
- **False:** Test Case #7

### **User.Roles:**
- **Empty []:** Test Case #9
- **Single role ["Player"]:** Test Case #5, #8, #11
- **Multiple roles ["Admin", "Player"]:** Test Case #10

### **User.UserName/Email:**
- **Valid strings:** Test Case #5, #8-11
- **Null:** Test Case #12

---

## Tổng Kết

- **Tổng số test cases:** 12 (đã tối ưu hóa từ 14 ban đầu)
- **Normal:** 4 test cases
- **Abnormal:** 5 test cases
- **Boundary:** 3 test cases
- **Độ bao phủ:** Bao phủ tất cả các điều kiện logic chính trong LoginAsync (validation, lockout, password, email confirmation, roles)

