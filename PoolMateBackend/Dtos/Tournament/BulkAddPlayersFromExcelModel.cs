using Microsoft.AspNetCore.Http;

namespace PoolMate.Api.Dtos.Tournament
{
    public class BulkAddPlayersFromExcelModel
    {
        public IFormFile File { get; set; } = default!;
    }
}
