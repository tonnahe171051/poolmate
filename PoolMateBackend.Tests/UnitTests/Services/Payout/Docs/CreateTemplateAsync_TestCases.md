# Test Cases cho `CreateTemplateAsync` - PayoutService

## Thông tin hàm

**File:** `PoolMateBackend/Services/PayoutService.cs`

**Signature:**
```csharp
public async Task<PayoutTemplateDto> CreateTemplateAsync(
    string userId, 
    CreatePayoutTemplateDto dto,
    CancellationToken ct = default)
```

---

## Phân tích Control Flow

Hàm `CreateTemplateAsync` có các nhánh điều kiện sau:

```
┌─────────────────────────────────────────────────────────────┐
│                    CreateTemplateAsync                       │
└─────────────────────────────────────────────────────────────┘
                              │
                              ▼
              ┌───────────────────────────────┐
              │ dto.MinPlayers > dto.MaxPlayers? │
              │         (Rule A)               │
              └───────────────────────────────┘
                    │YES              │NO
                    ▼                 ▼
              ┌──────────┐   ┌─────────────────────────────┐
              │ THROW    │   │ Calculate totalPercent =     │
              │ "Min >   │   │ dto.Distribution.Sum(%)      │
              │  Max"    │   └─────────────────────────────┘
              └──────────┘                 │
                                           ▼
                          ┌─────────────────────────────────┐
                          │ Math.Abs(totalPercent-100) > 0.01│
                          │           (Rule B)              │
                          └─────────────────────────────────┘
                                │YES              │NO
                                ▼                 ▼
                          ┌──────────┐   ┌────────────────────┐
                          │ THROW    │   │ Create Entity      │
                          │ "Total % │   │ Add to DbContext   │
                          │ must be  │   │ SaveChangesAsync   │
                          │ 100"     │   │ Return DTO         │
                          └──────────┘   └────────────────────┘
```

---

## Boundary Analysis

### 1. MinPlayers vs MaxPlayers (so sánh `>`)

| Vị trí | MinPlayers | MaxPlayers | Điều kiện | Kết quả |
|:-------|:-----------|:-----------|:----------|:--------|
| Trên biên | 6 | 5 | 6 > 5 = true | ❌ Throw InvalidOperationException |
| Ngay biên | 5 | 5 | 5 > 5 = false | ✅ Hợp lệ (Min = Max cho phép) |
| Dưới biên | 4 | 5 | 4 > 5 = false | ✅ Hợp lệ |

### 2. TotalPercent (so sánh `Math.Abs(totalPercent - 100) > 0.01`)

| Vị trí | TotalPercent | Sai số (Abs) | Điều kiện | Kết quả |
|:-------|:-------------|:-------------|:----------|:--------|
| Dưới biên xa | 99.98 | 0.02 | 0.02 > 0.01 = true | ❌ Throw |
| Ngay biên dưới | 99.99 | 0.01 | 0.01 > 0.01 = false | ✅ Hợp lệ |
| Chính xác | 100.00 | 0.00 | 0.00 > 0.01 = false | ✅ Hợp lệ |
| Ngay biên trên | 100.01 | 0.01 | 0.01 > 0.01 = false | ✅ Hợp lệ |
| Trên biên xa | 100.02 | 0.02 | 0.02 > 0.01 = true | ❌ Throw |

---

## Bảng Test Cases

