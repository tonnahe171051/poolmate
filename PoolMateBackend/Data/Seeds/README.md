# Seed Data - Hướng dẫn sử dụng

## 📁 Cấu trúc File Seed

Dữ liệu seed đã được tách thành các file riêng biệt để dễ quản lý và fix lỗi:

```
Data/
├── SeedData.cs                    # Master orchestrator - Điều phối các seed
└── Seeds/
    ├── UserSeed.cs                # Seed Users và Roles
    ├── VenueSeed.cs               # Seed Venues (Địa điểm)
    ├── PlayerSeed.cs              # Seed Players (Profile người chơi)
    ├── PayoutTemplateSeed.cs      # Seed PayoutTemplates (Mẫu chia giải)
    └── PostSeed.cs                # Seed Posts (Bài đăng)
```

## 🎯 Cách sử dụng

### Option 1: Gọi API (Khuyến nghị - Dễ nhất)

1. **Chạy ứng dụng:**
```powershell
cd C:\Subject\BackendSEP\poolmate_be\PoolMateBackend
dotnet run
```

2. **Mở Swagger UI:**
```
https://localhost:5001/swagger
```

3. **Gọi các endpoint seed:**

#### Seed từng bảng riêng lẻ:
- `POST /api/seed/users` - Seed Users và Roles (Phải chạy đầu tiên!)
- `POST /api/seed/venues` - Seed Venues
- `POST /api/seed/players` - Seed Players
- `POST /api/seed/payout-templates` - Seed PayoutTemplates
- `POST /api/seed/posts` - Seed Posts

#### Seed tất cả:
- `POST /api/seed/all` - Seed tất cả dữ liệu theo thứ tự

### Option 2: Thêm vào Program.cs (Tự động khi start)

Thêm vào `Program.cs`, ngay trước `app.Run()`:

```csharp
// Seed data (chỉ trong development)
if (app.Environment.IsDevelopment())
{
    using (var scope = app.Services.CreateScope())
    {
        var services = scope.ServiceProvider;
        try
        {
            // Seed tất cả
            await SeedData.SeedAllDataAsync(services);
            
            // Hoặc seed từng phần:
            // await SeedData.SeedUsersAsync(services);
            // await SeedData.SeedVenuesOnlyAsync(services);
            
            Console.WriteLine("✅ Seed data completed successfully!");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error seeding data: {ex.Message}");
        }
    }
}

app.Run();
```

## 📊 Dữ liệu được seed

### 1. Users (UserSeed.cs)

#### Roles:
- `Admin` - Quản trị viên hệ thống
- `Organizer` - Người tổ chức giải đấu
- `Player` - Người chơi

#### Users:
- **1 Admin:** `admin@poolmate.com` / `Admin@123456`
- **2 Organizers:** 
  - `john.organizer@poolmate.com` / `Organizer@123`
  - `sarah.events@poolmate.com` / `Organizer@123`
- **10 Players:** 
  - Format: `{name}.{lastname}@poolmate.com` / `Player@123`
  - Example: `mike.player@poolmate.com`

**Tổng: 13 users**

### 2. Venues (VenueSeed.cs)
- 5 địa điểm billiard ở các thành phố khác nhau
- Bao gồm: Saigon, Hanoi, Da Nang, Can Tho, Nha Trang

### 3. Players (PlayerSeed.cs)
- 10 player profiles được tạo từ các users có role "Player"
- Mỗi player có skill level từ 5-7
- Có đầy đủ thông tin: FullName, Slug, Nickname, Email, Phone, Country, City

### 4. PayoutTemplates (PayoutTemplateSeed.cs)
5 mẫu chia giải:
- **Top 2 places** (4-8 players): 70%-30%
- **Top 3 places** (9-16 players): 50%-30%-20%
- **Top 4 places** (17-24 players): 45%-25%-18%-12%
- **Top 5 places** (25-32 players): 40%-25%-15%-12%-8%
- **Top 8 places** (33-64 players): 35%-20%-12%-10%-8%-6%-5%-4%

