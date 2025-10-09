using System.ComponentModel.DataAnnotations;

namespace PoolMate.Api.Dtos.Tournament
{
    public class DeletePlayersModel
    {
        [Required]
        public List<int> PlayerIds { get; set; } = new();
    }
}
