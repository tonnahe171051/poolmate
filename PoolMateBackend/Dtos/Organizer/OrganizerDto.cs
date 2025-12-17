namespace PoolMate.Api.Dtos.Organizer
{
    public class OrganizerDto
    {
        public int Id { get; set; }
        public string UserId { get; set; } = default!;
        public string OrganizationName { get; set; } = default!;
        public string Email { get; set; } = default!;
        public string? FacebookPageUrl { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}