### 5. Posts (PostSeed.cs)
- 10 bài đăng mẫu về billiard
- Được tạo bởi các users khác nhau
- Có nội dung liên quan đến giải đấu, tips, thông báo

## ⚙️ Thứ tự phụ thuộc

**QUAN TRỌNG:** Phải seed theo thứ tự sau vì có dependencies:

```
1. Users (UserSeed)           ← Phải đầu tiên
   ↓
2. Venues (VenueSeed)         ← Cần Users
   ↓
3. Players (PlayerSeed)       ← Cần Users
   ↓
4. PayoutTemplates            ← Độc lập
   ↓
5. Posts (PostSeed)           ← Cần Users
```

## 🔧 Fix lỗi

Nếu có lỗi khi seed:

### Lỗi: "User not found"
**Nguyên nhân:** Chưa seed Users trước
**Giải pháp:** Chạy `POST /api/seed/users` trước

### Lỗi: "Duplicate key"
**Nguyên nhân:** Dữ liệu đã tồn tại
**Giải pháp:** Các seed đã có check `AnyAsync()`, nếu vẫn lỗi thì xóa database và migrate lại:
```powershell
dotnet ef database drop --force
dotnet ef database update
```

### Lỗi trong một file seed cụ thể
**Ưu điểm của cấu trúc tách file:**
- Dễ tìm lỗi: Mở file seed tương ứng (VD: `VenueSeed.cs`)
- Fix nhanh: Chỉ sửa file đó, không ảnh hưởng file khác
- Test riêng: Gọi endpoint riêng để test (VD: `POST /api/seed/venues`)

## 🚀 Testing

### Test từng bước:
```bash
# 1. Seed users
POST /api/seed/users

# 2. Kiểm tra login với user vừa tạo
POST /api/auth/login
{
  "email": "admin@poolmate.com",
  "password": "Admin@123456"
}

# 3. Seed venues
POST /api/seed/venues

# 4. Kiểm tra venues
GET /api/venues

# 5. Tiếp tục với các bảng khác...
```

### Test tất cả cùng lúc:
```bash
POST /api/seed/all
```

## 📝 Lưu ý

1. ✅ **Idempotent** - Có thể chạy nhiều lần, nếu dữ liệu đã có sẽ bỏ qua
2. ✅ **EmailConfirmed = true** - Users có thể đăng nhập ngay
3. ✅ **Chỉ Development** - Được bảo vệ bởi environment check
4. ✅ **Tách biệt** - Mỗi model có file seed riêng
5. ⚠️ **Không Production** - Đây là dữ liệu test

## 🔄 Mở rộng thêm

Để thêm seed cho model mới (VD: Tournament):

1. **Tạo file mới:** `Data/Seeds/TournamentSeed.cs`
```csharp
public static class TournamentSeed
{
    public static async Task SeedAsync(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
    {
        if (await context.Tournaments.AnyAsync())
            return;
            
        // Seed logic here...
    }
}
```

2. **Thêm vào SeedData.cs:**
```csharp
public static async Task SeedAllDataAsync(...)
{
    // ...existing seeds...
    await TournamentSeed.SeedAsync(context, userManager);
}
```

3. **Thêm endpoint vào SeedController.cs:**
```csharp
[HttpPost("tournaments")]
public async Task<IActionResult> SeedTournaments()
{
    await SeedData.SeedTournamentsOnlyAsync(_serviceProvider);
    return Ok(...);
}
```

## 🎉 Kết quả

Sau khi seed thành công, bạn có:
- ✅ 13 users với 3 roles khác nhau
- ✅ 5 venues ở các thành phố
- ✅ 10 player profiles
- ✅ 5 payout templates
- ✅ 10 posts

Sẵn sàng để test toàn bộ ứng dụng! 🚀

