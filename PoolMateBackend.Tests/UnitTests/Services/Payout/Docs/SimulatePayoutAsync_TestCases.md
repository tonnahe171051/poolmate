# Test Cases cho `SimulatePayoutAsync` - PayoutService

## Thông tin hàm

**File:** `PoolMateBackend/Services/PayoutService.cs`

**Signature:**
```csharp
public async Task<PayoutSimulationResultDto> SimulatePayoutAsync(
    PayoutSimulationRequestDto request,
    CancellationToken ct = default)
```

---

## Phân tích Control Flow

Hàm `SimulatePayoutAsync` có các nhánh điều kiện sau:

```
┌─────────────────────────────────────────────────────────────┐
│                    SimulatePayoutAsync                       │
└─────────────────────────────────────────────────────────────┘
                              │
                              ▼
              ┌───────────────────────────────┐
              │ request.TemplateId.HasValue?  │
              └───────────────────────────────┘
                    │YES              │NO
                    ▼                 ▼
        ┌──────────────────┐  ┌─────────────────────────────┐
        │ Query Template   │  │ CustomDistribution != null  │
        │ from DB          │  │ && Any() ?                  │
        └──────────────────┘  └─────────────────────────────┘
              │                     │YES            │NO
              ▼                     ▼               ▼
    ┌─────────────────┐   ┌────────────────┐  ┌────────────┐
    │ template null?  │   │ Sum(%) valid?  │  │ THROW      │
    └─────────────────┘   └────────────────┘  │ "Provide   │
      │YES      │NO          │YES    │NO     │ TemplateId │
      ▼         ▼            ▼       ▼       │ or Custom" │
   THROW    Deserialize   USE     THROW      └────────────┘
   "Not     PercentJson   Custom  "Total %
   found"                        must be 100"
              │                    │
              └────────┬───────────┘
                       ▼
         ┌─────────────────────────────────┐
         │ TotalPrizePool <= 0 ||          │
         │ !distribution.Any() ?           │
         └─────────────────────────────────┘
               │YES              │NO
               ▼                 ▼
         Return empty      Calculate loop
         result            (foreach rank)
                                 │
                                 ▼
                    ┌─────────────────────────┐
                    │ currentSum != TotalPrize│
                    │ Pool ?                  │
                    └─────────────────────────┘
                         │YES        │NO
                         ▼           ▼
                    Adjust      Return result
                    first rank
                         │
                         ▼
                    Return result
```

---

## Boundary Analysis

### 1. TotalPrizePool (so sánh `<= 0`)
| Vị trí | Giá trị | Kết quả |
|:-------|:--------|:--------|
| Dưới biên | `-1`, `-1000` (âm) | Return empty result |
| Ngay biên | `0` | Return empty result |
| Trên biên | `0.01`, `1` (dương) | Tiếp tục tính toán |

### 2. TotalPercent (so sánh `Math.Abs(totalPercent - 100) > 0.01`)
| Vị trí | Giá trị | Sai số | Kết quả |
|:-------|:--------|:-------|:--------|
| Dưới biên | `99.98` | 0.02 > 0.01 | Throw InvalidOperationException |
| Ngay biên (dưới) | `99.99` | 0.01 = 0.01 | ✅ Hợp lệ |
| Chính xác | `100.00` | 0.00 | ✅ Hợp lệ |
| Ngay biên (trên) | `100.01` | 0.01 = 0.01 | ✅ Hợp lệ |
| Trên biên | `100.02` | 0.02 > 0.01 | Throw InvalidOperationException |

---

## Bảng Test Cases

