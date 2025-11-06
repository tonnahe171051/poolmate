namespace PoolMate.Api.Dtos.Tournament
{
    public class ManualSlotAssignment
    {
        public int SlotPosition { get; set; } 
        public int? TpId { get; set; } // null nếu slot trống
    }
}
