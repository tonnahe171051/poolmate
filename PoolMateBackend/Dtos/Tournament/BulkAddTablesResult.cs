namespace PoolMate.Api.Dtos.Tournament
{
    public class BulkAddTablesResult
    {
        public int AddedCount { get; set; }
        public List<Item> Added { get; set; } = new();

        public class Item
        {
            public int Id { get; set; }
            public string Label { get; set; } = default!;
        }
    }
}
