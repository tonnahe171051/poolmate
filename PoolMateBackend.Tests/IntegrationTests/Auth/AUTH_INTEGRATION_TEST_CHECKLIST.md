# Auth Module - Integration Test Checklist

> **Module:** AuthController & AuthService  
> **Strategy:** Lean & Effective (80/20 Rule)  
> **Last Updated:** 2025-12-08

---

## 📋 Summary

| Endpoint | Method | Auth Required | Total Tests |
|:---------|:-------|:--------------|:------------|
| `/api/auth/register` | POST | ❌ Anonymous | 3 |
| `/api/auth/confirm-email` | GET | ❌ Anonymous | 2 |
| `/api/auth/login` | POST | ❌ Anonymous | 4 |
| `/api/auth/forgot-password` | POST | ❌ Anonymous | 2 |
| `/api/auth/reset-password` | POST | ❌ Anonymous | 2 |
| `/api/auth/change-password` | POST | ✅ Authorized | 3 |
| **TOTAL** | | | **16** |

---

## 🧪 Test Cases

### 1. Register (`POST /api/auth/register`)

| ID | Category | Scenario Name | Pre-Condition (Arrange) | Action (Act) | Critical Verification (Assert) |
|:---|:---------|:--------------|:------------------------|:-------------|:------------------------------|
| AUTH-REG-01 | ✅ Positive | Register_ValidUser_Success | No existing user with same username/email | POST valid `RegisterModel` (username, email, password, confirmPassword) | Returns 200 OK, Status="Success", User created in DB with `EmailConfirmed=false`, Role="Player" assigned |
| AUTH-REG-02 | ⚠️ Negative | Register_DuplicateUsername_Fails | User "testuser" already exists | POST with same username | Returns 400 BadRequest, Message contains "User already exists" |
| AUTH-REG-03 | ⚠️ Negative | Register_InvalidModel_Fails | None | POST with missing required field (e.g., empty Username) | Returns 400 BadRequest, Validation error message |

---

### 2. Confirm Email (`GET /api/auth/confirm-email`)

| ID | Category | Scenario Name | Pre-Condition (Arrange) | Action (Act) | Critical Verification (Assert) |
|:---|:---------|:--------------|:------------------------|:-------------|:------------------------------|
| AUTH-CONF-01 | ✅ Positive | ConfirmEmail_ValidToken_Success | Registered user with `EmailConfirmed=false`, valid confirmation token generated | GET with valid `userId` and `token` | Returns 200 OK, User's `EmailConfirmed=true` in DB |
| AUTH-CONF-02 | 💣 Edge | ConfirmEmail_InvalidToken_Fails | Registered user exists | GET with valid `userId` but invalid/expired `token` | Returns 400 BadRequest, Message contains "Invalid or expired token" |

---

### 3. Login (`POST /api/auth/login`)

| ID | Category | Scenario Name | Pre-Condition (Arrange) | Action (Act) | Critical Verification (Assert) |
|:---|:---------|:--------------|:------------------------|:-------------|:------------------------------|
| AUTH-LOGIN-01 | ✅ Positive | Login_ValidCredentials_ReturnsToken | User registered with `EmailConfirmed=true` | POST valid `LoginModel` | Returns 200 OK with `token`, `expiration`, `userId`, `userName`, `userEmail`, `roles` array |
| AUTH-LOGIN-02 | ⚠️ Negative | Login_InvalidPassword_Fails | User exists with confirmed email | POST with correct username, wrong password | Returns 401 Unauthorized (via exception) |
| AUTH-LOGIN-03 | ⚠️ Negative | Login_EmailNotConfirmed_Returns403 | User exists with `EmailConfirmed=false` | POST valid credentials | Returns 403 Forbidden, Message="Email is not confirmed." |
| AUTH-LOGIN-04 | 🔒 Security | Login_LockedAccount_Fails | User exists with `LockoutEnd` in the future | POST valid credentials | Throws exception "This account has been locked" |

---

### 4. Forgot Password (`POST /api/auth/forgot-password`)

| ID | Category | Scenario Name | Pre-Condition (Arrange) | Action (Act) | Critical Verification (Assert) |
|:---|:---------|:--------------|:------------------------|:-------------|:------------------------------|
| AUTH-FORGOT-01 | ✅ Positive | ForgotPassword_ValidEmail_SendsResetLink | User exists with `EmailConfirmed=true` | POST valid `ForgotPasswordModel` | Returns 200 OK, Status="Success" (generic message for security) |
| AUTH-FORGOT-02 | 🔒 Security | ForgotPassword_NonExistentEmail_NoLeak | No user with given email | POST with non-existent email | Returns 200 OK with same generic message (prevents email enumeration) |

---

### 5. Reset Password (`POST /api/auth/reset-password`)

| ID | Category | Scenario Name | Pre-Condition (Arrange) | Action (Act) | Critical Verification (Assert) |
|:---|:---------|:--------------|:------------------------|:-------------|:------------------------------|
| AUTH-RESET-01 | ✅ Positive | ResetPassword_ValidToken_Success | User requested password reset, has valid token | POST valid `ResetPasswordModel` (userId, token, newPassword) | Returns 200 OK, User can login with new password |
| AUTH-RESET-02 | 💣 Edge | ResetPassword_InvalidToken_Fails | User exists | POST with invalid/expired token | Returns 400 BadRequest, Message contains "Invalid or expired token" |

