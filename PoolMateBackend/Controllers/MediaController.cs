using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using PoolMate.Api.Integrations.Cloudinary36;
using System.Security.Claims;

namespace PoolMate.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class MediaController : ControllerBase
    {
        private readonly ICloudinaryService _cloud;

        public MediaController(ICloudinaryService cloud) => _cloud = cloud;

        [HttpPost("sign-upload")]
        public IActionResult SignUpload()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
            var dto = _cloud.SignAvatarUpload(userId);
            return Ok(dto);
        }

        [HttpPost("sign-post-image-upload/{postId}")]
        public IActionResult SignPostImageUpload(string postId)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
            var dto = _cloud.SignPostImageUpload(userId, postId);
            return Ok(dto);
        }

        [HttpPost("sign-flyer-upload/{flyerId}")]
        public IActionResult SignFlyerUpload(string flyerId)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
            var dto = _cloud.SignFlyerUpload(userId, flyerId);
            return Ok(dto);
        }
    }
}
