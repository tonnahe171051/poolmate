using PoolMate.Api.Models;
using PoolMate.Api.Dtos.Tournament;
using PoolMate.Api.Dtos.PlayerProfile;
using PoolMate.Api.Dtos.Payout;

namespace PoolMate.Api.Tests.Fixtures
{
    /// <summary>
    /// Factory ?? t?o mock data cho testing
    /// </summary>
    public static class MockDataFactory
    {
        /// <summary>
        /// T?o mock Tournament
        /// </summary>
        public static Tournament CreateMockTournament(
            string name = "Test Tournament",
            string organizerId = "org-123",
            DateTime? startDate = null)
        {
            return new Tournament
            {
                Id = Guid.NewGuid().ToString(),
                Name = name,
                OrganizerId = organizerId,
                StartDate = startDate ?? DateTime.UtcNow.AddDays(7),
                EndDate = (startDate ?? DateTime.UtcNow.AddDays(7)).AddDays(1),
                Format = 1, // Round Robin
                Status = 0, // Draft
                CreatedAt = DateTime.UtcNow
            };
        }

        /// <summary>
        /// T?o mock Player
        /// </summary>
        public static Player CreateMockPlayer(
            string name = "Test Player",
            string? email = null)
        {
            return new Player
            {
                Id = Guid.NewGuid().ToString(),
                Name = name,
                Email = email ?? $"{name.ToLower().Replace(" ", "")}@test.com",
                CreatedAt = DateTime.UtcNow
            };
        }

        /// <summary>
        /// T?o mock TournamentPlayer
        /// </summary>
        public static TournamentPlayer CreateMockTournamentPlayer(
            string tournamentId,
            string playerId,
            int slotNumber = 1)
        {
            return new TournamentPlayer
            {
                Id = Guid.NewGuid().ToString(),
                TournamentId = tournamentId,
                PlayerId = playerId,
                SlotNumber = slotNumber,
                Status = 0, // Pending
                JoinedAt = DateTime.UtcNow
            };
        }

        /// <summary>
        /// T?o mock Match
        /// </summary>
        public static Match CreateMockMatch(
            string tournamentId,
            string player1Id,
            string player2Id,
            int roundNumber = 1)
        {
            return new Match
            {
                Id = Guid.NewGuid().ToString(),
                TournamentId = tournamentId,
                Player1Id = player1Id,
                Player2Id = player2Id,
                RoundNumber = roundNumber,
                Status = 0, // Pending
                CreatedAt = DateTime.UtcNow
            };
        }

        /// <summary>
        /// T?o mock PayoutTemplate
        /// </summary>
        public static PayoutTemplate CreateMockPayoutTemplate(
            string name = "Default Payout",
            int numPlayers = 8)
        {
            var rankPercentages = new Dictionary<int, decimal>
            {
                { 1, 50 },
                { 2, 30 },
                { 3, 15 },
                { 4, 5 }
            };

            return new PayoutTemplate
            {
                Id = Guid.NewGuid().ToString(),
                Name = name,
                NumPlayers = numPlayers,
                RankPercentages = rankPercentages,
                CreatedAt = DateTime.UtcNow
            };
        }

        /// <summary>
        /// T?o ApplicationUser cho testing
        /// </summary>
        public static ApplicationUser CreateMockApplicationUser(
            string email = "test@example.com",
            string userName = "testuser")
        {
            return new ApplicationUser
            {
                Id = Guid.NewGuid().ToString(),
                Email = email,
                UserName = userName,
                NormalizedEmail = email.ToUpper(),
                NormalizedUserName = userName.ToUpper(),
                EmailConfirmed = true,
                CreatedAt = DateTime.UtcNow
            };
        }
    }
}