| ID | Loại | Tên Test Case (Gợi ý đặt tên) | Input Giả định (Mock Data) | Kết quả Mong đợi | Ghi chú |
|:---|:-----|:------------------------------|:---------------------------|:-----------------|:--------|
| 1 | Error | SimulatePayoutAsync_WhenTemplateIdProvidedButNotFound_ThrowsInvalidOperationException | `request.TemplateId = 999`, DB không có template này | Throw `InvalidOperationException("Payout template with ID 999 not found.")` | [Fact] |
| 2 | Happy | SimulatePayoutAsync_WhenTemplateIdProvidedAndFound_UsesTemplateDistribution | `request.TemplateId = 1`, template tồn tại với PercentJson hợp lệ, `TotalPrizePool = 1000` | Return result với Breakdown được tính từ template | [Fact] |
| 3 | Edge | SimulatePayoutAsync_WhenTemplatePercentJsonDeserializesToNull_UsesEmptyDistribution | `request.TemplateId = 1`, template.PercentJson = `"null"` | Return result với `Breakdown = []` (do early return) | [Fact] |
| 4 | Happy | SimulatePayoutAsync_WhenCustomDistributionValid_UsesCustomDistribution | `CustomDistribution = [{Rank:1, Percent:60}, {Rank:2, Percent:40}]`, `TotalPrizePool = 1000` | Return result với Breakdown = `[{1, 60, 600}, {2, 40, 400}]` | [Fact] |
| 5 | Boundary | SimulatePayoutAsync_WhenCustomDistributionTotalIs99_98_ThrowsInvalidOperationException | `CustomDistribution` sum = 99.98 (sai số 0.02) | Throw `InvalidOperationException("Total percentage must be exactly 100%...")` | [Fact] - Dưới biên |
| 6 | Boundary | SimulatePayoutAsync_WhenCustomDistributionTotalIs99_99_ReturnsResult | `CustomDistribution` sum = 99.99 (sai số 0.01) | Return result hợp lệ | [Fact] - Ngay biên (valid) |
| 7 | Boundary | SimulatePayoutAsync_WhenCustomDistributionTotalIs100_01_ReturnsResult | `CustomDistribution` sum = 100.01 (sai số 0.01) | Return result hợp lệ | [Fact] - Ngay biên (valid) |
| 8 | Boundary | SimulatePayoutAsync_WhenCustomDistributionTotalIs100_02_ThrowsInvalidOperationException | `CustomDistribution` sum = 100.02 (sai số 0.02) | Throw `InvalidOperationException` | [Fact] - Trên biên |
| 9 | Error | SimulatePayoutAsync_WhenNoTemplateIdAndNoCustomDistribution_ThrowsInvalidOperationException | `TemplateId = null`, `CustomDistribution = null` | Throw `InvalidOperationException("Please provide either a TemplateId or a CustomDistribution.")` | [Fact] |
| 10 | Error | SimulatePayoutAsync_WhenCustomDistributionIsEmpty_ThrowsInvalidOperationException | `TemplateId = null`, `CustomDistribution = []` (empty list) | Throw `InvalidOperationException("Please provide either a TemplateId or a CustomDistribution.")` | [Fact] |
| 11 | Boundary | SimulatePayoutAsync_WhenTotalPrizePoolIsNegative_ReturnsEmptyBreakdown | `TotalPrizePool = -100`, valid distribution | Return `{ TotalPrize = -100, Breakdown = [] }` | [Theory] với `[InlineData(-1)]`, `[InlineData(-1000)]` |
| 12 | Boundary | SimulatePayoutAsync_WhenTotalPrizePoolIsZero_ReturnsEmptyBreakdown | `TotalPrizePool = 0`, valid distribution | Return `{ TotalPrize = 0, Breakdown = [] }` | [Fact] - Ngay biên |
| 13 | Boundary | SimulatePayoutAsync_WhenTotalPrizePoolIsSmallPositive_CalculatesBreakdown | `TotalPrizePool = 0.01`, valid distribution | Return result với Breakdown được tính | [Fact] - Trên biên |
| 14 | Edge | SimulatePayoutAsync_WhenDistributionIsEmptyAfterDeserialize_ReturnsEmptyBreakdown | Template with empty PercentJson `"[]"`, `TotalPrizePool = 1000` | Return `{ TotalPrize = 1000, Breakdown = [] }` | [Fact] |
| 15 | Happy | SimulatePayoutAsync_WhenRoundingDifferenceExists_AdjustsFirstRank | `TotalPrizePool = 100`, distribution = `[{Rank:1, Percent:33.33}, {Rank:2, Percent:33.33}, {Rank:3, Percent:33.34}]` | Tổng sau làm tròn có thể sai lệch → điều chỉnh vào Rank 1 | [Fact] |
| 16 | Happy | SimulatePayoutAsync_WhenNoRoundingDifference_NoAdjustment | `TotalPrizePool = 100`, distribution = `[{Rank:1, Percent:50}, {Rank:2, Percent:50}]` | Breakdown = `[{1, 50, 50}, {2, 50, 50}]`, không cần điều chỉnh | [Fact] |
| 17 | Edge | SimulatePayoutAsync_VerifyBreakdownOrderedByRank | Distribution không theo thứ tự: `[{Rank:3, ...}, {Rank:1, ...}, {Rank:2, ...}]` | Breakdown trả về được sắp xếp theo Rank tăng dần | [Fact] |
| 18 | Happy | SimulatePayoutAsync_WhenMultipleRanks_CalculatesCorrectAmounts | `TotalPrizePool = 10000`, 5 ranks với % khác nhau | Verify Amount = `TotalPrizePool * (Percent/100)` làm tròn 2 chữ số | [Fact] |

