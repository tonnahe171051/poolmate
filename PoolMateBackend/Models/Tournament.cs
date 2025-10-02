using System.ComponentModel.DataAnnotations;

namespace PoolMate.Api.Models
{
    public class Tournament
    {
        public int Id { get; set; }

        [Required, MaxLength(200)]
        public string Name { get; set; } = default!;

        [MaxLength(1000)]
        public string? Description { get; set; }

        public DateTime StartUtc { get; set; }
        public DateTime? EndUtc { get; set; }

        // Venue
        public int? VenueId { get; set; }
        public Venue? Venue { get; set; }

        // Owner
        [Required] public string OwnerUserId { get; set; } = default!;
        public ApplicationUser? OwnerUser { get; set; }

        // Settings cơ bản
        public PlayerType PlayerType { get; set; } = PlayerType.Singles;
        public BracketType BracketType { get; set; } = BracketType.DoubleElimination;
        public GameType GameType { get; set; } = GameType.NineBall;

        public int? WinnersRaceTo { get; set; }
        public int? LosersRaceTo { get; set; }
        public int? FinalsRaceTo { get; set; }

        public BracketOrdering BracketOrdering { get; set; } = BracketOrdering.Random;

        // Đăng ký & hiển thị
        public bool OnlineRegistrationEnabled { get; set; } = false;
        public bool IsPublic { get; set; } = false;
        public int? BracketSizeEstimate { get; set; } // quy mo danh sach dang ky

        // Flyer (Cloudinary)
        [MaxLength(300)]
        public string? FlyerUrl { get; set; }
        [MaxLength(120)]
        public string? FlyerPublicId { get; set; }

        // Payout
        public decimal? EntryFee { get; set; }     // per player
        public decimal? AdminFee { get; set; }     // per player
        public decimal? AddedMoney { get; set; }   // sponsor money

        public PayoutMode? PayoutMode { get; set; }          // Template | Custom
        public int? PayoutTemplateId { get; set; }               // khi PayoutMode = Template
        public PayoutTemplate? PayoutTemplate { get; set; }
        public decimal? TotalPrize { get; set; }             // use when Custom

        // Luật & break
        public Rule Rule { get; set; } = Rule.WNT;   // WNT, WPA, ...
        public BreakFormat? BreakFormat { get; set; }

        // Runtime state cho đăng ký/lọc
        public bool IsStarted { get; set; } = false;
        public TournamentStatus Status { get; set; } = TournamentStatus.Upcoming;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        //// Navigations khác (để bạn thêm dần)
        //public ICollection<TournamentPlayer> TournamentPlayers { get; set; } = new();
        //public ICollection<TournamentTable> Tables { get; set; } = new();
        //public ICollection<Match> Matches { get; set; } = new();
    }
}