---

### 6. Change Password (`POST /api/auth/change-password`) - 🔒 Protected

| ID | Category | Scenario Name | Pre-Condition (Arrange) | Action (Act) | Critical Verification (Assert) |
|:---|:---------|:--------------|:------------------------|:-------------|:------------------------------|
| AUTH-CHANGE-01 | ✅ Positive | ChangePassword_ValidCurrentPassword_Success | User authenticated via JWT | POST valid `ChangePasswordModel` with correct current password | Returns 200 OK, User can login with new password, old password no longer works |
| AUTH-CHANGE-02 | ⚠️ Negative | ChangePassword_WrongCurrentPassword_Fails | User authenticated via JWT | POST with incorrect current password | Returns 400 BadRequest, Message="Current password is incorrect" |
| AUTH-CHANGE-03 | 🔒 Security | ChangePassword_Unauthorized_Returns401 | No authentication token / Invalid token | POST any data | Returns 401 Unauthorized |

---

## 📊 Coverage Matrix

| Logic Path | Covered By |
|:-----------|:-----------|
| User Registration Flow | AUTH-REG-01, AUTH-REG-02, AUTH-REG-03 |
| Email Confirmation Flow | AUTH-CONF-01, AUTH-CONF-02 |
| JWT Token Generation | AUTH-LOGIN-01 |
| Password Validation | AUTH-LOGIN-02, AUTH-CHANGE-02 |
| Email Confirmation Guard | AUTH-LOGIN-03, AUTH-FORGOT-02 (implicit) |
| Account Lockout Check | AUTH-LOGIN-04 |
| Password Reset Flow | AUTH-FORGOT-01, AUTH-RESET-01, AUTH-RESET-02 |
| Authorized Endpoint Protection | AUTH-CHANGE-03 |
| Token Expiry/Validity | AUTH-CONF-02, AUTH-RESET-02 |
| Email Enumeration Prevention | AUTH-FORGOT-02 |

---

## 🎯 Test Priority

### P0 - Critical (Must Pass)
- AUTH-REG-01: Core registration flow
- AUTH-LOGIN-01: Core login flow with JWT
- AUTH-CHANGE-03: Security - unauthorized access blocked

### P1 - High (Business Critical)
- AUTH-CONF-01: Email confirmation
- AUTH-LOGIN-03: Email not confirmed guard
- AUTH-LOGIN-04: Locked account guard
- AUTH-RESET-01: Password reset flow

### P2 - Medium (Validation)
- AUTH-REG-02, AUTH-REG-03: Registration validation
- AUTH-LOGIN-02: Invalid credentials
- AUTH-CHANGE-01, AUTH-CHANGE-02: Password change

### P3 - Low (Edge Cases)
- AUTH-CONF-02, AUTH-RESET-02: Token validation
- AUTH-FORGOT-01, AUTH-FORGOT-02: Forgot password (email enumeration prevention)

---

## 🔧 Test Implementation Notes

### Required Test Infrastructure
```csharp
// 1. WebApplicationFactory for API testing
// 2. In-memory database or test container for SQL Server
// 3. Mock IEmailSender to capture sent emails/tokens
// 4. Helper methods for:
//    - Creating confirmed/unconfirmed users
//    - Generating valid JWT tokens
//    - Extracting confirmation/reset tokens from mock email
```

### Key Test Data Setup
```csharp
// Confirmed User (for login/change password tests)
var confirmedUser = new ApplicationUser 
{ 
    UserName = "testuser", 
    Email = "test@example.com", 
    EmailConfirmed = true 
};

// Unconfirmed User (for email confirmation tests)
var unconfirmedUser = new ApplicationUser 
{ 
    UserName = "newuser", 
    Email = "new@example.com", 
    EmailConfirmed = false 
};

// Locked User (for lockout tests)
var lockedUser = new ApplicationUser 
{ 
    UserName = "lockeduser", 
    LockoutEnd = DateTimeOffset.UtcNow.AddHours(1) 
};
```

### Assertions Patterns
```csharp
// Success Response Pattern
response.StatusCode.Should().Be(HttpStatusCode.OK);
var content = await response.Content.ReadFromJsonAsync<Response>();
content.Status.Should().Be("Success");

// JWT Token Response Pattern (Login)
var loginResponse = await response.Content.ReadFromJsonAsync<LoginResponseDto>();
loginResponse.Token.Should().NotBeNullOrEmpty();
loginResponse.Roles.Should().Contain("Player");

// DB State Verification
var userInDb = await dbContext.Users.FindAsync(userId);
userInDb.EmailConfirmed.Should().BeTrue();
```

---

## ✅ Checklist Sign-off

- [ ] All P0 tests implemented and passing
- [ ] All P1 tests implemented and passing  
- [ ] All P2 tests implemented and passing
- [ ] All P3 tests implemented and passing
- [ ] Code coverage > 80% for AuthService
- [ ] No flaky tests
- [ ] CI/CD pipeline integration verified

