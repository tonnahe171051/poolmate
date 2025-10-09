using System.ComponentModel.DataAnnotations;
using PoolMate.Api.Models;

namespace PoolMate.Api.Dtos.Tournament
{
    public class AddTournamentTableModel
    {
        [Required, MaxLength(50)]
        public string Label { get; set; } = default!;

        [MaxLength(50)]
        public string? Manufacturer { get; set; }

        public decimal SizeFoot { get; set; } = 9.0m;

        public string? LiveStreamUrl { get; set; }
    }
}
