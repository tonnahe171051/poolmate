namespace PoolMate.Api.Dtos.Tournament
{
    public class PayoutPreviewResponse
    {
        public int Players { get; set; }
        public decimal Total { get; set; }

        public List<BreakdownItem> Breakdown { get; set; } = new();

        public class BreakdownItem
        {
            public int Rank { get; set; }        // 1,2,3,...
            public decimal Percent { get; set; } // 0..100
            public decimal Amount { get; set; }  // money for this rank
        }
    }
    public record RankPercent(int rank, decimal percent);
}