| ID | Loại | Tên Test Case (Gợi ý đặt tên) | Input Giả định (Mock Data) | Kết quả Mong đợi | Ghi chú |
|:---|:-----|:------------------------------|:---------------------------|:-----------------|:--------|
| 1 | Error | CreateTemplateAsync_WhenMinPlayersGreaterThanMaxPlayers_ThrowsInvalidOperationException | `dto.MinPlayers = 10`, `dto.MaxPlayers = 5` | Throw `InvalidOperationException("Min Players (10) cannot be greater than Max Players (5).")` | [Fact] |
| 2 | Boundary | CreateTemplateAsync_WhenMinPlayersEqualsMaxPlayers_ReturnsTemplate | `dto.MinPlayers = 5`, `dto.MaxPlayers = 5`, valid distribution | Return `PayoutTemplateDto` thành công | [Fact] - Ngay biên |
| 3 | Boundary | CreateTemplateAsync_WhenMinPlayersLessThanMaxPlayers_ReturnsTemplate | `dto.MinPlayers = 4`, `dto.MaxPlayers = 8`, valid distribution | Return `PayoutTemplateDto` thành công | [Fact] - Dưới biên |
| 4 | Boundary | CreateTemplateAsync_WhenTotalPercentIs99_98_ThrowsInvalidOperationException | Distribution sum = 99.98 (sai số 0.02) | Throw `InvalidOperationException("Total percentage must be exactly 100%...")` | [Fact] - Dưới biên |
| 5 | Boundary | CreateTemplateAsync_WhenTotalPercentIs99_99_ReturnsTemplate | Distribution sum = 99.99 (sai số 0.01) | Return `PayoutTemplateDto` thành công | [Fact] - Ngay biên (valid) |
| 6 | Boundary | CreateTemplateAsync_WhenTotalPercentIs100_01_ReturnsTemplate | Distribution sum = 100.01 (sai số 0.01) | Return `PayoutTemplateDto` thành công | [Fact] - Ngay biên (valid) |
| 7 | Boundary | CreateTemplateAsync_WhenTotalPercentIs100_02_ThrowsInvalidOperationException | Distribution sum = 100.02 (sai số 0.02) | Throw `InvalidOperationException` | [Fact] - Trên biên |
| 8 | Happy | CreateTemplateAsync_WhenAllValid_ReturnsCorrectDto | Valid dto với `Name = "Test Template"`, `MinPlayers = 4`, `MaxPlayers = 8`, `Distribution` sum = 100 | Return DTO với đầy đủ properties: `Id`, `Name`, `MinPlayers`, `MaxPlayers`, `Places`, `Distribution` | [Fact] |
| 9 | Happy | CreateTemplateAsync_WhenValid_SavesEntityToDatabase | Valid dto | Verify `_db.PayoutTemplates.Add()` được gọi 1 lần, `SaveChangesAsync()` được gọi 1 lần | [Fact] |
| 10 | Edge | CreateTemplateAsync_VerifyNameIsTrimmed | `dto.Name = "  Template Name  "` (có spaces đầu/cuối) | Entity.Name = `"Template Name"` (đã trim) | [Fact] |
| 11 | Edge | CreateTemplateAsync_VerifyOwnerUserIdIsSet | `userId = "user-123"`, valid dto | Entity.OwnerUserId = `"user-123"` | [Fact] |
| 12 | Edge | CreateTemplateAsync_VerifyPlacesEqualsDistributionCount | Distribution có 5 items | Entity.Places = 5, DTO.Places = 5 | [Fact] |
| 13 | Edge | CreateTemplateAsync_VerifyPercentJsonIsSerialized | Distribution = `[{Rank:1, Percent:60}, {Rank:2, Percent:40}]` | Entity.PercentJson = valid JSON string đại diện cho distribution | [Fact] |

---

## Chi tiết Mock Setup cho từng Test Case

### Test Case #1: MinPlayers > MaxPlayers
```csharp
[Fact]
public async Task CreateTemplateAsync_WhenMinPlayersGreaterThanMaxPlayers_ThrowsInvalidOperationException()
{
    // Arrange
    var userId = "user-123";
    var dto = new CreatePayoutTemplateDto
    {
        Name = "Test Template",
        MinPlayers = 10,
        MaxPlayers = 5,
        Distribution = new List<RankPercentDto>
        {
            new() { Rank = 1, Percent = 100 }
        }
    };

    // Act & Assert
    var ex = await Assert.ThrowsAsync<InvalidOperationException>(
        () => _payoutService.CreateTemplateAsync(userId, dto));
    Assert.Equal("Min Players (10) cannot be greater than Max Players (5).", ex.Message);
    
    // Verify DB was NOT called
    _mockDbContext.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
}
```

