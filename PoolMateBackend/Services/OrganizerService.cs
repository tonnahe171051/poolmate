using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using PoolMate.Api.Data;
using PoolMate.Api.Dtos.Auth;
using PoolMate.Api.Dtos.Organizer;
using PoolMate.Api.Models;

namespace PoolMate.Api.Services
{
    public class OrganizerService : IOrganizerService
    {
        private readonly ApplicationDbContext _db;
        private readonly UserManager<ApplicationUser> _userManager;

        public OrganizerService(ApplicationDbContext db, UserManager<ApplicationUser> userManager)
        {
            _db = db;
            _userManager = userManager;
        }

        public async Task<OrganizerDto> RegisterAsync(string userId, RegisterOrganizerRequest request, CancellationToken ct = default)
        {
            // Check if user exists
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
                throw new KeyNotFoundException("User not found.");

            // Check if user already has organizer profile
            var existingOrganizer = await _db.Organizers
                .FirstOrDefaultAsync(o => o.UserId == userId, ct);
            
            if (existingOrganizer != null)
                throw new InvalidOperationException("User already has an organizer profile.");

            // Check if email is already registered by another organizer
            var emailExists = await _db.Organizers
                .AnyAsync(o => o.Email == request.Email, ct);
            
            if (emailExists)
                throw new InvalidOperationException("This email is already registered by another organizer.");

            // Create organizer profile
            var organizer = new Models.Organizer
            {
                UserId = userId,
                OrganizationName = request.OrganizationName,
                Email = request.Email,
                FacebookPageUrl = request.FacebookPageUrl,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _db.Organizers.Add(organizer);
            await _db.SaveChangesAsync(ct);

            // Add ORGANIZER role to user
            var hasOrganizerRole = await _userManager.IsInRoleAsync(user, UserRoles.ORGANIZER);
            if (!hasOrganizerRole)
            {
                var result = await _userManager.AddToRoleAsync(user, UserRoles.ORGANIZER);
                if (!result.Succeeded)
                    throw new InvalidOperationException("Failed to add ORGANIZER role to user.");
            }

            return new OrganizerDto
            {
                Id = organizer.Id,
                UserId = organizer.UserId,
                OrganizationName = organizer.OrganizationName,
                Email = organizer.Email,
                FacebookPageUrl = organizer.FacebookPageUrl,
                CreatedAt = organizer.CreatedAt,
                UpdatedAt = organizer.UpdatedAt
            };
        }

        public async Task<OrganizerDto?> GetByUserIdAsync(string userId, CancellationToken ct = default)
        {
            var organizer = await _db.Organizers
                .AsNoTracking()
                .FirstOrDefaultAsync(o => o.UserId == userId, ct);

            if (organizer == null)
                return null;

            return new OrganizerDto
            {
                Id = organizer.Id,
                UserId = organizer.UserId,
                OrganizationName = organizer.OrganizationName,
                Email = organizer.Email,
                FacebookPageUrl = organizer.FacebookPageUrl,
                CreatedAt = organizer.CreatedAt,
                UpdatedAt = organizer.UpdatedAt
            };
        }

        public async Task<bool> IsEmailRegisteredAsync(string email, CancellationToken ct = default)
        {
            return await _db.Organizers
                .AsNoTracking()
                .AnyAsync(o => o.Email == email, ct);
        }
    }
}
