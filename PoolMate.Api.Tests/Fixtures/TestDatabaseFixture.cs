using Microsoft.EntityFrameworkCore;
using PoolMate.Api.Data;

namespace PoolMate.Api.Tests.Fixtures
{
    /// <summary>
    /// Fixture ?? t?o in-memory database cho testing
    /// </summary>
    public class TestDatabaseFixture : IDisposable
    {
        public ApplicationDbContext Context { get; }

        public TestDatabaseFixture()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

            Context = new ApplicationDbContext(options);
        }

        public void Dispose()
        {
            Context?.Dispose();
        }
    }
}
