using PoolMate.Api.Models;

namespace PoolMate.Api.Dtos.Tournament
{
    public class StagePreviewDto
    {
        public int StageNo { get; set; }
        public BracketType Type { get; set; }
        public BracketOrdering Ordering { get; set; }
        public int BracketSize { get; set; }
        public List<RoundPreviewDto> Rounds { get; set; } = new();
    }
}
