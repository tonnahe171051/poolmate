using System.ComponentModel.DataAnnotations;

namespace PoolMate.Api.Models
{
    public class PayoutTemplate
    {
        public int Id { get; set; }

        [Required, MaxLength(200)] public string Name { get; set; } = default!; // "Top 4 places (17–24 players)"

        public int MinPlayers { get; set; } // 17
        public int MaxPlayers { get; set; } // 24
        public int Places { get; set; } // 4 (số hạng có thưởng)

        // JSON chứa tỉ lệ phần trăm cho từng hạng: [{ "rank":1, "percent":60 }, ...]
        [Required] public string PercentJson { get; set; } = default!;

        [Required] public string OwnerUserId { get; set; } = default!;
        public ApplicationUser? OwnerUser { get; set; }
    }
}