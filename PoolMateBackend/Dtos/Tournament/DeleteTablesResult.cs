namespace PoolMate.Api.Dtos.Tournament
{
    public class DeleteTablesResult
    {
        public int DeletedCount { get; set; }
        public List<int> DeletedIds { get; set; } = new();
        public List<FailedItem> Failed { get; set; } = new();

        public class FailedItem
        {
            public int TableId { get; set; }
            public string Reason { get; set; } = default!;
        }
    }
}

