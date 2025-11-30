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

        /// <summary>
        /// Seed Users và Roles vào database
        /// </summary>
        [HttpPost("users")]
        public async Task<IActionResult> SeedUsers()
        {
            if (!_env.IsDevelopment())
            {
                return BadRequest(ApiResponse<object>.Fail(400, "Seeding is only allowed in development environment"));
            }

            try
            {
                await SeedData.SeedUsersAsync(_serviceProvider);
                var response = ApiResponse<string>.Ok("Success", "✅ User seed data created successfully. Check SEED_DATA_README.md for credentials.");
                return Ok(response);
            }
            catch (Exception ex)
            {
                return StatusCode(500, ApiResponse<object>.Fail(500, $"❌ Error seeding users: {ex.Message}"));
            }
        }

        /// <summary>
        /// Seed Venues vào database
        /// </summary>
        [HttpPost("venues")]
        public async Task<IActionResult> SeedVenues()
        {
            if (!_env.IsDevelopment())
            {
                return BadRequest(ApiResponse<object>.Fail(400, "Seeding is only allowed in development environment"));
            }

            try
            {
                await SeedData.SeedVenuesOnlyAsync(_serviceProvider);
                var response = ApiResponse<string>.Ok("Success", "✅ Venue seed data created successfully");
                return Ok(response);
            }
            catch (Exception ex)
            {
                return StatusCode(500, ApiResponse<object>.Fail(500, $"❌ Error seeding venues: {ex.Message}"));
            }
        }

        /// <summary>
        /// Seed Players vào database
        /// </summary>
        [HttpPost("players")]
        public async Task<IActionResult> SeedPlayers()
        {
            if (!_env.IsDevelopment())
            {
                return BadRequest(ApiResponse<object>.Fail(400, "Seeding is only allowed in development environment"));
            }

            try
            {
                await SeedData.SeedPlayersOnlyAsync(_serviceProvider);
                var response = ApiResponse<string>.Ok("Success", "✅ Player seed data created successfully");
                return Ok(response);
            }
            catch (Exception ex)
            {
                return StatusCode(500, ApiResponse<object>.Fail(500, $"❌ Error seeding players: {ex.Message}"));
            }
        }

        /// <summary>
        /// Seed PayoutTemplates vào database
        /// </summary>
        [HttpPost("payout-templates")]
        public async Task<IActionResult> SeedPayoutTemplates()
        {
            if (!_env.IsDevelopment())
            {
                return BadRequest(ApiResponse<object>.Fail(400, "Seeding is only allowed in development environment"));
            }

            try
            {
                await SeedData.SeedPayoutTemplatesOnlyAsync(_serviceProvider);
                var response = ApiResponse<string>.Ok("Success", "✅ PayoutTemplate seed data created successfully");
                return Ok(response);
            }
            catch (Exception ex)
            {
                return StatusCode(500, ApiResponse<object>.Fail(500, $"❌ Error seeding payout templates: {ex.Message}"));
            }
        }

        /// <summary>
        /// Seed Posts vào database
        /// </summary>
        [HttpPost("posts")]
        public async Task<IActionResult> SeedPosts()
        {
            if (!_env.IsDevelopment())
            {
                return BadRequest(ApiResponse<object>.Fail(400, "Seeding is only allowed in development environment"));
            }

            try
            {
                await SeedData.SeedPostsOnlyAsync(_serviceProvider);
                var response = ApiResponse<string>.Ok("Success", "✅ Post seed data created successfully");
                return Ok(response);
            }
            catch (Exception ex)
            {
                return StatusCode(500, ApiResponse<object>.Fail(500, $"❌ Error seeding posts: {ex.Message}"));
            }
        }

        /// <summary>
        /// Seed Tournaments vào database
        /// </summary>
        [HttpPost("tournaments")]
        public async Task<IActionResult> SeedTournaments()
        {
            if (!_env.IsDevelopment())
            {
                return BadRequest(ApiResponse<object>.Fail(400, "Seeding is only allowed in development environment"));
            }

            try
            {
                await SeedData.SeedTournamentsOnlyAsync(_serviceProvider);
                var response = ApiResponse<string>.Ok("Success", "✅ Tournament seed data created successfully");
                return Ok(response);
            }
            catch (Exception ex)
            {
                return StatusCode(500, ApiResponse<object>.Fail(500, $"❌ Error seeding tournaments: {ex.Message}"));
            }
        }

        /// <summary>
        /// Seed TournamentTables vào database
        /// </summary>
        [HttpPost("tournament-tables")]
        public async Task<IActionResult> SeedTournamentTables()
        {
            if (!_env.IsDevelopment())
            {
                return BadRequest(ApiResponse<object>.Fail(400, "Seeding is only allowed in development environment"));
            }

            try
            {
                await SeedData.SeedTournamentTablesOnlyAsync(_serviceProvider);
                var response = ApiResponse<string>.Ok("Success", "✅ TournamentTable seed data created successfully");
                return Ok(response);
            }
            catch (Exception ex)
            {
                return StatusCode(500, ApiResponse<object>.Fail(500, $"❌ Error seeding tournament tables: {ex.Message}"));
            }
        }

        /// <summary>
        /// Seed TournamentPlayers vào database
        /// </summary>
        [HttpPost("tournament-players")]
        public async Task<IActionResult> SeedTournamentPlayers()
        {
            if (!_env.IsDevelopment())
            {
                return BadRequest(ApiResponse<object>.Fail(400, "Seeding is only allowed in development environment"));
            }

            try
            {
                await SeedData.SeedTournamentPlayersOnlyAsync(_serviceProvider);
                var response = ApiResponse<string>.Ok("Success", "✅ TournamentPlayer seed data created successfully");
                return Ok(response);
            }
            catch (Exception ex)
            {
                return StatusCode(500, ApiResponse<object>.Fail(500, $"❌ Error seeding tournament players: {ex.Message}"));
            }
        }

        /// <summary>
        /// Seed TournamentStages vào database
        /// </summary>
        [HttpPost("tournament-stages")]
        public async Task<IActionResult> SeedTournamentStages()
        {
            if (!_env.IsDevelopment())
            {
                return BadRequest(ApiResponse<object>.Fail(400, "Seeding is only allowed in development environment"));
            }

            try
            {
                await SeedData.SeedTournamentStagesOnlyAsync(_serviceProvider);
                var response = ApiResponse<string>.Ok("Success", "✅ TournamentStage seed data created successfully");
                return Ok(response);
            }
            catch (Exception ex)
            {
                return StatusCode(500, ApiResponse<object>.Fail(500, $"❌ Error seeding tournament stages: {ex.Message}"));
            }
        }

        /// <summary>
        /// Seed Matches vào database
        /// </summary>
        [HttpPost("matches")]
        public async Task<IActionResult> SeedMatches()
        {
            if (!_env.IsDevelopment())
            {
                return BadRequest(ApiResponse<object>.Fail(400, "Seeding is only allowed in development environment"));
            }

            try
            {
                await SeedData.SeedMatchesOnlyAsync(_serviceProvider);
                var response = ApiResponse<string>.Ok("Success", "✅ Match seed data created successfully");
                return Ok(response);
            }
            catch (Exception ex)
            {
                return StatusCode(500, ApiResponse<object>.Fail(500, $"❌ Error seeding matches: {ex.Message}"));
            }
        }

        /// <summary>
        /// Seed tất cả dữ liệu vào database
        /// </summary>
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
                var response = ApiResponse<string>.Ok("Success", "✅ All seed data created successfully");
                return Ok(response);
            }
            catch (Exception ex)
            {
                return StatusCode(500, ApiResponse<object>.Fail(500, $"❌ Error seeding data: {ex.Message}"));
            }
        }
    }
}

