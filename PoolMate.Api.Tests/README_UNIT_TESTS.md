# ?? PoolMate.Api.Tests - Unit Test Setup Guide

## ?? T?ng Quan

D? án test này ???c thi?t l?p ?? test các hàm logic ph?c t?p nh?t trong PoolMate Backend:

### ?? 5-7 Hàm Chính ???c Test:

1. **BracketService.CreateAsync()** ?
   - Logic t?o bracket (Single/Double Elimination)
   - Multi-stage tournament handling
   - Validation ph?c t?p

2. **AdminPlayerService.MergePlayersAsync()** ?
   - Merge nhi?u player records
   - Maintain tournament history
   - Duplicate detection

3. **PayoutService.SimulatePayoutAsync()** ?
   - Tính toán payout theo percentage
   - Handle rounding precision
   - Multiple templates

4. **BracketService.UpdateMatchAsync()** (Pending)
   - Update match scores
   - Propagate results through bracket
   - Handle concurrent updates

5. **AdminUserService.BulkDeactivateAsync()** (Pending)
   - Bulk user management
   - Audit logging
   - Permission checking

6. **TournamentService.CompleteStageAsync()** (Pending)
   - Stage completion logic
   - Auto-advance to next stage
   - Validation chains

7. **OrganizerDashboardService.GetDashboardStatsAsync()** (Pending)
   - Complex data aggregation
   - Performance optimization
   - Real-time calculations

---

## ?? Quick Start

### 1. Build Project

```bash
cd D:\PoolmateBE\poolmate_be
dotnet build
```

### 2. Run All Tests

```bash
dotnet test PoolMate.Api.Tests
```

### 3. Run Specific Test Class

```bash
dotnet test PoolMate.Api.Tests --filter "ClassName=BracketServiceCreateAsyncTests"
```

### 4. Run with Verbose Output

```bash
dotnet test PoolMate.Api.Tests -v d
```

---

## ?? Project Structure

```
PoolMate.Api.Tests/
??? Fixtures/
?   ??? TestDatabaseFixture.cs          # In-memory DB setup
?   ??? MockDataFactory.cs              # Mock data generators
??? Services/
?   ??? BracketServiceCreateAsyncTests.cs
?   ??? AdminPlayerServiceTests.cs
?   ??? PayoutServiceTests.cs
?   ??? BracketServiceUpdateMatchTests.cs (Pending)
?   ??? AdminUserServiceTests.cs (Pending)
?   ??? TournamentServiceTests.cs (Pending)
??? Controllers/                         # (Optional) Controller tests
??? PoolMate.Api.Tests.csproj
```

---

## ??? Technologies Used

- **xUnit** v2.6+
  - Modern testing framework cho .NET
  - Parallel test execution
  - Clean test discovery

- **Moq** v4.20+
  - Mocking framework
  - Setup mock behaviors
  - Verify interactions

- **Entity Framework Core InMemory**
  - In-memory database for testing
  - No SQL Server dependency
  - Fast test execution

- **.NET 8**
  - Latest .NET framework
  - C# 12 features
  - Async/await support

---

## ?? Test Patterns Used

### 1. **Arrange-Act-Assert (AAA)**

```csharp
[Fact]
public async Task TestName()
{
    // Arrange - Setup test data
    var data = MockDataFactory.CreateMockTournament();
    _fixture.Context.Tournaments.Add(data);
    await _fixture.Context.SaveChangesAsync();

    // Act - Execute the method
    var result = await service.MethodAsync(data.Id, CancellationToken.None);

    // Assert - Verify results
    Assert.NotNull(result);
    Assert.Equal(expected, result.Value);
}
```

### 2. **Positive & Negative Cases**

```csharp
// ? Success case
[Fact]
public async Task Method_WithValidInput_ShouldReturnSuccess() { }

// ? Error case
[Fact]
public async Task Method_WithInvalidInput_ShouldThrowException() { }
```

### 3. **Edge Cases & Boundaries**

```csharp
[Fact]
public async Task Method_WithZeroValue_ShouldHandleCorrectly() { }

[Fact]
public async Task Method_WithLargeValue_ShouldMaintainPrecision() { }
```

---

## ?? Test Coverage by Service

### BracketService.CreateAsync()
- ? Single Elimination bracket creation
- ? Double Elimination bracket creation
- ? Multi-stage tournament validation
- ? Error cases (insufficient players, existing bracket)
- ? Bracket size estimation

**Total Tests:** 8
**Success Rate:** 100% (expected)

### AdminPlayerService
- ? Duplicate detection (email, name similarity)
- ? Player merge operations
- ? Tournament history preservation
- ? Statistics calculation
- ? Data quality assessment

**Total Tests:** 11
**Success Rate:** 100% (expected)

### PayoutService
- ? Payout calculation with percentages
- ? Correct distribution
- ? Rounding precision
- ? Large amount handling
- ? Zero/negative validation

**Total Tests:** 9
**Success Rate:** 100% (expected)

---

## ?? Adding New Tests

