# Seed Data - Hướng dẫn sử dụng

## Tổng quan
File `SeedData.cs` chứa dữ liệu mẫu để khởi tạo database với các user, role và dữ liệu test ban đầu.

## Cách sử dụng

### Cách 1: Gọi trong Program.cs (Khuyến nghị cho Development)

Thêm đoạn code sau vào `Program.cs`, ngay trước dòng `app.Run()`:

```csharp
// Seed data (chỉ trong development)
if (app.Environment.IsDevelopment())
{
    using (var scope = app.Services.CreateScope())
    {
        var services = scope.ServiceProvider;
        try
        {
            await SeedData.SeedUsersAsync(services);
            Console.WriteLine("✅ Seed data completed successfully!");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ An error occurred while seeding data: {ex.Message}");
        }
    }
}
```

### Cách 2: Tạo endpoint API để seed (Khuyến nghị cho kiểm soát thủ công)

Tạo một controller mới `SeedController.cs`:

```csharp
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PoolMate.Api.Data;
using PoolMate.Api.Dtos.Response;

namespace PoolMate.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class SeedController : ControllerBase
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly IWebHostEnvironment _env;

        public SeedController(IServiceProvider serviceProvider, IWebHostEnvironment env)
        {
            _serviceProvider = serviceProvider;
            _env = env;
        }

        [HttpPost("users")]
        public async Task<IActionResult> SeedUsers()
        {
            // Chỉ cho phép trong Development
            if (!_env.IsDevelopment())
            {
                return BadRequest(ApiResponse<object>.Fail(400, "Seeding is only allowed in development environment"));
            }

            try
            {
                await SeedData.SeedUsersAsync(_serviceProvider);
                return Ok(ApiResponse<object>.Success(null, "User seed data created successfully"));
            }
            catch (Exception ex)
            {
                return StatusCode(500, ApiResponse<object>.Fail(500, $"Error seeding users: {ex.Message}"));
            }
        }

        [HttpPost("all")]
        public async Task<IActionResult> SeedAll()
        {
            if (!_env.IsDevelopment())
            {
                return BadRequest(ApiResponse<object>.Fail(400, "Seeding is only allowed in development environment"));
            }

            try
            {
                await SeedData.SeedAllDataAsync(_serviceProvider);
                return Ok(ApiResponse<object>.Success(null, "All seed data created successfully"));
            }
            catch (Exception ex)
            {
                return StatusCode(500, ApiResponse<object>.Fail(500, $"Error seeding data: {ex.Message}"));
            }
        }
    }
}
```

Sau đó gọi API: `POST http://localhost:5000/api/seed/users`

## Dữ liệu được seed

### 1. Roles
- **Admin**: Quản trị viên hệ thống
- **Organizer**: Người tổ chức giải đấu
- **Player**: Người chơi

### 2. Users

#### Admin User
- Email: `admin@poolmate.com`
- Password: `Admin@123456`
- Roles: Admin
- Tên: Admin System

#### Organizer Users
1. **John Organizer**
   - Email: `john.organizer@poolmate.com`
   - Password: `Organizer@123`
   - Roles: Organizer, Player
   - Thành phố: Hanoi

2. **Sarah Events**
   - Email: `sarah.events@poolmate.com`
   - Password: `Organizer@123`
   - Roles: Organizer, Player
   - Thành phố: Da Nang

#### Player Users (10 users)
- Email format: `{name}.{lastname}@poolmate.com`
- Password tất cả: `Player@123`
- Role: Player
- Danh sách:
  1. Mike Johnson (mike.player@poolmate.com)
  2. Emily Chen (emily.pool@poolmate.com)
  3. David Williams (david.shark@poolmate.com)
  4. Lisa Martinez (lisa.nine@poolmate.com)
  5. Robert Taylor (robert.eight@poolmate.com)
  6. Jennifer Anderson (jennifer.cue@poolmate.com)
  7. James Thomas (james.pool@poolmate.com)
  8. Mary Jackson (mary.break@poolmate.com)
  9. Michael White (michael.rack@poolmate.com)
  10. Patricia Harris (patricia.ball@poolmate.com)

## Lưu ý

1. **Seed data chỉ nên chạy một lần** - Nếu user đã tồn tại, code sẽ bỏ qua không tạo lại
2. **Chỉ sử dụng trong môi trường Development** - Không nên seed trong Production
3. **Password mặc định** - Nên đổi password sau khi đăng nhập lần đầu
4. Các user được tạo đã có `EmailConfirmed = true` để có thể đăng nhập ngay

## Mở rộng

Để thêm seed data cho các bảng khác (Player, Venue, Tournament, etc.), uncomment và implement các method tương ứng trong `SeedData.cs`:

```csharp
// await SeedPlayersAsync(context, userManager);
// await SeedVenuesAsync(context, userManager);
// await SeedPayoutTemplatesAsync(context);
// await SeedTournamentsAsync(context, userManager);
```

Tôi sẽ tạo các method này trong các file tiếp theo.

