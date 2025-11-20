namespace PoolMate.Api.Services
{
    public sealed record ScoringContext(
        string ActorId,
        bool IsTableClient,
        int? TableId,
        int? TournamentId,
        string? TokenId)
    {
        public static ScoringContext ForTable(int tableId, int tournamentId, string tokenId)
            => new($"table:{tokenId}", true, tableId, tournamentId, tokenId);

        public static ScoringContext ForUser(string userId)
            => new($"user:{userId}", false, null, null, null);
    }
}
