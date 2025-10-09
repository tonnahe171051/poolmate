using System.ComponentModel.DataAnnotations;

namespace PoolMate.Api.Dtos.Tournament
{
    public class DeleteTablesModel
    {
        [Required]
        public List<int> TableIds { get; set; } = new();
    }
}

