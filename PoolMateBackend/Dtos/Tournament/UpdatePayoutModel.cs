using PoolMate.Api.Models;

namespace PoolMate.Api.Dtos.Tournament
{
    public class UpdatePayoutModel
    {
        public decimal? EntryFee { get; set; }
        public decimal? AdminFee { get; set; }
        public decimal? AddedMoney { get; set; }
        public PayoutMode? PayoutMode { get; set; }     // Template | Custom
        public int? PayoutTemplateId { get; set; }      // khi Template
        public decimal? TotalPrize { get; set; }        // khi Custom
    }
}
