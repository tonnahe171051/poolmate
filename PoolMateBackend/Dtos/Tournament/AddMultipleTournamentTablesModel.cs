using System.ComponentModel.DataAnnotations;
using PoolMate.Api.Models;

namespace PoolMate.Api.Dtos.Tournament
{
    public class AddMultipleTournamentTablesModel
    {
        [Required]
        [Range(1, 999, ErrorMessage = "Start number must be between 1 and 999")]
        public int StartNumber { get; set; }

        [Required]
        [Range(1, 999, ErrorMessage = "End number must be between 1 and 999")]
        public int EndNumber { get; set; }

        [MaxLength(50)]
        public string? Manufacturer { get; set; }

        public decimal SizeFoot { get; set; } = 9.0m;
    }
}
