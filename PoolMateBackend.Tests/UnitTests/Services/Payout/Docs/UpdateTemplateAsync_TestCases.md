# Test Cases cho `UpdateTemplateAsync` - PayoutService

## Thông tin hàm

**File:** `PoolMateBackend/Services/PayoutService.cs`

**Signature:**
```csharp
public async Task<PayoutTemplateDto?> UpdateTemplateAsync(
    int id, 
    string userId, 
    CreatePayoutTemplateDto dto,
    CancellationToken ct = default)
```

---

## ⚠️ BUG DETECTED

**Vấn đề:** `SaveChangesAsync()` được gọi **2 lần** liên tiếp (dòng 166 và 169 trong code gốc).

```csharp
await _db.SaveChangesAsync(ct);  // Dòng 166 - Lưu lần 1

// 4. Lưu thay đổi
await _db.SaveChangesAsync(ct);  // Dòng 169 - Lưu lần 2 (DƯ THỪA!)
```

**Đề xuất:** Xóa một trong hai lệnh `SaveChangesAsync()`.

---

## Phân tích Control Flow

Hàm `UpdateTemplateAsync` có các nhánh điều kiện sau:

```
┌─────────────────────────────────────────────────────────────┐
│                    UpdateTemplateAsync                       │
└─────────────────────────────────────────────────────────────┘
                              │
                              ▼
              ┌───────────────────────────────┐
              │ Query: Find by Id AND UserId  │
              │ (Ownership check)             │
              └───────────────────────────────┘
                              │
                              ▼
              ┌───────────────────────────────┐
              │       entity == null ?        │
              └───────────────────────────────┘
                    │YES              │NO
                    ▼                 ▼
              ┌──────────┐   ┌─────────────────────────────┐
              │ return   │   │ dto.MinPlayers > MaxPlayers?│
              │ null     │   │          (Rule A)           │
              └──────────┘   └─────────────────────────────┘
                                   │YES              │NO
                                   ▼                 ▼
                             ┌──────────┐   ┌────────────────────┐
                             │ THROW    │   │ Calculate total %  │
                             │ "Min >   │   │ Math.Abs() > 0.01? │
                             │  Max"    │   │      (Rule B)      │
                             └──────────┘   └────────────────────┘
                                                 │YES      │NO
                                                 ▼         ▼
                                           ┌──────────┐ ┌────────────────┐
                                           │ THROW    │ │ Update entity  │
                                           │ "Total % │ │ SaveChanges x2 │
                                           │ must be  │ │ Return DTO     │
                                           │ 100"     │ └────────────────┘
                                           └──────────┘
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
| 1 | Error | UpdateTemplateAsync_WhenTemplateNotFound_ReturnsNull | `id = 999` (không tồn tại trong DB), `userId = "user-123"` | Return `null` | [Fact] |
| 2 | Error | UpdateTemplateAsync_WhenUserNotOwner_ReturnsNull | `id = 1` (tồn tại nhưng `OwnerUserId = "other-user"`), `userId = "user-123"` | Return `null` | [Fact] |
| 3 | Error | UpdateTemplateAsync_WhenMinPlayersGreaterThanMaxPlayers_ThrowsInvalidOperationException | Entity tồn tại và đúng owner, `dto.MinPlayers = 10`, `dto.MaxPlayers = 5` | Throw `InvalidOperationException("Min Players (10) cannot be greater than Max Players (5).")` | [Fact] |
| 4 | Boundary | UpdateTemplateAsync_WhenMinPlayersEqualsMaxPlayers_ReturnsUpdatedTemplate | Entity tồn tại, `dto.MinPlayers = 5`, `dto.MaxPlayers = 5`, valid distribution | Return `PayoutTemplateDto` thành công | [Fact] - Ngay biên |
| 5 | Boundary | UpdateTemplateAsync_WhenMinPlayersLessThanMaxPlayers_ReturnsUpdatedTemplate | Entity tồn tại, `dto.MinPlayers = 4`, `dto.MaxPlayers = 8`, valid distribution | Return `PayoutTemplateDto` thành công | [Fact] - Dưới biên |
| 6 | Boundary | UpdateTemplateAsync_WhenTotalPercentIs99_98_ThrowsInvalidOperationException | Distribution sum = 99.98 (sai số 0.02) | Throw `InvalidOperationException("Total percentage must be exactly 100%...")` | [Fact] - Dưới biên |
| 7 | Boundary | UpdateTemplateAsync_WhenTotalPercentIs99_99_ReturnsUpdatedTemplate | Distribution sum = 99.99 (sai số 0.01) | Return `PayoutTemplateDto` thành công | [Fact] - Ngay biên (valid) |
| 8 | Boundary | UpdateTemplateAsync_WhenTotalPercentIs100_01_ReturnsUpdatedTemplate | Distribution sum = 100.01 (sai số 0.01) | Return `PayoutTemplateDto` thành công | [Fact] - Ngay biên (valid) |
| 9 | Boundary | UpdateTemplateAsync_WhenTotalPercentIs100_02_ThrowsInvalidOperationException | Distribution sum = 100.02 (sai số 0.02) | Throw `InvalidOperationException` | [Fact] - Trên biên |
| 10 | Happy | UpdateTemplateAsync_WhenAllValid_ReturnsCorrectDto | Entity tồn tại với valid dto: `Name = "Updated"`, `MinPlayers = 4`, `MaxPlayers = 16` | Return DTO với `Id`, `Name`, `MinPlayers`, `MaxPlayers`, `Places`, `Distribution` đúng | [Fact] |
| 11 | Happy | UpdateTemplateAsync_WhenValid_SavesChangesToDatabase | Valid update | Verify `SaveChangesAsync()` được gọi (hiện tại gọi 2 lần do bug) | [Fact] |
| 12 | Edge | UpdateTemplateAsync_VerifyNameIsTrimmed | `dto.Name = "  Updated Template  "` (có spaces đầu/cuối) | Entity.Name = `"Updated Template"` (đã trim) | [Fact] |
| 13 | Edge | UpdateTemplateAsync_VerifyPlacesEqualsDistributionCount | Distribution có 5 items | Entity.Places = 5, DTO.Places = 5 | [Fact] |
| 14 | Edge | UpdateTemplateAsync_VerifyPercentJsonIsSerialized | Distribution = `[{Rank:1, Percent:60}, {Rank:2, Percent:40}]` | Entity.PercentJson = valid JSON string | [Fact] |
| 15 | Edge | UpdateTemplateAsync_VerifyEntityPropertiesAreUpdated | Old entity có `Name="Old"`, update với `Name="New"` | Verify entity properties được update đúng | [Fact] |

---

## Chi tiết Mock Setup cho từng Test Case

### Test Case #1: Template not found
```csharp
[Fact]
public async Task UpdateTemplateAsync_WhenTemplateNotFound_ReturnsNull()
{
    // Arrange
    var id = 999; // Non-existent ID
    var userId = "user-123";
    var dto = new CreatePayoutTemplateDto
    {
        Name = "Test",
        MinPlayers = 4,
        MaxPlayers = 8,
        Distribution = new List<RankPercentDto> { new() { Rank = 1, Percent = 100 } }
    };
    
    // Mock: No templates in DB
    _mockDbContext.Setup(x => x.PayoutTemplates).Returns(MockDbSet(new List<PayoutTemplate>()));

    // Act
    var result = await _payoutService.UpdateTemplateAsync(id, userId, dto);

    // Assert
    Assert.Null(result);
    _mockDbContext.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
}
```

### Test Case #2: User not owner
```csharp
[Fact]
public async Task UpdateTemplateAsync_WhenUserNotOwner_ReturnsNull()
{
    // Arrange
    var existingTemplate = new PayoutTemplate
    {
        Id = 1,
        OwnerUserId = "other-user", // Different owner
        Name = "Existing Template"
    };
    var userId = "user-123"; // Current user trying to update
    var dto = new CreatePayoutTemplateDto
    {
        Name = "Updated",
        MinPlayers = 4,
        MaxPlayers = 8,
        Distribution = new List<RankPercentDto> { new() { Rank = 1, Percent = 100 } }
    };
    
    _mockDbContext.Setup(x => x.PayoutTemplates)
        .Returns(MockDbSet(new List<PayoutTemplate> { existingTemplate }));

    // Act
    var result = await _payoutService.UpdateTemplateAsync(1, userId, dto);

    // Assert
    Assert.Null(result);
}
```

### Test Case #3: MinPlayers > MaxPlayers
```csharp
[Fact]
public async Task UpdateTemplateAsync_WhenMinPlayersGreaterThanMaxPlayers_ThrowsInvalidOperationException()
{
    // Arrange
    var existingTemplate = new PayoutTemplate
    {
        Id = 1,
        OwnerUserId = "user-123",
        Name = "Existing"
    };
    var dto = new CreatePayoutTemplateDto
    {
        Name = "Updated",
        MinPlayers = 10, // Greater than MaxPlayers
        MaxPlayers = 5,
        Distribution = new List<RankPercentDto> { new() { Rank = 1, Percent = 100 } }
    };
    
    _mockDbContext.Setup(x => x.PayoutTemplates)
        .Returns(MockDbSet(new List<PayoutTemplate> { existingTemplate }));

    // Act & Assert
    var ex = await Assert.ThrowsAsync<InvalidOperationException>(
        () => _payoutService.UpdateTemplateAsync(1, "user-123", dto));
    Assert.Equal("Min Players (10) cannot be greater than Max Players (5).", ex.Message);
}
```

### Test Case #6, #7, #8, #9: Boundary tests for TotalPercent
```csharp
[Fact]
public async Task UpdateTemplateAsync_WhenTotalPercentIs99_98_ThrowsInvalidOperationException()
{
    // Arrange
    var existingTemplate = new PayoutTemplate { Id = 1, OwnerUserId = "user-123" };
    var dto = new CreatePayoutTemplateDto
    {
        Name = "Test",
        MinPlayers = 4,
        MaxPlayers = 8,
        Distribution = new List<RankPercentDto>
        {
            new() { Rank = 1, Percent = 59.98 },
            new() { Rank = 2, Percent = 40 }
            // Total = 99.98, error = 0.02 > 0.01
        }
    };
    
    _mockDbContext.Setup(x => x.PayoutTemplates)
        .Returns(MockDbSet(new List<PayoutTemplate> { existingTemplate }));

    // Act & Assert
    var ex = await Assert.ThrowsAsync<InvalidOperationException>(
        () => _payoutService.UpdateTemplateAsync(1, "user-123", dto));
    Assert.Contains("Total percentage must be exactly 100%", ex.Message);
}

