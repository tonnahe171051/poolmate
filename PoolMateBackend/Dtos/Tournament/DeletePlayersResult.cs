namespace PoolMate.Api.Dtos.Tournament
{
    public class DeletePlayersResult
    {
        public int DeletedCount { get; set; }
        public List<int> DeletedIds { get; set; } = new();
        public List<FailedItem> Failed { get; set; } = new();

        public class FailedItem
        {
            public int PlayerId { get; set; }
            public string Reason { get; set; } = default!;
        }
    }
}