---

## Chi tiết Mock Setup cho từng Test Case

### Test Case #1: Template not found
```csharp
[Fact]
public async Task SimulatePayoutAsync_WhenTemplateIdProvidedButNotFound_ThrowsInvalidOperationException()
{
    // Arrange
    var request = new PayoutSimulationRequestDto
    {
        TemplateId = 999,
        TotalPrizePool = 1000
    };
    
    // Mock DbContext - PayoutTemplates returns empty
    _mockDbContext.Setup(x => x.PayoutTemplates).Returns(MockDbSet(new List<PayoutTemplate>()));

    // Act & Assert
    var ex = await Assert.ThrowsAsync<InvalidOperationException>(
        () => _payoutService.SimulatePayoutAsync(request));
    Assert.Equal("Payout template with ID 999 not found.", ex.Message);
}
```

### Test Case #2: Template found, use template distribution
```csharp
[Fact]
public async Task SimulatePayoutAsync_WhenTemplateIdProvidedAndFound_UsesTemplateDistribution()
{
    // Arrange
    var template = new PayoutTemplate
    {
        Id = 1,
        PercentJson = "[{\"Rank\":1,\"Percent\":60},{\"Rank\":2,\"Percent\":40}]"
    };
    var request = new PayoutSimulationRequestDto
    {
        TemplateId = 1,
        TotalPrizePool = 1000
    };
    
    // Mock DbContext
    _mockDbContext.Setup(x => x.PayoutTemplates).Returns(MockDbSet(new List<PayoutTemplate> { template }));

    // Act
    var result = await _payoutService.SimulatePayoutAsync(request);

    // Assert
    Assert.Equal(1000, result.TotalPrize);
    Assert.Equal(2, result.Breakdown.Count);
    Assert.Equal(600, result.Breakdown[0].Amount); // 60% of 1000
    Assert.Equal(400, result.Breakdown[1].Amount); // 40% of 1000
}
```

### Test Case #5, #6, #7, #8: Boundary tests for percentage sum
```csharp
[Fact]
public async Task SimulatePayoutAsync_WhenCustomDistributionTotalIs99_98_ThrowsInvalidOperationException()
{
    // Arrange - Sum = 99.98 (error = 0.02 > 0.01)
    var request = new PayoutSimulationRequestDto
    {
        CustomDistribution = new List<RankPercentDto>
        {
            new() { Rank = 1, Percent = 59.98 },
            new() { Rank = 2, Percent = 40 }
        },
        TotalPrizePool = 1000
    };

    // Act & Assert
    var ex = await Assert.ThrowsAsync<InvalidOperationException>(
        () => _payoutService.SimulatePayoutAsync(request));
    Assert.Contains("Total percentage must be exactly 100%", ex.Message);
}

[Fact]
public async Task SimulatePayoutAsync_WhenCustomDistributionTotalIs99_99_ReturnsResult()
{
    // Arrange - Sum = 99.99 (error = 0.01, exactly at boundary, should be valid)
    var request = new PayoutSimulationRequestDto
    {
        CustomDistribution = new List<RankPercentDto>
        {
            new() { Rank = 1, Percent = 59.99 },
            new() { Rank = 2, Percent = 40 }
        },
        TotalPrizePool = 1000
    };

    // Act
    var result = await _payoutService.SimulatePayoutAsync(request);

    // Assert - Should NOT throw
    Assert.NotNull(result);
    Assert.Equal(2, result.Breakdown.Count);
}
```

### Test Case #11, #12, #13: Boundary tests for TotalPrizePool
```csharp
[Theory]
[InlineData(-1)]
[InlineData(-1000)]
public async Task SimulatePayoutAsync_WhenTotalPrizePoolIsNegative_ReturnsEmptyBreakdown(decimal prizePool)
{
    // Arrange
    var request = new PayoutSimulationRequestDto
    {
        CustomDistribution = new List<RankPercentDto>
        {
            new() { Rank = 1, Percent = 100 }
        },
        TotalPrizePool = prizePool
    };

    // Act
    var result = await _payoutService.SimulatePayoutAsync(request);

    // Assert
    Assert.Equal(prizePool, result.TotalPrize);
    Assert.Empty(result.Breakdown);
}

[Fact]
public async Task SimulatePayoutAsync_WhenTotalPrizePoolIsZero_ReturnsEmptyBreakdown()
{
    // Arrange
    var request = new PayoutSimulationRequestDto
    {
        CustomDistribution = new List<RankPercentDto>
        {
            new() { Rank = 1, Percent = 100 }
        },
        TotalPrizePool = 0
    };

    // Act
    var result = await _payoutService.SimulatePayoutAsync(request);

    // Assert
    Assert.Equal(0, result.TotalPrize);
    Assert.Empty(result.Breakdown);
}
```

