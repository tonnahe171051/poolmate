using Microsoft.Extensions.DependencyInjection;
using PoolMate.Api.Data;
using System.Net.Http.Headers;

namespace PoolMateBackend.Tests.IntegrationTests.Base;

/// <summary>
/// Abstract base class for all integration tests.
/// Provides common functionality for HTTP client, database access, and authentication simulation.
/// </summary>
public abstract class IntegrationTestBase : IClassFixture<PoolMateWebApplicationFactory>, IDisposable
{
    protected readonly PoolMateWebApplicationFactory Factory;
    protected readonly HttpClient Client;
    protected readonly IServiceScope Scope;
    protected readonly ApplicationDbContext DbContext;

    /// <summary>
    /// Custom header name used for test authentication simulation.
    /// </summary>
    public const string TestUserIdHeader = "X-Test-User-Id";

    protected IntegrationTestBase(PoolMateWebApplicationFactory factory)
    {
        Factory = factory;
        Client = factory.CreateClient();
        Scope = factory.Services.CreateScope();
        DbContext = Scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    }

    /// <summary>
    /// Simulates authentication by adding a custom header with the user ID.
    /// In a real scenario, you might want to use a mock JWT token instead.
    /// </summary>
    /// <param name="userId">The user ID to authenticate as (use constants from PoolMateWebApplicationFactory)</param>
    protected void AuthenticateAs(string userId)
    {
        // Remove any existing auth headers
        Client.DefaultRequestHeaders.Remove(TestUserIdHeader);
        Client.DefaultRequestHeaders.Authorization = null;

        // Add custom test header for user identification
        Client.DefaultRequestHeaders.Add(TestUserIdHeader, userId);

        // Optionally, you can also set a mock Bearer token
        // This is useful if your middleware checks for Authorization header presence
        var mockToken = GenerateMockToken(userId);
        Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", mockToken);
    }

    /// <summary>
    /// Authenticates as the Admin user.
    /// </summary>
    protected void AuthenticateAsAdmin()
    {
        AuthenticateAs(PoolMateWebApplicationFactory.AdminUserId);
    }

    /// <summary>
    /// Authenticates as the Organizer user.
    /// </summary>
    protected void AuthenticateAsOrganizer()
    {
        AuthenticateAs(PoolMateWebApplicationFactory.OrganizerUserId);
    }

    /// <summary>
    /// Clears authentication headers from the client.
    /// </summary>
    protected void ClearAuthentication()
    {
        Client.DefaultRequestHeaders.Remove(TestUserIdHeader);
        Client.DefaultRequestHeaders.Authorization = null;
    }

    /// <summary>
    /// Resolves a service from the dependency injection container.
    /// </summary>
    /// <typeparam name="T">The type of service to resolve</typeparam>
    /// <returns>The resolved service instance</returns>
    protected T GetService<T>() where T : notnull
    {
        return Scope.ServiceProvider.GetRequiredService<T>();
    }

    /// <summary>
    /// Tries to resolve a service from the dependency injection container.
    /// </summary>
    /// <typeparam name="T">The type of service to resolve</typeparam>
    /// <returns>The resolved service instance or null if not found</returns>
    protected T? GetServiceOrDefault<T>() where T : class
    {
        return Scope.ServiceProvider.GetService<T>();
    }

    /// <summary>
    /// Creates a fresh database context from a new scope.
    /// Useful when you need to verify data independently from the test's main context.
    /// </summary>
    protected ApplicationDbContext CreateFreshDbContext()
    {
        return Factory.GetDbContext();
    }

    /// <summary>
    /// Generates a mock JWT-like token for testing purposes.
    /// This is a simplified mock - not a real JWT token.
    /// </summary>
    private static string GenerateMockToken(string userId)
    {
        // Create a simple base64-encoded mock token
        // Format: header.payload.signature (mimics JWT structure)
        var header = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("{\"alg\":\"HS256\",\"typ\":\"JWT\"}"));
        var payload = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes($"{{\"sub\":\"{userId}\",\"exp\":{DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeSeconds()}}}"));
        var signature = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("mock-signature"));

        return $"{header}.{payload}.{signature}";
    }

    /// <summary>
    /// Disposes resources used by the test.
    /// </summary>
    public virtual void Dispose()
    {
        Scope.Dispose();
        Client.Dispose();
        GC.SuppressFinalize(this);
    }
}

