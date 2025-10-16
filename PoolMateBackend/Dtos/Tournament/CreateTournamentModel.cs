using System.ComponentModel.DataAnnotations;
using PoolMate.Api.Models;

namespace PoolMate.Api.Dtos.Tournament;

public class CreateTournamentModel
{
    [Required, MaxLength(200)]
    public string Name { get; set; } = default!;

    public DateTime StartUtc { get; set; } = DateTime.UtcNow;
    public DateTime? EndUtc { get; set; }

    // Venue (nullable)
    public int? VenueId { get; set; }

    public bool IsPublic { get; set; } = false;
    public bool OnlineRegistrationEnabled { get; set; } = false;
    [Required]
    [Range(2, 256, ErrorMessage = "Bracket size must be between 2 and 256 players")]
    public int? BracketSizeEstimate { get; set; }

    // Settings
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
    public PayoutMode? PayoutMode { get; set; }      
    public int? PayoutTemplateId { get; set; }       

    // for Custom
    public decimal? TotalPrize { get; set; }

    //multi stage
    public bool? IsMultiStage { get; set; } = false;
    public int? AdvanceToStage2Count { get; set; }
    public BracketType? Stage1Type { get; set; }
    public BracketOrdering? Stage1Ordering { get; set; }
    public BracketOrdering? Stage2Ordering { get; set; }
}
