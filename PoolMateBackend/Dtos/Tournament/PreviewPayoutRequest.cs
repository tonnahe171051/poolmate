namespace PoolMate.Api.Dtos.Tournament
{
    public class PreviewPayoutRequest
    {
        public int Players { get; set; } = 0;          
        public decimal? EntryFee { get; set; }
        public decimal? AdminFee { get; set; }
        public decimal? AddedMoney { get; set; }

        public bool IsCustom { get; set; } = false;     // = PayoutMode == Custom
        public decimal? TotalPrizeWhenCustom { get; set; }

        public int? PayoutTemplateId { get; set; }
    }
}
