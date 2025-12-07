# QUY TẮC KIỂM THỬ ỨNG DỤNG POOLMATE BACKEND

## MỤC LỤC
1. [Quy tắc phương pháp](#1-quy-tắc-phương-pháp)
2. [Quy tắc xác định giá trị input](#2-quy-tắc-xác-định-giá-trị-input)
3. [Quy tắc đặt tên](#3-quy-tắc-đặt-tên)
4. [Bộ khung mẫu (Template)](#4-bộ-khung-mẫu-template)
5. [Những điều cấm kỵ](#5-những-điều-cấm-kỵ)
6. [Các phần cần tập trung kiểm thử](#6-các-phần-cần-tập-trung-kiểm-thử)

---

## 1. QUY TẮC PHƯƠNG PHÁP
**Áp dụng cho 100% Unit Test**

### Phương pháp duy nhất
**Solitary Unit Testing (Kiểm thử đơn vị cô lập)** kết hợp **White-box Testing (Kiểm thử hộp trắng)**

### Nguyên tắc cốt lõi

#### ✅ Cô lập hoàn toàn
- Class nào test Class đó
- Mọi thứ bên ngoài (Database, API khác, Service khác) **BẮT BUỘC phải Mock**
- Sử dụng thư viện: **Moq**

#### ✅ Cấu trúc code
**BẮT BUỘC** tuân thủ khuôn mẫu **AAA**:
- **Arrange** (Chuẩn bị): Khởi tạo dữ liệu, mock dependencies
- **Act** (Thực hiện): Gọi method cần test
- **Assert** (Kiểm tra): Xác nhận kết quả

---

## 2. QUY TẮC XÁC ĐỊNH GIÁ TRỊ INPUT
**Không chọn bừa!** Áp dụng công thức **"3 điểm vàng"** dựa trên kỹ thuật **Boundary Value Analysis (Phân tích giá trị biên)**

### Công thức 3 điểm vàng

Với mỗi điều kiện logic (ví dụ: `if (soLuong > 0 && soLuong <= 10)`), bạn phải viết đủ **3 test cases**:

#### 1️⃣ Giá trị Hợp lệ (Happy Path)
- **Mô tả**: Chọn 1 số nằm giữa khoảng hợp lệ
- **Ví dụ**: `5` (với khoảng 1-10)
- **Mong đợi**: `True` / Thành công

#### 2️⃣ Giá trị Biên (Edge Case)
- **Mô tả**: Chọn đúng số ở mép giới hạn
- **Ví dụ**: `1`, `10` (với khoảng 1-10)
- **Mong đợi**: Xử lý đúng theo logic code

#### 3️⃣ Giá trị Lỗi/Ngoại lệ (Invalid Case)
- **Mô tả**: Chọn số nằm ngoài vùng hoặc `null`
- **Ví dụ**: `-1`, `100`, `null`
- **Mong đợi**: `False` hoặc ném Exception

### ⚠️ Quy tắc bắt buộc với Object
Nếu hàm có tham số là Object (`User`, `Order`, `Tournament`...), **BẮT BUỘC** phải test trường hợp tham số đó là `null`.

### Ví dụ minh họa

```csharp
// Hàm cần test
public bool ValidatePlayerCount(int count)
{
    return count > 0 && count <= 32;
}

// 3 Test cases bắt buộc:
[Fact]
public void ValidatePlayerCount_HopLe_TraVeTrue()
{
    // Giá trị ở giữa: 16
    var result = _sut.ValidatePlayerCount(16);
    Assert.True(result);
}

[Fact]
public void ValidatePlayerCount_GiaTriBien_TraVeTrue()
{
    // Giá trị biên: 1 và 32
    Assert.True(_sut.ValidatePlayerCount(1));
    Assert.True(_sut.ValidatePlayerCount(32));
}

[Fact]
public void ValidatePlayerCount_GiaTriNgoaiKhoang_TraVeFalse()
{
    // Giá trị ngoài khoảng: 0, -1, 33
    Assert.False(_sut.ValidatePlayerCount(0));
    Assert.False(_sut.ValidatePlayerCount(-1));
    Assert.False(_sut.ValidatePlayerCount(33));
}
```

---

## 3. QUY TẮC ĐẶT TÊN

### A. Tên Project & Folder

#### Project Test
```
PoolMateBackend.Tests
```

#### Cấu trúc Folder
**BẮT BUỘC** giống hệt cấu trúc project chính:

```
Project chính:
PoolMateBackend/
  └── Services/
      └── TournamentService.cs

Project test:
PoolMateBackend.Tests/
  └── UnitTests/
      └── Services/
          └── TournamentServiceTests.cs
```

### B. Tên Hàm Test (Method Name)

#### Công thức
```
TênHàm_TìnhHuống_KếtQuảMongĐợi
```

**Giải thích:**
- **TênHàm**: Tên của method trong Service đang test
- **TìnhHuống**: Input đầu vào là gì?
- **KếtQuảMongĐợi**: Hàm sẽ trả về gì?

#### Ví dụ thực tế

```csharp
// ✅ ĐÚNG
Login_EmailKhongTonTai_TraVeFalse()
CreateTournament_TenRong_NemValidationException()
CalculateScore_DuLieuHopLe_TraVeDiemChinhXac()
GetTournamentById_IdKhongHopLe_TraVeNull()
UpdateMatch_MatchKhongTonTai_TraVeFalse()

// ❌ SAI
TestLogin()
Test1()
LoginTest()
CheckEmail()
```

### C. Tên Biến trong Code Test

| Loại biến | Tên gọi | Ví dụ |
|-----------|---------|-------|
| Đối tượng cần test | `_sut` hoặc `_service` | `private readonly TournamentService _sut;` |
| Đối tượng giả (Mock) | Tiền tố `mock` | `mockRepo`, `mockEmailService` |
| Dữ liệu mong đợi | `expected` | `var expected = 100;` |
| Dữ liệu thực tế | `actual` hoặc `result` | `var actual = _sut.Calculate();` |

#### Ví dụ đầy đủ

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

## 4. BỘ KHUNG MẪU (TEMPLATE)

### Template cho Unit Test Service

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
    /// Unit Tests cho [TênService]
    /// Phương pháp: Solitary Unit Testing với Mock
    /// </summary>
    public class TenServiceTests  // Ví dụ: TournamentServiceTests
    {
        // ============================================
        // PHẦN 1: KHAI BÁO MOCK OBJECTS
        // ============================================
        private readonly Mock<IRepository> _mockRepo;
        private readonly Mock<IDependencyService> _mockDependency;
        
        // ============================================
        // PHẦN 2: KHAI BÁO SYSTEM UNDER TEST (SUT)
        // ============================================
        private readonly TenService _sut;

        // ============================================
        // PHẦN 3: CONSTRUCTOR - KHỞI TẠO
        // ============================================
        public TenServiceTests()
        {
            // Khởi tạo Mock objects
            _mockRepo = new Mock<IRepository>();
            _mockDependency = new Mock<IDependencyService>();
            
            // Bơm Mock vào Service chính (Dependency Injection)
            _sut = new TenService(_mockRepo.Object, _mockDependency.Object);
        }

        // ============================================
        // PHẦN 4: TEST CASES
        // ============================================
        
        /// <summary>
        /// Test Happy Path - Dữ liệu hợp lệ
        /// </summary>
        [Fact]
        public void TenHam_DuLieuHopLe_ThanhCong()
        {
            // -------- ARRANGE (Chuẩn bị) --------
            // 1. Giả lập dữ liệu input
            var input = new InputModel 
            { 
                Name = "Test Tournament",
                PlayerCount = 16  // Giá trị hợp lệ ở giữa
            };
            var expected = true;
            
            // 2. Setup Mock behavior (Nếu Service gọi đến dependency)
            _mockRepo.Setup(x => x.GetById(It.IsAny<int>()))
                     .Returns(new SomeEntity());
            
            // -------- ACT (Thực hiện) --------
            var actual = _sut.TenHamCanTest(input);
            
            // -------- ASSERT (Kiểm tra) --------
            Assert.Equal(expected, actual);
            
            // Verify Mock được gọi đúng số lần
            _mockRepo.Verify(x => x.GetById(It.IsAny<int>()), Times.Once);
        }
        
        /// <summary>
        /// Test Edge Case - Giá trị biên
        /// </summary>
        [Fact]
        public void TenHam_GiaTriBien_XuLyDung()
        {
            // ARRANGE
            var input = 1;  // Giá trị biên dưới
            var expected = /* ... */;
            
            // ACT
            var actual = _sut.TenHamCanTest(input);
            
            // ASSERT
            Assert.Equal(expected, actual);
        }
        
        /// <summary>
        /// Test Invalid Case - Dữ liệu không hợp lệ
        /// </summary>
        [Fact]
        public void TenHam_InputNull_NemArgumentNullException()
        {
            // ARRANGE
            InputModel input = null;
            
            // ACT & ASSERT
            Assert.Throws<ArgumentNullException>(() => _sut.TenHamCanTest(input));
        }
        
        /// <summary>
        /// Test với [Theory] - Chạy nhiều test case cùng lúc
        /// </summary>
        [Theory]
        [InlineData(-1, false)]  // Invalid
        [InlineData(0, false)]   // Boundary
        [InlineData(1, true)]    // Valid
        [InlineData(32, true)]   // Boundary
        [InlineData(33, false)]  // Invalid
        public void TenHam_NhieuGiaTri_KetQuaDungVoiTungCase(int input, bool expected)
        {
            // ACT
            var actual = _sut.TenHamCanTest(input);
            
            // ASSERT
            Assert.Equal(expected, actual);
        }
    }
}
```

### Template cho Async Method

```csharp
[Fact]
public async Task TenHamAsync_DuLieuHopLe_ThanhCong()
{
    // ARRANGE
    var input = /* ... */;
    var expected = /* ... */;
    
    _mockRepo.Setup(x => x.GetByIdAsync(It.IsAny<int>()))
             .ReturnsAsync(new SomeEntity());
    
    // ACT
    var actual = await _sut.TenHamCanTestAsync(input);
    
    // ASSERT
    Assert.Equal(expected, actual);
    _mockRepo.Verify(x => x.GetByIdAsync(It.IsAny<int>()), Times.Once);
}
```

### Template cho Exception Testing

```csharp
[Fact]
public void TenHam_DieuKienLoi_NemException()
{
    // ARRANGE
    var input = /* dữ liệu gây lỗi */;
    
    _mockRepo.Setup(x => x.GetById(It.IsAny<int>()))
             .Throws<InvalidOperationException>();
    
    // ACT & ASSERT
    var exception = Assert.Throws<InvalidOperationException>(
        () => _sut.TenHamCanTest(input)
    );
    
    // Kiểm tra message (Optional)
    Assert.Contains("expected error message", exception.Message);
}
```

---

## 5. NHỮNG ĐIỀU CẤM KỴ
**Tuyệt đối KHÔNG làm những điều sau:**

### ❌ CẤM #1: Logic phức tạp trong Test
```csharp
// ❌ SAI
[Fact]
public void Test_WithLoop()
{
    for (int i = 0; i < 10; i++)  // KHÔNG ĐƯỢC dùng vòng lặp
    {
        if (i % 2 == 0)  // KHÔNG ĐƯỢC dùng if-else
        {
            // test logic
        }
    }
}

// ✅ ĐÚNG
[Theory]
[InlineData(0)]
[InlineData(2)]
[InlineData(4)]
public void Test_WithTheory(int input)
{
    // Test thẳng, không có logic rẽ nhánh
    var result = _sut.Process(input);
    Assert.True(result);
}
```

**Lý do**: Hàm Test phải chạy thẳng tuột từ trên xuống dưới. Nếu có logic phức tạp, ai sẽ test cái Test?

### ❌ CẤM #2: Sử dụng DateTime.Now trực tiếp

```csharp
// ❌ SAI - Service
public class TournamentService
{
    public bool IsTournamentActive(Tournament tournament)
    {
        return tournament.EndDate > DateTime.Now;  // KHÔNG ĐƯỢC!
    }
}

// ✅ ĐÚNG - Service với Interface
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

// ✅ ĐÚNG - Test với Mock
[Fact]
public void IsTournamentActive_TruocEndDate_TraVeTrue()
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

**Lý do**: `DateTime.Now` thay đổi mỗi lần chạy → Test không ổn định → Kết quả khác nhau mỗi lần chạy.

### ❌ CẤM #3: Kết nối Database thật

```csharp
// ❌ SAI
[Fact]
public void GetUser_FromRealDatabase()
{
    var connectionString = "Server=localhost;Database=PoolMate;...";
    var dbContext = new ApplicationDbContext(connectionString);
    
    var sut = new UserService(dbContext);  // Kết nối DB thật!
    var user = sut.GetUser(1);
    
    Assert.NotNull(user);
}

// ✅ ĐÚNG
[Fact]
public void GetUser_WithMockRepo_ReturnsUser()
{
    // ARRANGE
    var mockRepo = new Mock<IUserRepository>();
    mockRepo.Setup(x => x.GetById(1))
            .Returns(new User { Id = 1, Name = "Test" });
    
    var sut = new UserService(mockRepo.Object);  // Mock, không có DB!
    
    // ACT
    var user = sut.GetUser(1);
    
    // ASSERT
    Assert.NotNull(user);
    Assert.Equal("Test", user.Name);
}
```

**Lý do**: Đây là **Unit Test**, không phải **Integration Test**. Nếu kết nối DB → Chậm, không ổn định, phụ thuộc môi trường.

### ❌ CẤM #4: Test phụ thuộc lẫn nhau

```csharp
// ❌ SAI
private User _sharedUser;  // Biến chia sẻ giữa các test

[Fact]
public void Test1_CreateUser()
{
    _sharedUser = _sut.CreateUser("John");  // Test A tạo User
    Assert.NotNull(_sharedUser);
}

[Fact]
public void Test2_UpdateUser()
{
    _sut.UpdateUser(_sharedUser, "Jane");  // Test B dùng User từ Test A!
    Assert.Equal("Jane", _sharedUser.Name);
}

// ✅ ĐÚNG
[Fact]
public void Test1_CreateUser()
{
    var user = _sut.CreateUser("John");
    Assert.NotNull(user);
}

[Fact]
public void Test2_UpdateUser()
{
    // Tự tạo User riêng cho Test này
    var user = new User { Id = 1, Name = "John" };
    _sut.UpdateUser(user, "Jane");
    Assert.Equal("Jane", user.Name);
}
```

**Lý do**: Mỗi Test phải **hoàn toàn độc lập**. Nếu Test A fail → Test B cũng fail → Khó debug.

### ❌ CẤM #5: Assert nhiều thứ không liên quan

```csharp
// ❌ SAI - Test quá nhiều thứ
[Fact]
public void CreateTournament_TestEverything()
{
    var tournament = _sut.CreateTournament("Test");
    
    Assert.NotNull(tournament);
    Assert.Equal("Test", tournament.Name);
    Assert.True(tournament.IsActive);
    Assert.NotNull(tournament.Players);  // Không liên quan đến Create
    Assert.Equal(0, tournament.Players.Count);  // Không liên quan
    Assert.NotNull(tournament.Venue);  // Không liên quan
}

// ✅ ĐÚNG - Tách thành nhiều test
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

**Lý do**: **One Test, One Concept**. Mỗi test chỉ kiểm tra 1 khía cạnh. Dễ đọc, dễ maintain.

---

## 6. CÁC PHẦN CẦN TẬP TRUNG KIỂM THỬ

### A. Thứ tự ưu tiên (Quan trọng nhất → Ít quan trọng nhất)

#### 🔥 MỨC 1: BẮT BUỘC (Critical Business Logic)

**Services Layer** - Đây là trái tim của ứng dụng

Tập trung vào:
- ✅ **Business Logic phức tạp**: Tính toán điểm số, xếp hạng, bracket logic
- ✅ **Validation**: Kiểm tra dữ liệu đầu vào
- ✅ **Authorization**: Kiểm tra quyền truy cập
- ✅ **Data Transformation**: Chuyển đổi giữa Models và DTOs

**Ví dụ trong dự án PoolMate:**
```
Priority Services cần test:
✅ BracketService.cs           (Logic tạo bracket phức tạp)
✅ TournamentService.cs        (Business logic chính)
✅ MatchService.cs             (Tính điểm, xác định winner)
✅ PayoutService.cs            (Tính toán tiền thưởng)
✅ AuthService.cs              (Authentication/Authorization)
✅ FargoRatingService.cs       (Tính toán rating)
```

#### 🟡 MỨC 2: NÊN CÓ (Important)

**Helper Classes & Validators**
```
✅ PlayerDataValidator.cs      (Validation logic)
✅ SlugHelper.cs               (String transformation)
✅ Custom Exceptions           (ValidationException, ConcurrencyConflictException)
```

**Complex DTOs with Logic**
- DTOs có phương thức mapping phức tạp
- DTOs có validation logic

#### 🟢 MỨC 3: TÙY CHỌN (Nice to Have)

**Controllers** - Test nhẹ, chỉ kiểm tra:
- Route mapping đúng không?
- Return đúng status code không?
- Call đúng Service method không?

**Models** - Chỉ test nếu có:
- Custom validation attributes
- Calculated properties
- Complex relationships

### B. Các phần KHÔNG CẦN test

❌ **KHÔNG test:**
- Auto-properties đơn giản (`public string Name { get; set; }`)
- Framework code (Entity Framework, ASP.NET Core)
- External libraries (Cloudinary, Email services)
- Database migrations
- DTOs thuần túy (chỉ là data containers)

### C. Checklist cho mỗi Service Method

Khi test 1 method, hãy đảm bảo cover đủ các trường hợp sau:

```
☐ Happy Path (Dữ liệu hợp lệ)
☐ Null Input (Tham số null)
☐ Empty Collection (List/Array rỗng)
☐ Boundary Values (Giá trị biên)
☐ Invalid Input (Dữ liệu không hợp lệ)
☐ Exception Scenarios (Các trường hợp ném exception)
☐ Authorization (Nếu có kiểm tra quyền)
☐ Edge Cases đặc biệt (Tùy logic nghiệp vụ)
```

### D. Ví dụ cụ thể cho dự án PoolMate

#### Test cho BracketService

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

#### Test cho TournamentService

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

## 7. CHECKLIST CUỐI CÙNG

Trước khi submit code, hãy kiểm tra:

### ✅ Cấu trúc
- [ ] File test nằm đúng folder (mirror structure)
- [ ] Tên file có suffix `Tests` (VD: `TournamentServiceTests.cs`)
- [ ] Namespace đúng format: `PoolMateBackend.Tests.UnitTests.[Folder]`

### ✅ Đặt tên
- [ ] Tên method theo format: `MethodName_Scenario_ExpectedResult`
- [ ] Biến Mock có prefix `mock` hoặc `_mock`
- [ ] System Under Test đặt tên `_sut` hoặc `_service`

### ✅ Code chất lượng
- [ ] Mỗi test tuân thủ AAA pattern
- [ ] Không có logic if/else/for trong test
- [ ] Mỗi test độc lập (không phụ thuộc test khác)
- [ ] Mock đầy đủ dependencies
- [ ] Verify các Mock được gọi đúng (nếu cần)

### ✅ Coverage
- [ ] Test đủ 3 loại: Valid, Boundary, Invalid
- [ ] Test null input cho tất cả tham số Object
- [ ] Test exception scenarios
- [ ] Coverage > 80% cho Services chính

### ✅ Conventions
- [ ] Sử dụng `[Fact]` cho test đơn
- [ ] Sử dụng `[Theory]` + `[InlineData]` cho test nhiều case
- [ ] Comment rõ ràng cho mỗi test (XML comment)
- [ ] Không kết nối Database/External API thật

---

## 8. TÀI LIỆU THAM KHẢO

### Thư viện sử dụng
- **xUnit**: Framework test chính
- **Moq**: Mock framework
- **FluentAssertions** (Optional): Assert dễ đọc hơn

### Câu lệnh chạy test

```powershell
# Chạy tất cả tests
dotnet test

# Chạy với coverage
dotnet test --collect:"XPlat Code Coverage"

# Chạy test của 1 class cụ thể
dotnet test --filter FullyQualifiedName~TournamentServiceTests

# Chạy với output chi tiết
dotnet test --logger "console;verbosity=detailed"
```

### Mẫu comment cho test

```csharp
/// <summary>
/// Kiểm tra [Tên method] với [Tình huống]
/// </summary>
/// <remarks>
/// Input: [Mô tả input]
/// Expected: [Kết quả mong đợi]
/// </remarks>
[Fact]
public void MethodName_Scenario_ExpectedResult()
{
    // Test implementation
}
```

---

## PHỤ LỤC: THUẬT NGỮ

| Tiếng Việt | Tiếng Anh | Giải thích |
|------------|-----------|------------|
| Kiểm thử đơn vị | Unit Test | Test từng class/method riêng lẻ |
| Kiểm thử tích hợp | Integration Test | Test nhiều components kết hợp |
| Kiểm thử hộp trắng | White-box Testing | Biết code bên trong, test dựa trên logic |
| Cô lập | Isolation | Tách biệt, không phụ thuộc bên ngoài |
| Giả lập | Mock | Tạo đối tượng giả thay thế dependency |
| Đối tượng cần test | System Under Test (SUT) | Class/Method đang được kiểm thử |
| Giá trị biên | Boundary Value | Giá trị ở mép giới hạn (min, max) |
| Trường hợp ngoại lệ | Edge Case | Tình huống đặc biệt, hiếm gặp |

---

**Phiên bản:** 1.0  
**Ngày cập nhật:** 2025-12-06  
**Người tạo:** PoolMate Development Team

---

## LƯU Ý CUỐI CÙNG

> 💡 **"Chỉ cần làm chuẩn 1 file đầu tiên, các file sau cứ thế nhân bản lên!"**

Hãy bắt đầu với 1 Service đơn giản nhất, áp dụng đúng 100% quy tắc này. Sau đó copy template cho các Service khác.

**Mục tiêu:**
- ✅ Code coverage > 80% cho Services
- ✅ Mọi test đều pass
- ✅ Không có warning
- ✅ Test chạy nhanh (< 1 giây mỗi test)

**Hãy nhớ:** Test tốt = Code tốt = Sản phẩm tốt! 🚀

