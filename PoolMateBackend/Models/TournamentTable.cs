using Org.BouncyCastle.Utilities;
using System.ComponentModel.DataAnnotations;

namespace PoolMate.Api.Models
{
    public class TournamentTable
    {
        public int Id { get; set; }

        public int TournamentId { get; set; }
        public Tournament Tournament { get; set; } = default!;

        [Required, MaxLength(50)]
        public string Label { get; set; } = default!;

        [MaxLength(50)]
        public string? Manufacturer { get; set; }

        public decimal SizeFoot { get; set; } = 9.0m;

        public TableStatus Status { get; set; } = TableStatus.Open;

        public bool IsStreaming { get; set; } = false;
        [MaxLength(int.MaxValue)]
        public string? LiveStreamUrl { get; set; }
    }
}