### Step 1: Create Test Class

```csharp
public class ServiceNameTests : IDisposable
{
    private readonly TestDatabaseFixture _fixture;
    private readonly Mock<IDependency> _mockDependency;
    private readonly ServiceUnderTest _service;

    public ServiceNameTests()
    {
        _fixture = new TestDatabaseFixture();
        _mockDependency = new Mock<IDependency>();
        _service = new ServiceUnderTest(_fixture.Context, _mockDependency.Object);
    }

    // Test methods here...

    public void Dispose()
    {
        _fixture?.Dispose();
    }
}
```

### Step 2: Write Test Method

```csharp
[Fact]
public async Task MethodName_Scenario_ExpectedResult()
{
    // Arrange
    var input = MockDataFactory.CreateMockData();

    // Act
    var result = await _service.MethodAsync(input, CancellationToken.None);

    // Assert
    Assert.NotNull(result);
    Assert.Equal(expected, result.Value);
}
```

### Step 3: Use Mock Factory

```csharp
// Create mock data easily
var tournament = MockDataFactory.CreateMockTournament();
var player = MockDataFactory.CreateMockPlayer("John Doe");
var match = MockDataFactory.CreateMockMatch(tournamentId, player1Id, player2Id);
var payout = MockDataFactory.CreateMockPayoutTemplate("Standard", 8);
```

---

## ?? Running Tests with Coverage

### Using OpenCover (if installed)

```bash
dotnet add package OpenCover
dotnet add package ReportGenerator

dotnet test PoolMate.Api.Tests --collect:"XPlat Code Coverage"
```

### View Coverage Report

```bash
# After running tests with coverage
reportgenerator -reports:"**/coverage.cobertura.xml" -targetdir:"coveragereport" -reporttypes:Html
```

---

## ?? Troubleshooting

### Issue: Tests fail with "Connection refused"

**Solution:** Make sure you're using InMemory database
```csharp
// ? Correct
var options = new DbContextOptionsBuilder<ApplicationDbContext>()
    .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
    .Options;

// ? Wrong - Don't use SQL Server in tests
.UseSqlServer("connection-string")
```

### Issue: "DbUpdateConcurrencyException" in tests

**Solution:** Clear mock data between tests or use unique DB instance per test

```csharp
public TestDatabaseFixture()
{
    var options = new DbContextOptionsBuilder<ApplicationDbContext>()
        .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString()) // Unique per test
        .Options;
    Context = new ApplicationDbContext(options);
}
```

### Issue: Mock not working

**Solution:** Verify mock setup before assertion

```csharp
_mockService.Setup(m => m.MethodAsync(It.IsAny<int>()))
    .ReturnsAsync(expectedValue);

// Later in test...
var result = await _mockService.Object.MethodAsync(1);
```

---

## ?? Best Practices

### ? DO:

1. **Use meaningful test names**
   ```csharp
   // ? Good
   public async Task CreateBracket_WithValidPlayers_ShouldCreateMatches()
   
   // ? Bad
   public async Task Test1()
   ```

2. **Keep tests isolated**
   ```csharp
   // Each test should be independent
   // Don't rely on test execution order
   ```

3. **Use Arrange-Act-Assert**
   ```csharp
   // Clear separation of concerns
   ```

4. **Test one thing per test**
   ```csharp
   // One assertion per test (ideally)
   // Multiple related assertions OK if grouped logically
   ```

5. **Use descriptive variable names**
   ```csharp
   var expectedPlayerCount = 8;
   var actualPlayerCount = result.Players.Count;
   ```

### ? DON'T:

1. Don't use real services in unit tests
2. Don't test internal implementation details
3. Don't ignore test failures
4. Don't create interdependent tests
5. Don't use `Thread.Sleep()` - use async patterns

---

## ?? CI/CD Integration

### Add to Azure DevOps Pipeline

```yaml
- task: DotNetCoreCLI@2
  displayName: 'Run Unit Tests'
  inputs:
    command: 'test'
    projects: '**/PoolMate.Api.Tests.csproj'
    arguments: '--logger trx --collect:"XPlat Code Coverage"'
```

### Add to GitHub Actions

```yaml
- name: Run Tests
  run: dotnet test PoolMate.Api.Tests --verbosity normal
```

---

## ?? References

- [xUnit Documentation](https://xunit.net/)
- [Moq Documentation](https://github.com/moq/moq4)
- [Entity Framework Testing](https://docs.microsoft.com/en-us/ef/core/testing/)
- [Unit Testing Best Practices](https://docs.microsoft.com/en-us/dotnet/core/testing/unit-testing-best-practices)

---

## ?? Next Steps

1. ? Complete remaining 4 test classes
2. ? Add integration tests for API endpoints
3. ? Add performance benchmarks
4. ? Setup code coverage thresholds (>80%)
5. ? Add CI/CD pipeline integration

---

## ?? Support

For questions or issues:
- Check existing tests for examples
- Review MockDataFactory for available mock objects
- Refer to service interfaces for expected behavior

**Happy Testing! ??**