### Test Case #2: MinPlayers = MaxPlayers (Boundary - Ngay biên)
```csharp
[Fact]
public async Task CreateTemplateAsync_WhenMinPlayersEqualsMaxPlayers_ReturnsTemplate()
{
    // Arrange
    var userId = "user-123";
    var dto = new CreatePayoutTemplateDto
    {
        Name = "Equal Players Template",
        MinPlayers = 5,
        MaxPlayers = 5, // SAME as MinPlayers
        Distribution = new List<RankPercentDto>
        {
            new() { Rank = 1, Percent = 100 }
        }
    };
    
    // Setup mock DbContext...

    // Act
    var result = await _payoutService.CreateTemplateAsync(userId, dto);

    // Assert
    Assert.NotNull(result);
    Assert.Equal(5, result.MinPlayers);
    Assert.Equal(5, result.MaxPlayers);
}
```

### Test Case #4, #5, #6, #7: Boundary tests for TotalPercent
```csharp
[Fact]
public async Task CreateTemplateAsync_WhenTotalPercentIs99_98_ThrowsInvalidOperationException()
{
    // Arrange - Sum = 99.98 (error = 0.02 > 0.01)
    var dto = new CreatePayoutTemplateDto
    {
        Name = "Test",
        MinPlayers = 4,
        MaxPlayers = 8,
        Distribution = new List<RankPercentDto>
        {
            new() { Rank = 1, Percent = 59.98 },
            new() { Rank = 2, Percent = 40 }
            // Total = 99.98
        }
    };

    // Act & Assert
    var ex = await Assert.ThrowsAsync<InvalidOperationException>(
        () => _payoutService.CreateTemplateAsync("user-123", dto));
    Assert.Contains("Total percentage must be exactly 100%", ex.Message);
    Assert.Contains("99.98", ex.Message);
}

[Fact]
public async Task CreateTemplateAsync_WhenTotalPercentIs99_99_ReturnsTemplate()
{
    // Arrange - Sum = 99.99 (error = 0.01, exactly at boundary)
    var dto = new CreatePayoutTemplateDto
    {
        Name = "Test",
        MinPlayers = 4,
        MaxPlayers = 8,
        Distribution = new List<RankPercentDto>
        {
            new() { Rank = 1, Percent = 59.99 },
            new() { Rank = 2, Percent = 40 }
            // Total = 99.99
        }
    };
    
    // Setup mock...

    // Act
    var result = await _payoutService.CreateTemplateAsync("user-123", dto);

    // Assert - Should NOT throw
    Assert.NotNull(result);
}
```

### Test Case #8: Happy path - Full validation
```csharp
[Fact]
public async Task CreateTemplateAsync_WhenAllValid_ReturnsCorrectDto()
{
    // Arrange
    var userId = "user-123";
    var distribution = new List<RankPercentDto>
    {
        new() { Rank = 1, Percent = 60 },
        new() { Rank = 2, Percent = 30 },
        new() { Rank = 3, Percent = 10 }
    };
    var dto = new CreatePayoutTemplateDto
    {
        Name = "Standard Payout",
        MinPlayers = 4,
        MaxPlayers = 16,
        Distribution = distribution
    };
    
    // Setup mock DbContext to assign Id on SaveChangesAsync
    PayoutTemplate capturedEntity = null;
    _mockDbSet.Setup(x => x.Add(It.IsAny<PayoutTemplate>()))
        .Callback<PayoutTemplate>(e => 
        {
            capturedEntity = e;
            e.Id = 42; // Simulate DB-generated Id
        });

    // Act
    var result = await _payoutService.CreateTemplateAsync(userId, dto);

    // Assert
    Assert.NotNull(result);
    Assert.Equal(42, result.Id);
    Assert.Equal("Standard Payout", result.Name);
    Assert.Equal(4, result.MinPlayers);
    Assert.Equal(16, result.MaxPlayers);
    Assert.Equal(3, result.Places);
    Assert.Equal(distribution, result.Distribution);
}
```

