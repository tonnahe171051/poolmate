using System.ComponentModel.DataAnnotations;
using PoolMate.Api.Models;


namespace PoolMate.Api.Dtos.Tournament;

public class CreateTournamentModel
{
    [Required, MaxLength(200)]
    public string Name { get; set; } = default!;

    // Thời gian dự kiến
    public DateTime StartUtc { get; set; } = DateTime.UtcNow;
    public DateTime? EndUtc { get; set; }

    // Venue (nullable)
    public int? VenueId { get; set; }

    // Hiển thị & đăng ký
    public bool IsPublic { get; set; } = false;
    public bool OnlineRegistrationEnabled { get; set; } = false;
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
    public PayoutMode? PayoutMode { get; set; }      // default Template nếu null
    public int? PayoutTemplateId { get; set; }       // optional (để sau nếu cần breakdown)

    // Chỉ có nghĩa khi Custom
    public decimal? TotalPrize { get; set; }
}
