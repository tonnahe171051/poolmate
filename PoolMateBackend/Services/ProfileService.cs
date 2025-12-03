using Microsoft.AspNetCore.Identity;
using PoolMate.Api.Dtos.Auth;
using PoolMate.Api.Dtos.UserProfile;
using PoolMate.Api.Integrations.Cloudinary36;
using PoolMate.Api.Models;

namespace PoolMate.Api.Services
{
    public class ProfileService : IProfileService
    {
        private readonly UserManager<ApplicationUser> _users;
        private readonly ICloudinaryService _cloud;
        private readonly IPlayerProfileService _playerService;

        public ProfileService(
            UserManager<ApplicationUser> users, 
            ICloudinaryService cloud,
            IPlayerProfileService playerService)
        {
            _users = users;
            _cloud = cloud;
            _playerService = playerService;
        }

        public async Task<Response> MeAsync(string userId, CancellationToken ct)
        {
            var u = await _users.FindByIdAsync(userId);
            if (u is null) return Response.Error("User not found");

            var dto = new
            {
                u.Email,
                u.PhoneNumber,
                u.FirstName,
                u.LastName,
                u.Nickname,
                u.City,
                u.Country,
                avatarUrl = u.ProfilePicture,
                avatarPublicId = u.AvatarPublicId
            };

            return Response.Ok(dto);
        }

        public async Task<Response> UpdateAsync(string userId, UpdateProfileModel m, CancellationToken ct)
        {
            var u = await _users.FindByIdAsync(userId);
            if (u is null) return Response.Error("User not found");

            u.FirstName = m.FirstName ?? u.FirstName;
            u.LastName = m.LastName ?? u.LastName;
            u.Nickname = m.Nickname ?? u.Nickname;
            u.City = m.City ?? u.City;
            u.Country = m.Country ?? u.Country;
            u.PhoneNumber = m.Phone ?? u.PhoneNumber;

            // Nếu FE gửi metadata mới
            if (!string.IsNullOrWhiteSpace(m.AvatarPublicId) &&
                !string.IsNullOrWhiteSpace(m.AvatarUrl))
            {
                if (!string.IsNullOrEmpty(u.AvatarPublicId) &&
                    u.AvatarPublicId != m.AvatarPublicId)
                {
                    // dọn ảnh cũ
                    _ = await _cloud.DeleteAsync(u.AvatarPublicId);
                }

                u.AvatarPublicId = m.AvatarPublicId;
                u.ProfilePicture = m.AvatarUrl;
            }

            var res = await _users.UpdateAsync(u);
            if (!res.Succeeded) return Response.Error(string.Join("; ", res.Errors.Select(e => e.Description)));

            // 👇 Đồng bộ dữ liệu sang Player Profile sau khi update User thành công
            await _playerService.UpdatePlayerFromUserAsync(u, ct);

            return Response.Ok(new
            {
                u.FirstName,
                u.LastName,
                u.Nickname,
                u.City,
                u.Country,
                u.PhoneNumber,
                avatarUrl = u.ProfilePicture,
                avatarPublicId = u.AvatarPublicId
            });
        }

        public async Task<Response> GetUserProfileAsync(string targetUserId, CancellationToken ct)
        {
            var user = await _users.FindByIdAsync(targetUserId);
            if (user is null) return Response.Error("User not found");

            var dto = new
            {
                user.Id,
                user.FirstName,
                user.LastName,
                user.Nickname,
                user.City,
                user.Country,
                avatarUrl = user.ProfilePicture,
            };

            return Response.Ok(dto);
        }


    }
}
