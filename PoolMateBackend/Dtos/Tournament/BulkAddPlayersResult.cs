namespace PoolMate.Api.Dtos.Tournament
{
    public class BulkAddPlayersResult
    {
        public int AddedCount { get; set; }
        public List<Item> Added { get; set; } = new();
        public List<SkippedItem> Skipped { get; set; } = new();

        public class Item
        {
            public int Id { get; set; }
            public string DisplayName { get; set; } = string.Empty;
        }

        public class SkippedItem
        {
            public string Line { get; set; } = string.Empty;
            public string Reason { get; set; } = string.Empty;
        }
    }
}
