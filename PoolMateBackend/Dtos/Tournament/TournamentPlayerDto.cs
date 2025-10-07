namespace PoolMate.Api.Dtos.Tournament
{
    public class TournamentPlayerDto
    {
        public int Id { get; set; }
        public int TournamentId { get; set; }
        public int PlayerId { get; set; }
        public string FullName { get; set; } = default!;
        public string? Nickname { get; set; }
        public int? Seed { get; set; }
        public string Status { get; set; } = default!;
    }
}
