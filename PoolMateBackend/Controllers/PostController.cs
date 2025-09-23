using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PoolMate.Api.Dtos.Post;
using PoolMate.Api.Services;
using System.Security.Claims;

namespace PoolMate.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class PostController : ControllerBase
    {
        private readonly IPostService _posts;

        public PostController(IPostService posts)
        {
            _posts = posts;
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreatePostModel model, CancellationToken ct)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
            var postId = await _posts.CreatePostAsync(userId, model, ct);
            if (postId == null)
                return BadRequest(new { message = "Failed to create post" });
            return Ok(new { id = postId, message = "Post created successfully" });
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Update(Guid id, [FromBody] UpdatePostModel model, CancellationToken ct)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
            var ok = await _posts.UpdatePostAsync(id, userId, model, ct);
            return ok ? Ok(new { message = "Post updated successfully" }) : NotFound(new { message = "Post not found or not owned by user" });
        }

        [HttpGet("my-posts")]
        public async Task<IActionResult> GetMyPosts(CancellationToken ct)
        {
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
            var posts = await _posts.GetPostsAsync(currentUserId, currentUserId, ct);
            return Ok(posts);
        }

        [HttpGet("user/{targetUserId}")]
        public async Task<IActionResult> GetPostsByUser(string targetUserId, CancellationToken ct)
        {
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
            var posts = await _posts.GetPostsAsync(targetUserId, currentUserId, ct);
            return Ok(posts);
        }


        [HttpPatch("{id}/toggle-visibility")]
        public async Task<IActionResult> ToggleVisibility(Guid id, CancellationToken ct)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
            var ok = await _posts.TogglePostVisibilityAsync(id, userId, ct);
            return ok ? Ok(new { message = "Post visibility toggled successfully" })
                      : NotFound(new { message = "Post not found" });
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> HardDelete(Guid id, CancellationToken ct)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
            var ok = await _posts.HardDeletePostAsync(id, userId, ct);
            return ok ? Ok(new { message = "Post deleted permanently" })
                      : NotFound(new { message = "Post not found" });
        }



    }
}
