using PoolMate.Api.Models;

namespace PoolMate.Api.Dtos.Tournament
{
    public class CreateBracketRequest
    {
        public BracketCreationType Type { get; set; } = BracketCreationType.Automatic;
        public List<ManualSlotAssignment>? ManualAssignments { get; set; }
    }
}
