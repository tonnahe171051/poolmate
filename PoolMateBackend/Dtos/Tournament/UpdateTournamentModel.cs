
using PoolMate.Api.Models;
using System.ComponentModel.DataAnnotations;

namespace PoolMate.Api.Dtos.Tournament
{
    public class UpdateTournamentModel
    {
        public string? Name { get; set; }
        public string? Description { get; set; }
        public DateTime? StartUtc { get; set; }
        public DateTime? EndUtc { get; set; }
        public int? VenueId { get; set; }
        public bool? IsPublic { get; set; }
        public bool? OnlineRegistrationEnabled { get; set; }
        [Required]
        [Range(2, 256, ErrorMessage = "Bracket size must be between 2 and 256 players")]
        public int? BracketSizeEstimate { get; set; }
        public PlayerType? PlayerType { get; set; }
        public BracketType? BracketType { get; set; }
        public GameType? GameType { get; set; }
        public BracketOrdering? BracketOrdering { get; set; }
        public int? WinnersRaceTo { get; set; }
        public int? LosersRaceTo { get; set; }
        public int? FinalsRaceTo { get; set; }
        public Rule? Rule { get; set; }
        public BreakFormat? BreakFormat { get; set; }
        // Fee
        public decimal? EntryFee { get; set; }
        public decimal? AdminFee { get; set; }
        public decimal? AddedMoney { get; set; }

        // Payout mode
        public PayoutMode? PayoutMode { get; set; }      // default Template nếu null
        public int? PayoutTemplateId { get; set; }       // optional (để sau nếu cần breakdown)

        // Chỉ có nghĩa khi Custom
        public decimal? TotalPrize { get; set; }
    }
}
