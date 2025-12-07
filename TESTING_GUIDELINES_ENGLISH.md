# POOLMATE BACKEND TESTING GUIDELINES

## TABLE OF CONTENTS
1. [Testing Methodology](#1-testing-methodology)
2. [Input Value Selection Rules](#2-input-value-selection-rules)
3. [Naming Conventions](#3-naming-conventions)
4. [Code Templates](#4-code-templates)
5. [Prohibited Practices](#5-prohibited-practices)
6. [Testing Focus Areas](#6-testing-focus-areas)

---

## 1. TESTING METHODOLOGY
**Applied to 100% of Unit Tests**

### Primary Methodology
**Solitary Unit Testing (Isolated Unit Testing)** combined with **White-box Testing**

### Core Principles

#### ✅ Complete Isolation
- Test each class independently
- Everything external (Database, other APIs, other Services) **MUST be Mocked**
- Use library: **Moq**

#### ✅ Code Structure
**MANDATORY** adherence to the **AAA** pattern:
- **Arrange**: Initialize data, mock dependencies
- **Act**: Call the method under test
- **Assert**: Verify results

---

## 2. INPUT VALUE SELECTION RULES
**Don't choose randomly!** Apply the **"3 Golden Points"** formula based on **Boundary Value Analysis**

### The 3 Golden Points Formula

For each logical condition (e.g., `if (count > 0 && count <= 10)`), you must write **3 test cases**:

#### 1️⃣ Valid Value (Happy Path)
- **Description**: Choose a number in the middle of the valid range
- **Example**: `5` (for range 1-10)
- **Expected**: `True` / Success

#### 2️⃣ Boundary Value (Edge Case)
- **Description**: Choose numbers at the exact limits
- **Example**: `1`, `10` (for range 1-10)
- **Expected**: Handle correctly according to code logic

#### 3️⃣ Invalid/Exception Value (Invalid Case)
- **Description**: Choose numbers outside the range or `null`
- **Example**: `-1`, `100`, `null`
- **Expected**: `False` or throw Exception

### ⚠️ Mandatory Rule for Objects
If a method has an Object parameter (`User`, `Order`, `Tournament`...), you **MUST** test the case where that parameter is `null`.

### Example

```csharp
// Method to test
public bool ValidatePlayerCount(int count)
{
    return count > 0 && count <= 32;
}

// 3 Mandatory Test Cases:
[Fact]
public void ValidatePlayerCount_ValidValue_ReturnsTrue()
{
    // Middle value: 16
    var result = _sut.ValidatePlayerCount(16);
    Assert.True(result);
}

[Fact]
public void ValidatePlayerCount_BoundaryValue_ReturnsTrue()
{
    // Boundary values: 1 and 32
    Assert.True(_sut.ValidatePlayerCount(1));
    Assert.True(_sut.ValidatePlayerCount(32));
}

[Fact]
public void ValidatePlayerCount_OutOfRange_ReturnsFalse()
{
    // Out of range values: 0, -1, 33
    Assert.False(_sut.ValidatePlayerCount(0));
    Assert.False(_sut.ValidatePlayerCount(-1));
    Assert.False(_sut.ValidatePlayerCount(33));
}
```

---

## 3. NAMING CONVENTIONS

### A. Project & Folder Names

#### Test Project
```
PoolMateBackend.Tests
```

#### Folder Structure
**MUST** mirror the main project structure:

```
Main Project:
PoolMateBackend/
  └── Services/
      └── TournamentService.cs

Test Project:
PoolMateBackend.Tests/
  └── UnitTests/
      └── Services/
          └── TournamentServiceTests.cs
```

### B. Test Method Names

#### Formula
```
MethodName_Scenario_ExpectedResult
```

**Explanation:**
- **MethodName**: Name of the method in the Service being tested
- **Scenario**: What is the input situation?
- **ExpectedResult**: What will the method return?

#### Real Examples

```csharp
// ✅ CORRECT
Login_EmailDoesNotExist_ReturnsFalse()
CreateTournament_EmptyName_ThrowsValidationException()
CalculateScore_ValidData_ReturnsCorrectScore()
GetTournamentById_InvalidId_ReturnsNull()
UpdateMatch_MatchNotFound_ReturnsFalse()

// ❌ WRONG
TestLogin()
Test1()
LoginTest()
CheckEmail()
```

### C. Variable Names in Test Code

| Variable Type | Name | Example |
|--------------|------|---------|
| Object under test | `_sut` or `_service` | `private readonly TournamentService _sut;` |
| Mock object | Prefix `mock` | `mockRepo`, `mockEmailService` |
| Expected data | `expected` | `var expected = 100;` |
| Actual data | `actual` or `result` | `var actual = _sut.Calculate();` |

#### Complete Example

```csharp
public class TournamentServiceTests
{
    // Mock objects
    private readonly Mock<ITournamentRepository> _mockTournamentRepo;
    private readonly Mock<IEmailService> _mockEmailService;
    
    // System Under Test
    private readonly TournamentService _sut;
    
    [Fact]
    public void GetTournamentCount_ReturnsCorrectNumber()
    {
        // Arrange
        var expected = 10;
        _mockTournamentRepo.Setup(x => x.Count()).Returns(expected);
        
        // Act
        var actual = _sut.GetTournamentCount();
        
        // Assert
        Assert.Equal(expected, actual);
    }
}
```

---

## 4. CODE TEMPLATES

### Template for Unit Test Service

```csharp
using Xunit;
using Moq;
using PoolMateBackend.Services;
using PoolMateBackend.Data;
using PoolMateBackend.Models;
using System;
using System.Threading.Tasks;

namespace PoolMateBackend.Tests.UnitTests.Services
{
    /// <summary>
    /// Unit Tests for [ServiceName]
    /// Method: Solitary Unit Testing with Mocks
    /// </summary>
    public class ServiceNameTests  // Example: TournamentServiceTests
    {
        // ============================================
        // SECTION 1: MOCK OBJECTS DECLARATION
        // ============================================
        private readonly Mock<IRepository> _mockRepo;
        private readonly Mock<IDependencyService> _mockDependency;
        
        // ============================================
        // SECTION 2: SYSTEM UNDER TEST (SUT) DECLARATION
        // ============================================
        private readonly ServiceName _sut;

        // ============================================
        // SECTION 3: CONSTRUCTOR - INITIALIZATION
        // ============================================
        public ServiceNameTests()
        {
            // Initialize Mock objects
            _mockRepo = new Mock<IRepository>();
            _mockDependency = new Mock<IDependencyService>();
            
            // Inject Mocks into the Service (Dependency Injection)
            _sut = new ServiceName(_mockRepo.Object, _mockDependency.Object);
        }

        // ============================================
        // SECTION 4: TEST CASES
        // ============================================
        
        /// <summary>
        /// Test Happy Path - Valid data
        /// </summary>
        [Fact]
        public void MethodName_ValidData_Success()
        {
            // -------- ARRANGE (Prepare) --------
            // 1. Mock input data
            var input = new InputModel 
            { 
                Name = "Test Tournament",
                PlayerCount = 16  // Valid middle value
            };
            var expected = true;
            
            // 2. Setup Mock behavior (If Service calls dependency)
            _mockRepo.Setup(x => x.GetById(It.IsAny<int>()))
                     .Returns(new SomeEntity());
            
            // -------- ACT (Execute) --------
            var actual = _sut.MethodToTest(input);
            
            // -------- ASSERT (Verify) --------
            Assert.Equal(expected, actual);
            
            // Verify Mock was called correct number of times
            _mockRepo.Verify(x => x.GetById(It.IsAny<int>()), Times.Once);
        }
        
        /// <summary>
        /// Test Edge Case - Boundary value
        /// </summary>
        [Fact]
        public void MethodName_BoundaryValue_HandlesCorrectly()
        {
            // ARRANGE
            var input = 1;  // Lower boundary value
            var expected = /* ... */;
            
            // ACT
            var actual = _sut.MethodToTest(input);
            
            // ASSERT
            Assert.Equal(expected, actual);
        }
        
        /// <summary>
        /// Test Invalid Case - Invalid data
        /// </summary>
        [Fact]
        public void MethodName_NullInput_ThrowsArgumentNullException()
        {
            // ARRANGE
            InputModel input = null;
            
            // ACT & ASSERT
            Assert.Throws<ArgumentNullException>(() => _sut.MethodToTest(input));
        }
        
        /// <summary>
        /// Test with [Theory] - Run multiple test cases at once
        /// </summary>
        [Theory]
        [InlineData(-1, false)]  // Invalid
        [InlineData(0, false)]   // Boundary
        [InlineData(1, true)]    // Valid
        [InlineData(32, true)]   // Boundary
        [InlineData(33, false)]  // Invalid
        public void MethodName_MultipleValues_ReturnsCorrectForEachCase(int input, bool expected)
        {
            // ACT
            var actual = _sut.MethodToTest(input);
            
            // ASSERT
            Assert.Equal(expected, actual);
        }
    }
}
```

### Template for Async Methods

```csharp
[Fact]
public async Task MethodNameAsync_ValidData_Success()
{
    // ARRANGE
    var input = /* ... */;
    var expected = /* ... */;
    
    _mockRepo.Setup(x => x.GetByIdAsync(It.IsAny<int>()))
             .ReturnsAsync(new SomeEntity());
    
    // ACT
    var actual = await _sut.MethodToTestAsync(input);
    
    // ASSERT
    Assert.Equal(expected, actual);
    _mockRepo.Verify(x => x.GetByIdAsync(It.IsAny<int>()), Times.Once);
}
```

### Template for Exception Testing

```csharp
[Fact]
public void MethodName_ErrorCondition_ThrowsException()
{
    // ARRANGE
    var input = /* data that causes error */;
    
    _mockRepo.Setup(x => x.GetById(It.IsAny<int>()))
             .Throws<InvalidOperationException>();
    
    // ACT & ASSERT
    var exception = Assert.Throws<InvalidOperationException>(
        () => _sut.MethodToTest(input)
    );
    
    // Check message (Optional)
    Assert.Contains("expected error message", exception.Message);
}
```

---

## 5. PROHIBITED PRACTICES
**Absolutely DO NOT do the following:**

### ❌ FORBIDDEN #1: Complex Logic in Tests
```csharp
// ❌ WRONG
[Fact]
public void Test_WithLoop()
{
    for (int i = 0; i < 10; i++)  // DON'T use loops
    {
        if (i % 2 == 0)  // DON'T use if-else
        {
            // test logic
        }
    }
}

// ✅ CORRECT
[Theory]
[InlineData(0)]
[InlineData(2)]
[InlineData(4)]
public void Test_WithTheory(int input)
{
    // Straight test, no branching logic
    var result = _sut.Process(input);
    Assert.True(result);
}
```

**Reason**: Test methods must run straight through from top to bottom. If they have complex logic, who will test the test?

### ❌ FORBIDDEN #2: Using DateTime.Now Directly

```csharp
// ❌ WRONG - Service
public class TournamentService
{
    public bool IsTournamentActive(Tournament tournament)
    {
        return tournament.EndDate > DateTime.Now;  // DON'T DO THIS!
    }
}

// ✅ CORRECT - Service with Interface
public interface IDateTimeProvider
{
    DateTime Now { get; }
}

public class TournamentService
{
    private readonly IDateTimeProvider _dateTimeProvider;
    
    public TournamentService(IDateTimeProvider dateTimeProvider)
    {
        _dateTimeProvider = dateTimeProvider;
    }
    
    public bool IsTournamentActive(Tournament tournament)
    {
        return tournament.EndDate > _dateTimeProvider.Now;  // OK!
    }
}

// ✅ CORRECT - Test with Mock
[Fact]
public void IsTournamentActive_BeforeEndDate_ReturnsTrue()
{
    // ARRANGE
    var mockDateTime = new Mock<IDateTimeProvider>();
    mockDateTime.Setup(x => x.Now).Returns(new DateTime(2025, 1, 1));
    
    var sut = new TournamentService(mockDateTime.Object);
    var tournament = new Tournament { EndDate = new DateTime(2025, 12, 31) };
    
    // ACT
    var result = sut.IsTournamentActive(tournament);
    
    // ASSERT
    Assert.True(result);
}
```

**Reason**: `DateTime.Now` changes every time it runs → Unstable tests → Different results each run.

### ❌ FORBIDDEN #3: Connecting to Real Database

```csharp
// ❌ WRONG
[Fact]
public void GetUser_FromRealDatabase()
{
    var connectionString = "Server=localhost;Database=PoolMate;...";
    var dbContext = new ApplicationDbContext(connectionString);
    
    var sut = new UserService(dbContext);  // Connected to real DB!
    var user = sut.GetUser(1);
    
    Assert.NotNull(user);
}

// ✅ CORRECT
[Fact]
public void GetUser_WithMockRepo_ReturnsUser()
{
    // ARRANGE
    var mockRepo = new Mock<IUserRepository>();
    mockRepo.Setup(x => x.GetById(1))
            .Returns(new User { Id = 1, Name = "Test" });
    
    var sut = new UserService(mockRepo.Object);  // Mock, no DB!
    
    // ACT
    var user = sut.GetUser(1);
    
    // ASSERT
    Assert.NotNull(user);
    Assert.Equal("Test", user.Name);
}
```

**Reason**: This is **Unit Test**, not **Integration Test**. If connected to DB → Slow, unstable, environment dependent.

### ❌ FORBIDDEN #4: Tests Depending on Each Other

```csharp
// ❌ WRONG
private User _sharedUser;  // Shared variable between tests

[Fact]
public void Test1_CreateUser()
{
    _sharedUser = _sut.CreateUser("John");  // Test A creates User
    Assert.NotNull(_sharedUser);
}

[Fact]
public void Test2_UpdateUser()
{
    _sut.UpdateUser(_sharedUser, "Jane");  // Test B uses User from Test A!
    Assert.Equal("Jane", _sharedUser.Name);
}

// ✅ CORRECT
[Fact]
public void Test1_CreateUser()
{
    var user = _sut.CreateUser("John");
    Assert.NotNull(user);
}

[Fact]
public void Test2_UpdateUser()
{
    // Create User specifically for this test
    var user = new User { Id = 1, Name = "John" };
    _sut.UpdateUser(user, "Jane");
    Assert.Equal("Jane", user.Name);
}
```

**Reason**: Each test must be **completely independent**. If Test A fails → Test B fails too → Hard to debug.

### ❌ FORBIDDEN #5: Asserting Many Unrelated Things

```csharp
// ❌ WRONG - Testing too many things
[Fact]
public void CreateTournament_TestEverything()
{
    var tournament = _sut.CreateTournament("Test");
    
    Assert.NotNull(tournament);
    Assert.Equal("Test", tournament.Name);
    Assert.True(tournament.IsActive);
    Assert.NotNull(tournament.Players);  // Not related to Create
    Assert.Equal(0, tournament.Players.Count);  // Not related
    Assert.NotNull(tournament.Venue);  // Not related
}

// ✅ CORRECT - Split into multiple tests
[Fact]
public void CreateTournament_ReturnsNonNullObject()
{
    var tournament = _sut.CreateTournament("Test");
    Assert.NotNull(tournament);
}

[Fact]
public void CreateTournament_SetsNameCorrectly()
{
    var tournament = _sut.CreateTournament("Test");
    Assert.Equal("Test", tournament.Name);
}

[Fact]
public void CreateTournament_DefaultStatusIsActive()
{
    var tournament = _sut.CreateTournament("Test");
    Assert.True(tournament.IsActive);
}
```

**Reason**: **One Test, One Concept**. Each test should check only one aspect. Easy to read, easy to maintain.

---

## 6. TESTING FOCUS AREAS

### A. Priority Levels (Most Important → Least Important)

#### 🔥 LEVEL 1: MANDATORY (Critical Business Logic)

**Services Layer** - The heart of the application

Focus on:
- ✅ **Complex Business Logic**: Score calculation, ranking, bracket logic
- ✅ **Validation**: Input data verification
- ✅ **Authorization**: Access control checking
- ✅ **Data Transformation**: Converting between Models and DTOs

**Examples in PoolMate Project:**
```
Priority Services to test:
✅ BracketService.cs           (Complex bracket generation logic)
✅ TournamentService.cs        (Core business logic)
✅ MatchService.cs             (Score calculation, winner determination)
✅ PayoutService.cs            (Prize money calculation)
✅ AuthService.cs              (Authentication/Authorization)
✅ FargoRatingService.cs       (Rating calculation)
```

#### 🟡 LEVEL 2: RECOMMENDED (Important)

**Helper Classes & Validators**
```
✅ PlayerDataValidator.cs      (Validation logic)
✅ SlugHelper.cs               (String transformation)
✅ Custom Exceptions           (ValidationException, ConcurrencyConflictException)
```

**Complex DTOs with Logic**
- DTOs with complex mapping methods
- DTOs with validation logic

#### 🟢 LEVEL 3: OPTIONAL (Nice to Have)

**Controllers** - Light testing, only check:
- Is route mapping correct?
- Does it return the correct status code?
- Does it call the correct Service method?

**Models** - Only test if they have:
- Custom validation attributes
- Calculated properties
- Complex relationships

### B. Areas NOT to Test

❌ **DO NOT test:**
- Simple auto-properties (`public string Name { get; set; }`)
- Framework code (Entity Framework, ASP.NET Core)
- External libraries (Cloudinary, Email services)
- Database migrations
- Plain DTOs (just data containers)

### C. Checklist for Each Service Method

When testing one method, ensure you cover these cases:

```
☐ Happy Path (Valid data)
☐ Null Input (Null parameter)
☐ Empty Collection (Empty List/Array)
☐ Boundary Values (Edge limits)
☐ Invalid Input (Invalid data)
☐ Exception Scenarios (Cases that throw exceptions)
☐ Authorization (If permission checking exists)
☐ Special Edge Cases (Depending on business logic)
```

### D. Specific Examples for PoolMate Project

#### Tests for BracketService

```csharp
public class BracketServiceTests
{
    // Test Happy Path
    ☑ GenerateSingleElimination_With8Players_Creates7Matches()
    ☑ GenerateDoubleElimination_With16Players_CreatesCorrectBracket()
    
    // Test Edge Cases
    ☑ GenerateBracket_With1Player_ThrowsException()
    ☑ GenerateBracket_WithOddNumber_FillsByes()
    
    // Test Boundaries
    ☑ GenerateBracket_WithMinPlayers_Works()
    ☑ GenerateBracket_WithMaxPlayers_Works()
    
    // Test Invalid
    ☑ GenerateBracket_WithNullTournament_ThrowsArgumentNullException()
    ☑ GenerateBracket_WithNegativePlayerCount_ThrowsException()
}
```

#### Tests for TournamentService

```csharp
public class TournamentServiceTests
{
    // CRUD Operations
    ☑ CreateTournament_ValidData_ReturnsCreatedTournament()
    ☑ CreateTournament_NullInput_ThrowsArgumentNullException()
    ☑ CreateTournament_DuplicateName_ThrowsValidationException()
    
    // Business Logic
    ☑ StartTournament_EnoughPlayers_ChangesStatusToActive()
    ☑ StartTournament_NotEnoughPlayers_ThrowsException()
    ☑ FinalizeTournament_AllMatchesComplete_CalculatesWinner()
    
    // Authorization
    ☑ UpdateTournament_ByOwner_Succeeds()
    ☑ UpdateTournament_ByNonOwner_ThrowsUnauthorizedException()
}
```

---

## 7. FINAL CHECKLIST

Before submitting code, verify:

### ✅ Structure
- [ ] Test file is in the correct folder (mirror structure)
- [ ] File name has `Tests` suffix (e.g., `TournamentServiceTests.cs`)
- [ ] Namespace follows format: `PoolMateBackend.Tests.UnitTests.[Folder]`

### ✅ Naming
- [ ] Method names follow format: `MethodName_Scenario_ExpectedResult`
- [ ] Mock variables have `mock` or `_mock` prefix
- [ ] System Under Test is named `_sut` or `_service`

### ✅ Code Quality
- [ ] Each test follows AAA pattern
- [ ] No if/else/for logic in tests
- [ ] Each test is independent (no dependency on other tests)
- [ ] All dependencies are mocked
- [ ] Verify Mocks are called correctly (if needed)

### ✅ Coverage
- [ ] Test all 3 types: Valid, Boundary, Invalid
- [ ] Test null input for all Object parameters
- [ ] Test exception scenarios
- [ ] Coverage > 80% for core Services

### ✅ Conventions
- [ ] Use `[Fact]` for single tests
- [ ] Use `[Theory]` + `[InlineData]` for multiple case tests
- [ ] Clear comments for each test (XML comments)
- [ ] No connection to real Database/External API

---

## 8. REFERENCE DOCUMENTATION

### Libraries Used
- **xUnit**: Main testing framework
- **Moq**: Mock framework
- **FluentAssertions** (Optional): More readable assertions

### Test Commands

```powershell
# Run all tests
dotnet test

# Run with coverage
dotnet test --collect:"XPlat Code Coverage"

# Run tests for a specific class
dotnet test --filter FullyQualifiedName~TournamentServiceTests

# Run with detailed output
dotnet test --logger "console;verbosity=detailed"
```

### Comment Template for Tests

```csharp
/// <summary>
/// Tests [Method name] with [Scenario]
/// </summary>
/// <remarks>
/// Input: [Input description]
/// Expected: [Expected result]
/// </remarks>
[Fact]
public void MethodName_Scenario_ExpectedResult()
{
    // Test implementation
}
```

---

## APPENDIX: TERMINOLOGY

| Term | Definition |
|------|------------|
| Unit Test | Test individual class/method in isolation |
| Integration Test | Test multiple components working together |
| White-box Testing | Know internal code, test based on logic |
| Isolation | Separate, no external dependencies |
| Mock | Create fake objects to replace dependencies |
| System Under Test (SUT) | The class/method being tested |
| Boundary Value | Values at the edge limits (min, max) |
| Edge Case | Special, rare situations |

---

**Version:** 1.0  
**Last Updated:** December 6, 2025  
**Created By:** PoolMate Development Team

---

## FINAL NOTE

> 💡 **"Just get the first file right, then replicate the template for the rest!"**

Start with the simplest Service, apply 100% of these rules. Then copy the template for other Services.

**Goals:**
- ✅ Code coverage > 80% for Services
- ✅ All tests pass
- ✅ No warnings
- ✅ Tests run fast (< 1 second per test)

**Remember:** Good tests = Good code = Good product! 🚀

