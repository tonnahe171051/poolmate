using System.ComponentModel.DataAnnotations;
using PoolMate.Api.Models;

namespace PoolMate.Api.Dtos.Tournament
{
    public class UpdateTournamentTableModel
    {
        [MaxLength(50)]
        public string? Label { get; set; }

        [MaxLength(50)]
        public string? Manufacturer { get; set; }

        public decimal? SizeFoot { get; set; }

        public TableStatus? Status { get; set; }

        public bool? IsStreaming { get; set; }

        public string? LiveStreamUrl { get; set; }
    }
}
