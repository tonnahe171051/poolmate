
using PoolMate.Api.Models;

namespace PoolMate.Api.Dtos.Tournament
{
    public class TournamentDetailDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = default!;
        public string? Description { get; set; }
        public DateTime StartUtc { get; set; }
        public DateTime? EndUtc { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        
        // Tournament settings
        public bool IsPublic { get; set; }
        public bool OnlineRegistrationEnabled { get; set; }
        public bool IsStarted { get; set; }
        public TournamentStatus Status { get; set; }
        
        // Game settings
        public PlayerType PlayerType { get; set; }
        public BracketType BracketType { get; set; }
        public GameType GameType { get; set; }
        public BracketOrdering BracketOrdering { get; set; }
        public int? BracketSizeEstimate { get; set; }
        public int? WinnersRaceTo { get; set; }
        public int? LosersRaceTo { get; set; }
        public int? FinalsRaceTo { get; set; }
        public Rule Rule { get; set; }
        public BreakFormat? BreakFormat { get; set; }
        
        public decimal? EntryFee { get; set; }
        public decimal? AdminFee { get; set; }
        public decimal? AddedMoney { get; set; }
        public PayoutMode? PayoutMode { get; set; }
        public int? PayoutTemplateId { get; set; }
        public decimal? TotalPrize { get; set; }
        
        public string? FlyerUrl { get; set; }
        
        public string CreatorName { get; set; } = default!;
        public VenueDto? Venue { get; set; }
        
        // Counts
        public int TotalPlayers { get; set; }
        public int TotalTables { get; set; }
    }
}
