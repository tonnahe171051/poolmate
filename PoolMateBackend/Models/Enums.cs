namespace PoolMate.Api.Models
{

    public enum PlayerType { Singles, Doubles }
    public enum BracketType { SingleElimination, DoubleElimination }
    public enum GameType { EightBall, NineBall, TenBall }
    public enum BracketOrdering { Random, Seeded, SetOrder }

    // filter tournament
    public enum TournamentStatus { Upcoming, InProgress, Completed }

    // break format
    public enum BreakFormat { WinnerBreak, AlternateBreak, Other }

    // Payout mode (co cau giai thuong)
    public enum PayoutMode { Template, Custom }
    //rules
    public enum Rule { WNT, WPA }


}
