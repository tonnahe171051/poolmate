using PoolMate.Api.Models;

namespace PoolMate.Api.Dtos.Tournament
{
    public class BracketFilterRequest
    {
        public BracketFilterType FilterType { get; set; } = BracketFilterType.ShowAll;
    }

    public enum BracketFilterType
    {
        ShowAll,
        DrawRound,      // Round 1 only
        WinnerSide,     // Winners bracket only  
        LoserSide,      // Losers bracket only
        Finals,         // Finals only
    }
}