### Test Case #15: Rounding adjustment
```csharp
[Fact]
public async Task SimulatePayoutAsync_WhenRoundingDifferenceExists_AdjustsFirstRank()
{
    // Arrange - 33.33% + 33.33% + 33.34% = 100%, but rounding may cause issues
    var request = new PayoutSimulationRequestDto
    {
        CustomDistribution = new List<RankPercentDto>
        {
            new() { Rank = 1, Percent = 33.33 },
            new() { Rank = 2, Percent = 33.33 },
            new() { Rank = 3, Percent = 33.34 }
        },
        TotalPrizePool = 100
    };

    // Act
    var result = await _payoutService.SimulatePayoutAsync(request);

    // Assert
    var totalAmount = result.Breakdown.Sum(x => x.Amount);
    Assert.Equal(100, totalAmount); // Total must equal TotalPrizePool after adjustment
}
```

### Test Case #17: Verify ordering by Rank
```csharp
[Fact]
public async Task SimulatePayoutAsync_VerifyBreakdownOrderedByRank()
{
    // Arrange - Distribution NOT in order
    var request = new PayoutSimulationRequestDto
    {
        CustomDistribution = new List<RankPercentDto>
        {
            new() { Rank = 3, Percent = 20 },
            new() { Rank = 1, Percent = 50 },
            new() { Rank = 2, Percent = 30 }
        },
        TotalPrizePool = 1000
    };

    // Act
    var result = await _payoutService.SimulatePayoutAsync(request);

    // Assert - Verify ordered by Rank ascending
    Assert.Equal(1, result.Breakdown[0].Rank);
    Assert.Equal(2, result.Breakdown[1].Rank);
    Assert.Equal(3, result.Breakdown[2].Rank);
}
```

---

## Tổng kết Coverage

| Nhánh Code | Test Case IDs |
|:-----------|:--------------|
| `request.TemplateId.HasValue` = true | #1, #2, #3, #14 |
| `template == null` | #1 |
| `template != null` → Deserialize | #2, #3, #14 |
| Deserialize returns null → empty list | #3 |
| `CustomDistribution != null && Any()` = true | #4, #5, #6, #7, #8, #11, #12, #13, #15, #16, #17, #18 |
| `Math.Abs(totalPercent - 100) > 0.01` = true | #5, #8 |
| `Math.Abs(totalPercent - 100) > 0.01` = false | #4, #6, #7, #11, #12, #13, #15, #16, #17, #18 |
| Neither TemplateId nor CustomDistribution | #9, #10 |
| `TotalPrizePool <= 0` = true | #11, #12 |
| `TotalPrizePool <= 0` = false | #2, #4, #13, #15, #16, #17, #18 |
| `!distribution.Any()` = true | #3, #14 |
| foreach loop (calculation) | #2, #4, #13, #15, #16, #17, #18 |
| `currentSum != TotalPrizePool` = true | #15 |
| `currentSum != TotalPrizePool` = false | #16 |
| `result.Breakdown.Count > 0` (in rounding) | #15 |

---

## Thống kê

- **Tổng số Test Cases:** 18
- **Happy Path:** 4 (ID #2, #4, #16, #18)
- **Error Cases:** 4 (ID #1, #9, #10)
- **Boundary Cases:** 7 (ID #5, #6, #7, #8, #11, #12, #13)
- **Edge Cases:** 4 (ID #3, #14, #15, #17)

**Code Coverage dự kiến:** 100% cho hàm `SimulatePayoutAsync`

---

## ⚠️ Đề xuất cải thiện Code

1. **Thêm null check cho `request`:**
   ```csharp
   ArgumentNullException.ThrowIfNull(request);
   ```

2. **Handle JsonException khi deserialize:**
   ```csharp
   try
   {
       distribution = JsonSerializer.Deserialize<List<RankPercentDto>>(template.PercentJson)
                      ?? new List<RankPercentDto>();
   }
   catch (JsonException ex)
   {
       _logger.LogWarning(ex, "Failed to deserialize PercentJson for template {Id}", template.Id);
       distribution = new List<RankPercentDto>();
   }
   ```

3. **Validate Rank không trùng lặp trong CustomDistribution**

4. **Validate Percent >= 0 cho mỗi item trong distribution**

