namespace PoolMate.Api.Dtos.Tournament
{
    public class LinkPlayerRequest
    {
        public int PlayerId { get; set; }
        public bool OverwriteSnapshot { get; set; } = true;
    }
    public class CreateProfileFromSnapshotRequest
    {
        public bool CopyBackToSnapshot { get; set; } = true;
    }
    public class PlayerSearchItemDto
    {
        public int Id { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string? Email { get; set; }
        public string? Phone { get; set; }
        public string? Country { get; set; }
        public string? City { get; set; }
        public int? SkillLevel { get; set; }
    }
}