### Test Case #10: Name is trimmed
```csharp
[Fact]
public async Task CreateTemplateAsync_VerifyNameIsTrimmed()
{
    // Arrange
    var dto = new CreatePayoutTemplateDto
    {
        Name = "  Template With Spaces  ", // Spaces at both ends
        MinPlayers = 4,
        MaxPlayers = 8,
        Distribution = new List<RankPercentDto> { new() { Rank = 1, Percent = 100 } }
    };
    
    PayoutTemplate capturedEntity = null;
    _mockDbSet.Setup(x => x.Add(It.IsAny<PayoutTemplate>()))
        .Callback<PayoutTemplate>(e => capturedEntity = e);

    // Act
    var result = await _payoutService.CreateTemplateAsync("user-123", dto);

    // Assert
    Assert.Equal("Template With Spaces", capturedEntity.Name); // Trimmed
    Assert.Equal("Template With Spaces", result.Name);
}
```

### Test Case #13: PercentJson is serialized correctly
```csharp
[Fact]
public async Task CreateTemplateAsync_VerifyPercentJsonIsSerialized()
{
    // Arrange
    var distribution = new List<RankPercentDto>
    {
        new() { Rank = 1, Percent = 60 },
        new() { Rank = 2, Percent = 40 }
    };
    var dto = new CreatePayoutTemplateDto
    {
        Name = "Test",
        MinPlayers = 4,
        MaxPlayers = 8,
        Distribution = distribution
    };
    
    PayoutTemplate capturedEntity = null;
    _mockDbSet.Setup(x => x.Add(It.IsAny<PayoutTemplate>()))
        .Callback<PayoutTemplate>(e => capturedEntity = e);

    // Act
    await _payoutService.CreateTemplateAsync("user-123", dto);

    // Assert
    Assert.NotNull(capturedEntity.PercentJson);
    var deserialized = JsonSerializer.Deserialize<List<RankPercentDto>>(capturedEntity.PercentJson);
    Assert.Equal(2, deserialized.Count);
    Assert.Equal(60, deserialized[0].Percent);
    Assert.Equal(40, deserialized[1].Percent);
}
```

---

## Tổng kết Coverage

| Nhánh Code | Test Case IDs |
|:-----------|:--------------|
| `dto.MinPlayers > dto.MaxPlayers` = true | #1 |
| `dto.MinPlayers > dto.MaxPlayers` = false (equal) | #2 |
| `dto.MinPlayers > dto.MaxPlayers` = false (less) | #3, #8, #9, #10, #11, #12, #13 |
| `Math.Abs(totalPercent - 100) > 0.01` = true (under) | #4 |
| `Math.Abs(totalPercent - 100) > 0.01` = true (over) | #7 |
| `Math.Abs(totalPercent - 100) > 0.01` = false (99.99) | #5 |
| `Math.Abs(totalPercent - 100) > 0.01` = false (100.01) | #6 |
| Entity creation & DB save | #8, #9 |
| Name.Trim() | #10 |
| OwnerUserId assignment | #11 |
| Places = Distribution.Count | #12 |
| PercentJson serialization | #13 |

---

## Thống kê

- **Tổng số Test Cases:** 13
- **Happy Path:** 2 (ID #8, #9)
- **Error Cases:** 3 (ID #1, #4, #7)
- **Boundary Cases:** 4 (ID #2, #3, #5, #6)
- **Edge Cases:** 4 (ID #10, #11, #12, #13)

**Code Coverage dự kiến:** 100% cho hàm `CreateTemplateAsync`

---

## ⚠️ Đề xuất cải thiện Code

1. **Thêm null check cho `dto`:**
   ```csharp
   ArgumentNullException.ThrowIfNull(dto);
   ```

2. **Thêm null check cho `userId`:**
   ```csharp
   ArgumentException.ThrowIfNullOrWhiteSpace(userId);
   ```

3. **Thêm null check cho `dto.Distribution`:**
   ```csharp
   if (dto.Distribution == null || !dto.Distribution.Any())
   {
       throw new InvalidOperationException("Distribution cannot be empty.");
   }
   ```

4. **Thêm null check cho `dto.Name`:**
   ```csharp
   if (string.IsNullOrWhiteSpace(dto.Name))
   {
       throw new InvalidOperationException("Template name cannot be empty.");
   }
   ```

5. **Validate Rank không trùng lặp trong Distribution**

6. **Validate Percent >= 0 cho mỗi item trong Distribution**

