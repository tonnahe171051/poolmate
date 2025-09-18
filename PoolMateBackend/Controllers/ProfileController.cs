using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using PoolMate.Api.Dtos.UserProfile;
using PoolMate.Api.Services;
using System.Security.Claims;
using System.Text.RegularExpressions;

namespace PoolMate.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ProfileController : ControllerBase
    {
        private readonly IProfileService _profiles;

        public ProfileController(IProfileService profiles) => _profiles = profiles;

        [HttpGet("me")]
        public async Task<IActionResult> Me(CancellationToken ct)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
            var res = await _profiles.MeAsync(userId, ct);
            return res.Status == "Success" ? Ok(res) : BadRequest(res);
        }

        [HttpPut]
        public async Task<IActionResult> Update([FromBody] UpdateProfileModel model, CancellationToken ct)
        {
            //validate for phone number, boi vi model binding bien model.Phone thanh string.Empty
            if (model.Phone != null && model.Phone != "")
            {
                if (model.Phone.Trim().Length == 0)
                    return BadRequest(new { message = "Phone number cannot be only whitespace." });

                var phone = model.Phone.Trim();
                if (!Regex.IsMatch(phone, @"^\+?\d{10,15}$"))
                    return BadRequest(new { message = "Invalid phone number. Must be 10-15 digits, optional leading '+'." });

                model.Phone = phone;
            }
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
            var res = await _profiles.UpdateAsync(userId, model, ct);
            return res.Status == "Success" ? Ok(res) : BadRequest(res);
        }
    }
}
