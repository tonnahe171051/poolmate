# SlugHelper - Hướng dẫn sử dụng

## 📍 File đã tạo

```
Common/SlugHelper.cs
```

## 🎯 Chức năng

**SlugHelper.GenerateSlug(string name)** - Chuyển đổi tên có dấu (tiếng Việt) thành slug không dấu, chữ thường, phân cách bằng dấu gạch ngang.

## ✨ Tính năng

1. ✅ **Bỏ dấu tiếng Việt**: á à ả ã ạ → a, ê → e, ô → o, v.v.
2. ✅ **Chuyển chữ thường**: ABC → abc
3. ✅ **Thay khoảng trắng**: "Hello World" → "hello-world"
4. ✅ **Loại bỏ ký tự đặc biệt**: "Name@#$123" → "name123"
5. ✅ **Trim dấu gạch ngang**: "---name---" → "name"

## 💡 Ví dụ sử dụng

### Ví dụ 1: Tên tiếng Việt
```csharp
string name = "Nguyễn Văn Ánh";
string slug = SlugHelper.GenerateSlug(name);
// Result: "nguyen-van-anh"
```

### Ví dụ 2: Tên có ký tự đặc biệt
```csharp
string name = "John@Doe #123!";
string slug = SlugHelper.GenerateSlug(name);
// Result: "johndoe-123"
```

### Ví dụ 3: Tên có khoảng trắng nhiều
```csharp
string name = "Hello    World   Test";
string slug = SlugHelper.GenerateSlug(name);
// Result: "hello-world-test"
```

### Ví dụ 4: Tên tiếng Việt phức tạp
```csharp
string name = "Trần Thị Hương Giang";
string slug = SlugHelper.GenerateSlug(name);
// Result: "tran-thi-huong-giang"
```

### Ví dụ 5: Tên có số
```csharp
string name = "Player 2024 #1";
string slug = SlugHelper.GenerateSlug(name);
// Result: "player-2024-1"
```

## 🔧 Đã tích hợp vào

### 1. PlayerProfileService.cs
```csharp
var newPlayer = new Player
{
    FullName = fullNameMap,
    Slug = SlugHelper.GenerateSlug(fullNameMap), // ✅ Sử dụng SlugHelper
    // ...
};
```

### 2. PlayerSeed.cs
```csharp
var player = new Player
{
    FullName = fullName,
    Slug = SlugHelper.GenerateSlug(fullName), // ✅ Sử dụng SlugHelper
    // ...
};
```

## 📊 Test Cases

| Input | Output |
|-------|--------|
| "Nguyễn Văn A" | "nguyen-van-a" |
| "Lê Thị Bích Ngọc" | "le-thi-bich-ngoc" |
| "Player #123" | "player-123" |
| "John Doe" | "john-doe" |
| "  Spaces  " | "spaces" |
| "CamelCase Name" | "camelcase-name" |
| "Đặng Quốc Việt" | "dang-quoc-viet" |
| "Hồ Chí Minh" | "ho-chi-minh" |

## 🚀 Cách sử dụng trong code mới

```csharp
using PoolMate.Api.Common;

// Trong service hoặc seed
var playerName = "Trần Văn Thành";
var slug = SlugHelper.GenerateSlug(playerName);
// slug = "tran-van-thanh"

// Sử dụng cho Player
var player = new Player
{
    FullName = playerName,
    Slug = slug
};
```

## ⚠️ Lưu ý

1. **Null/Empty handling**: Nếu input null hoặc empty, return empty string
2. **Unique constraint**: Slug trong database có unique constraint, nên cần handle duplicate
3. **Case sensitivity**: Output luôn là lowercase
4. **Unicode normalization**: Sử dụng FormD và FormC để xử lý dấu

## ✅ Build Status

```
✅ Build successful
✅ No errors
✅ Integrated in PlayerProfileService
✅ Integrated in PlayerSeed
✅ Ready to use
```

## 🎉 Hoàn thành!

SlugHelper đã sẵn sàng để sử dụng trong toàn bộ dự án cho việc tạo slug từ tên người chơi tiếng Việt.

