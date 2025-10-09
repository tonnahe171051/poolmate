using PoolMate.Api.Models;

namespace PoolMate.Api.Dtos.Tournament
{
    public class TournamentTableDto
    {
        public int Id { get; set; }
        public string Label { get; set; } = default!;
        public string? Manufacturer { get; set; }
        public decimal SizeFoot { get; set; }
        public TableStatus Status { get; set; }
        public bool IsStreaming { get; set; }
        public string? LiveStreamUrl { get; set; }
    }
}
