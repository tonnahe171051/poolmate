namespace PoolMate.Api.Models
{

    public enum PlayerType { Singles, Doubles }
    public enum BracketType { SingleElimination, DoubleElimination }
    public enum GameType { EightBall, NineBall, TenBall }
    public enum BracketOrdering { Random, Seeded }

    // filter tournament
    public enum TournamentStatus { Upcoming, InProgress, Completed }

    // break format
    public enum BreakFormat { WinnerBreak, AlternateBreak, Other }

    // Payout mode (co cau giai thuong)
    public enum PayoutMode { Template, Custom }
    //rules
    public enum Rule { WNT, WPA }

    public enum TournamentPlayerStatus { Unconfirmed, Confirmed }

    public enum TableStatus { Open, InUse, Closed }

    public enum StageStatus { NotStarted, InProgress, Completed}

    public enum BracketSide { Winners, Losers, Finals, Knockout}

    public enum MatchStatus { NotStarted, InProgress, Completed}


}
