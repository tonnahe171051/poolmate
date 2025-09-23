using Microsoft.EntityFrameworkCore;
using PoolMate.Api.Data;
using PoolMate.Api.Dtos.Post;
using PoolMate.Api.Integrations.Cloudinary36;
using PoolMate.Api.Models;

namespace PoolMate.Api.Services
{
    public class PostService : IPostService
    {
        private readonly ApplicationDbContext _db;
        private readonly ICloudinaryService _cloud;

        public PostService(ApplicationDbContext db, ICloudinaryService cloud)
        {
            _db = db;
            _cloud = cloud;
        }

        public async Task<Guid?> CreatePostAsync(string userId, CreatePostModel model, CancellationToken ct)
        {
            var post = new Post
            {
                Id = Guid.NewGuid(),
                Content = model.Content,
                ImageUrl = model.ImageUrl,
                ImagePublicId = model.ImagePublicId,
                UserId = userId,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                IsActive = true
            };
            _db.Posts.Add(post);
            await _db.SaveChangesAsync(ct);
            return post.Id;
        }

        public async Task<bool> UpdatePostAsync(Guid postId, string userId, UpdatePostModel model, CancellationToken ct)
        {
            var post = await _db.Posts.FirstOrDefaultAsync(x => x.Id == postId && x.UserId == userId, ct);
            if (post == null) return false;

            if (model.Content != null)
                post.Content = model.Content;
            if (model.ImageUrl != null)
                post.ImageUrl = model.ImageUrl;
            if (model.ImagePublicId != null)
                post.ImagePublicId = model.ImagePublicId;

            post.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);
            return true;
        }

        public async Task<List<PostResponseModel>> GetPostsAsync(string targetUserId, string? currentUserId = null, CancellationToken ct = default)
        {
            var query = _db.Posts
                .Include(p => p.User)
                .Where(p => p.UserId == targetUserId);

            if (targetUserId != currentUserId)
            {
                query = query.Where(p => p.IsActive); // Chỉ posts public
            }

            var posts = await query
                .OrderByDescending(p => p.CreatedAt)
                .Select(p => new PostResponseModel
                {
                    Id = p.Id,
                    Content = p.Content,
                    ImageUrl = p.ImageUrl,
                    ImagePublicId = p.ImagePublicId,
                    UserId = p.UserId,
                    UserName = p.User.UserName ?? "",
                    UserNickname = p.User.Nickname,
                    UserAvatar = p.User.ProfilePicture,
                    CreatedAt = p.CreatedAt,
                    UpdatedAt = p.UpdatedAt,
                    IsActive = p.IsActive
                })
                .ToListAsync(ct);

            return posts;
        }

        public async Task<bool> TogglePostVisibilityAsync(Guid postId, string userId, CancellationToken ct)
        {
            var post = await _db.Posts.FirstOrDefaultAsync(x => x.Id == postId && x.UserId == userId, ct);
            if (post == null) return false;

            // Toggle IsActive
            post.IsActive = !post.IsActive;
            post.UpdatedAt = DateTime.UtcNow;

            await _db.SaveChangesAsync(ct);
            return true;
        }

        public async Task<bool> HardDeletePostAsync(Guid postId, string userId, CancellationToken ct)
        {
            var post = await _db.Posts.FirstOrDefaultAsync(x => x.Id == postId && x.UserId == userId, ct);
            if (post == null) return false;

            // delete image from cloudinary
            if (!string.IsNullOrEmpty(post.ImagePublicId))
            {
                await _cloud.DeleteAsync(post.ImagePublicId);
            }

            _db.Posts.Remove(post);
            await _db.SaveChangesAsync(ct);
            return true;
        }






    }
}