[Fact]
public async Task UpdateTemplateAsync_WhenTotalPercentIs99_99_ReturnsUpdatedTemplate()
{
    // Arrange - Sum = 99.99, error = 0.01 (exactly at boundary, should pass)
    var existingTemplate = new PayoutTemplate { Id = 1, OwnerUserId = "user-123", Name = "Old" };
    var dto = new CreatePayoutTemplateDto
    {
        Name = "Updated",
        MinPlayers = 4,
        MaxPlayers = 8,
        Distribution = new List<RankPercentDto>
        {
            new() { Rank = 1, Percent = 59.99 },
            new() { Rank = 2, Percent = 40 }
        }
    };
    
    // Setup mock...

    // Act
    var result = await _payoutService.UpdateTemplateAsync(1, "user-123", dto);

    // Assert
    Assert.NotNull(result);
}
```

### Test Case #10: Happy path
```csharp
[Fact]
public async Task UpdateTemplateAsync_WhenAllValid_ReturnsCorrectDto()
{
    // Arrange
    var existingTemplate = new PayoutTemplate
    {
        Id = 42,
        OwnerUserId = "user-123",
        Name = "Old Template",
        MinPlayers = 2,
        MaxPlayers = 4
    };
    var distribution = new List<RankPercentDto>
    {
        new() { Rank = 1, Percent = 60 },
        new() { Rank = 2, Percent = 30 },
        new() { Rank = 3, Percent = 10 }
    };
    var dto = new CreatePayoutTemplateDto
    {
        Name = "Updated Template",
        MinPlayers = 4,
        MaxPlayers = 16,
        Distribution = distribution
    };
    
    _mockDbContext.Setup(x => x.PayoutTemplates)
        .Returns(MockDbSet(new List<PayoutTemplate> { existingTemplate }));

    // Act
    var result = await _payoutService.UpdateTemplateAsync(42, "user-123", dto);

    // Assert
    Assert.NotNull(result);
    Assert.Equal(42, result.Id);
    Assert.Equal("Updated Template", result.Name);
    Assert.Equal(4, result.MinPlayers);
    Assert.Equal(16, result.MaxPlayers);
    Assert.Equal(3, result.Places);
    Assert.Equal(distribution, result.Distribution);
}
```

### Test Case #12: Name is trimmed
```csharp
[Fact]
public async Task UpdateTemplateAsync_VerifyNameIsTrimmed()
{
    // Arrange
    var existingTemplate = new PayoutTemplate { Id = 1, OwnerUserId = "user-123", Name = "Old" };
    var dto = new CreatePayoutTemplateDto
    {
        Name = "  Updated Template With Spaces  ", // Spaces
        MinPlayers = 4,
        MaxPlayers = 8,
        Distribution = new List<RankPercentDto> { new() { Rank = 1, Percent = 100 } }
    };
    
    _mockDbContext.Setup(x => x.PayoutTemplates)
        .Returns(MockDbSet(new List<PayoutTemplate> { existingTemplate }));

    // Act
    var result = await _payoutService.UpdateTemplateAsync(1, "user-123", dto);

    // Assert
    Assert.Equal("Updated Template With Spaces", result.Name); // Trimmed
    Assert.Equal("Updated Template With Spaces", existingTemplate.Name); // Entity also updated
}
```

---

## Tổng kết Coverage

| Nhánh Code | Test Case IDs |
|:-----------|:--------------|
| `entity == null` (not found) | #1 |
| `entity == null` (wrong owner) | #2 |
| `dto.MinPlayers > dto.MaxPlayers` = true | #3 |
| `dto.MinPlayers > dto.MaxPlayers` = false (equal) | #4 |
| `dto.MinPlayers > dto.MaxPlayers` = false (less) | #5, #10, #11, #12, #13, #14, #15 |
| `Math.Abs(totalPercent - 100) > 0.01` = true (under) | #6 |
| `Math.Abs(totalPercent - 100) > 0.01` = true (over) | #9 |
| `Math.Abs(totalPercent - 100) > 0.01` = false (99.99) | #7 |
| `Math.Abs(totalPercent - 100) > 0.01` = false (100.01) | #8 |
| Entity update & DB save | #10, #11 |
| Name.Trim() | #12 |
| Places = Distribution.Count | #13 |
| PercentJson serialization | #14 |
| Entity properties updated | #15 |

---

## So sánh với `CreateTemplateAsync`

| Khác biệt | CreateTemplateAsync | UpdateTemplateAsync |
|:----------|:--------------------|:--------------------|
| Return type | `PayoutTemplateDto` (not nullable) | `PayoutTemplateDto?` (nullable) |
| Entity not found | N/A (tạo mới) | Return `null` |
| Ownership check | Gán `OwnerUserId = userId` | Query với `OwnerUserId == userId` |
| DB operation | `Add()` + `SaveChangesAsync()` | Update properties + `SaveChangesAsync()` x2 (BUG!) |

---

## Thống kê

- **Tổng số Test Cases:** 15
- **Happy Path:** 2 (ID #10, #11)
- **Error Cases:** 5 (ID #1, #2, #3, #6, #9)
- **Boundary Cases:** 4 (ID #4, #5, #7, #8)
- **Edge Cases:** 4 (ID #12, #13, #14, #15)

**Code Coverage dự kiến:** 100% cho hàm `UpdateTemplateAsync`

---

## ⚠️ Đề xuất cải thiện Code

1. **FIX BUG: Xóa `SaveChangesAsync()` dư thừa:**
   ```csharp
   // 3. Cập nhật dữ liệu
   entity.Name = dto.Name.Trim();
   entity.MinPlayers = dto.MinPlayers;
   entity.MaxPlayers = dto.MaxPlayers;
   entity.Places = dto.Distribution.Count;
   entity.PercentJson = JsonSerializer.Serialize(dto.Distribution);

   // 4. Lưu thay đổi (CHỈ 1 LẦN)
   await _db.SaveChangesAsync(ct);
   ```

2. **Thêm null check cho `dto`:**
   ```csharp
   ArgumentNullException.ThrowIfNull(dto);
   ```

3. **Thêm null check cho `userId`:**
   ```csharp
   ArgumentException.ThrowIfNullOrWhiteSpace(userId);
   ```

4. **Thêm null check cho `dto.Distribution`:**
   ```csharp
   if (dto.Distribution == null || !dto.Distribution.Any())
   {
       throw new InvalidOperationException("Distribution cannot be empty.");
   }
   ```

5. **Thêm null check cho `dto.Name`:**
   ```csharp
   if (string.IsNullOrWhiteSpace(dto.Name))
   {
       throw new InvalidOperationException("Template name cannot be empty.");
   }
   ```

