using PoolMate.Api.Dtos.Post;

namespace PoolMate.Api.Services
{
    public interface IPostService
    {
        Task<Guid?> CreatePostAsync(string userId, CreatePostModel model, CancellationToken ct);
        Task<bool> UpdatePostAsync(Guid postId, string userId, UpdatePostModel model, CancellationToken ct);
        Task<List<PostResponseModel>> GetPostsAsync(string targetUserId, string? currentUserId = null, CancellationToken ct = default);

        Task<bool> TogglePostVisibilityAsync(Guid postId, string userId, CancellationToken ct);
        Task<bool> HardDeletePostAsync(Guid postId, string userId, CancellationToken ct);
    }



}
