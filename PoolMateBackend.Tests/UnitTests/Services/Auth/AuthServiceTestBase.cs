using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Moq;
using PoolMate.Api.Integrations.Email;
using PoolMate.Api.Models;
using PoolMate.Api.Services;

namespace PoolMateBackend.Tests.UnitTests.Services;

/// <summary>
/// Base class for AuthService tests.
/// Contains shared mock setup and helper methods.
/// </summary>
public abstract class AuthServiceTestBase
{
    protected readonly Mock<UserManager<ApplicationUser>> MockUserManager;
    protected readonly Mock<RoleManager<IdentityRole>> MockRoleManager;
    protected readonly Mock<IConfiguration> MockConfiguration;
    protected readonly Mock<IEmailSender> MockEmailSender;
    protected readonly AuthService Sut;

    protected AuthServiceTestBase()
    {
        MockUserManager = CreateMockUserManager();
        MockRoleManager = CreateMockRoleManager();
        MockConfiguration = new Mock<IConfiguration>();
        MockEmailSender = new Mock<IEmailSender>();

        // Setup JWT configuration
        MockConfiguration.Setup(c => c["JWT:ValidIssuer"]).Returns("TestIssuer");
        MockConfiguration.Setup(c => c["JWT:ValidAudience"]).Returns("TestAudience");
        MockConfiguration.Setup(c => c["JWT:Secret"]).Returns("ThisIsASecretKeyForTestingPurposesOnly123456789");

        Sut = new AuthService(
            MockUserManager.Object,
            MockRoleManager.Object,
            MockConfiguration.Object,
            MockEmailSender.Object
        );
    }

    #region Helper Methods

    /// <summary>
    /// Creates a mock UserManager with required constructor parameters.
    /// </summary>
    protected static Mock<UserManager<ApplicationUser>> CreateMockUserManager()
    {
        var store = new Mock<IUserStore<ApplicationUser>>();
        return new Mock<UserManager<ApplicationUser>>(
            store.Object,
            null!, // IOptions<IdentityOptions>
            null!, // IPasswordHasher<ApplicationUser>
            null!, // IEnumerable<IUserValidator<ApplicationUser>>
            null!, // IEnumerable<IPasswordValidator<ApplicationUser>>
            null!, // ILookupNormalizer
            null!, // IdentityErrorDescriber
            null!, // IServiceProvider
            null!  // ILogger<UserManager<ApplicationUser>>
        );
    }

    /// <summary>
    /// Creates a mock RoleManager with required constructor parameters.
    /// </summary>
    protected static Mock<RoleManager<IdentityRole>> CreateMockRoleManager()
    {
        var store = new Mock<IRoleStore<IdentityRole>>();
        return new Mock<RoleManager<IdentityRole>>(
            store.Object,
            null!, // IEnumerable<IRoleValidator<IdentityRole>>
            null!, // ILookupNormalizer
            null!, // IdentityErrorDescriber
            null!  // ILogger<RoleManager<IdentityRole>>
        );
    }

    #endregion
}
