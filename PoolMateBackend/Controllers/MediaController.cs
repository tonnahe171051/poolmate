using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using PoolMate.Api.Integrations.Cloudinary36;
using System.Security.Claims;

namespace PoolMate.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
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
    }
}
