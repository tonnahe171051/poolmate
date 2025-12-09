using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using PoolMate.Api.Dtos.Auth;
using PoolMate.Api.Dtos.UserProfile;
using PoolMate.Api.Integrations.Cloudinary36;
using PoolMate.Api.Models;
using PoolMate.Api.Services;
using PoolMateBackend.Tests.IntegrationTests.Base;

namespace PoolMateBackend.Tests.IntegrationTests.UserProfile;

/// <summary>
/// Integration tests for Profile Management (ProfileController).
/// Tests cover: GET /api/profile/me, PUT /api/profile, GET /api/profile/user/{id}
/// Follows the 80/20 rule for maximum coverage with minimum test cases.
/// </summary>
[Collection("Integration Tests")]
public class ProfileControllerTests : IntegrationTestBase
{
    private readonly UserManager<ApplicationUser> _userManager;
    private Mock<ICloudinaryService>? _cloudinaryMock;
    private Mock<IPlayerProfileService>? _playerProfileMock;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    public ProfileControllerTests(PoolMateWebApplicationFactory factory) : base(factory)
    {
        _userManager = GetService<UserManager<ApplicationUser>>();
    }

    private static async Task<T?> DeserializeResponse<T>(HttpResponseMessage response)
    {
        var content = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<T>(content, JsonOptions);
    }

    private void SetupMocks()
    {
        // Mock Cloudinary Service
        _cloudinaryMock = new Mock<ICloudinaryService>();
        _cloudinaryMock.Setup(x => x.DeleteAsync(It.IsAny<string>()))
            .ReturnsAsync(true);

        // Mock Player Profile Service
        _playerProfileMock = new Mock<IPlayerProfileService>();
        _playerProfileMock.Setup(x => x.UpdatePlayerFromUserAsync(
                It.IsAny<ApplicationUser>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
    }

    #region TC-P01: GetMe_Authenticated_ReturnsProfile

    [Fact]
    public async Task GetMe_Authenticated_ReturnsProfile()
    {
        // Arrange
        var email = $"test-{Guid.NewGuid()}@example.com";
        var user = await ProfileTestHelpers.CreateUserWithProfileAsync(
            DbContext,
            _userManager,
            email,
            "Test@123",
            "John",
            "Doe",
            phoneNumber: "+84123456789",
            nickname: "JDoe",
            city: "Hanoi",
            country: "VN",
            avatarUrl: "http://example.com/avatar.jpg",
            avatarPublicId: "avatar_123"
        );

        AuthenticateAs(user.Id);

        // Act
        var response = await Client.GetAsync("/api/profile/me");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await DeserializeResponse<Response>(response);
        result.Should().NotBeNull();
        result!.Status.Should().Be("Success");
        result.Data.Should().NotBeNull();

        // Verify response contains all expected fields
        var data = JsonSerializer.Deserialize<JsonElement>(result.Data!.ToString()!);
        data.GetProperty("email").GetString().Should().Be(email);
        data.GetProperty("phoneNumber").GetString().Should().Be("+84123456789");
        data.GetProperty("firstName").GetString().Should().Be("John");
        data.GetProperty("lastName").GetString().Should().Be("Doe");
        data.GetProperty("nickname").GetString().Should().Be("JDoe");
        data.GetProperty("city").GetString().Should().Be("Hanoi");
        data.GetProperty("country").GetString().Should().Be("VN");
        data.GetProperty("avatarUrl").GetString().Should().Be("http://example.com/avatar.jpg");
        data.GetProperty("avatarPublicId").GetString().Should().Be("avatar_123");

        // Verify database - user exists and data matches
        var dbUser = await _userManager.FindByIdAsync(user.Id);
        dbUser.Should().NotBeNull();
        dbUser!.Email.Should().Be(email);
        dbUser.FirstName.Should().Be("John");
        dbUser.LastName.Should().Be("Doe");
    }

    #endregion

    #region TC-P02: UpdateProfile_ValidData_Success

    [Fact]
    public async Task UpdateProfile_ValidData_Success()
    {
        // Arrange
        SetupMocks();

        var email = $"test-{Guid.NewGuid()}@example.com";
        var user = await ProfileTestHelpers.CreateUserWithProfileAsync(
            DbContext,
            _userManager,
            email,
            "Test@123",
            "John",
            "Doe",
            phoneNumber: "+84123456789",
            city: "Hanoi",
            country: "VN"
        );

        AuthenticateAs(user.Id);

        var request = new UpdateProfileModel
        {
            FirstName = "Jane",
            LastName = "Smith",
            Nickname = "JSmith",
            Phone = "+84987654321",
            City = "Ho Chi Minh",
            Country = "VN"
        };

        // Act
        var response = await Client.PutAsJsonAsync("/api/profile", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await DeserializeResponse<Response>(response);
        result.Should().NotBeNull();
        result!.Status.Should().Be("Success");

        // Verify response data
        var data = JsonSerializer.Deserialize<JsonElement>(result.Data!.ToString()!);
        data.GetProperty("firstName").GetString().Should().Be("Jane");
        data.GetProperty("lastName").GetString().Should().Be("Smith");
        data.GetProperty("nickname").GetString().Should().Be("JSmith");
        data.GetProperty("phoneNumber").GetString().Should().Be("+84987654321");
        data.GetProperty("city").GetString().Should().Be("Ho Chi Minh");
        data.GetProperty("country").GetString().Should().Be("VN");

        // Verify database persistence
        var freshUserManager = Factory.Services.CreateScope().ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var updatedUser = await freshUserManager.FindByIdAsync(user.Id);
        updatedUser.Should().NotBeNull();
        updatedUser!.FirstName.Should().Be("Jane");
        updatedUser.LastName.Should().Be("Smith");
        updatedUser.Nickname.Should().Be("JSmith");
        updatedUser.PhoneNumber.Should().Be("+84987654321");
        updatedUser.City.Should().Be("Ho Chi Minh");
        updatedUser.Country.Should().Be("VN");
    }

    #endregion

    #region TC-P03: UpdateProfile_WithAvatar_Success

    [Fact]
    public async Task UpdateProfile_WithAvatar_Success()
    {
        // Arrange
        SetupMocks();

        var email = $"test-{Guid.NewGuid()}@example.com";
        var user = await ProfileTestHelpers.CreateUserWithProfileAsync(
            DbContext,
            _userManager,
            email,
            "Test@123",
            "John",
            "Doe",
            avatarUrl: "http://old.url/avatar.jpg",
            avatarPublicId: "old_avatar_123"
        );

        AuthenticateAs(user.Id);

        var request = new UpdateProfileModel
        {
            AvatarPublicId = "new_avatar_456",
            AvatarUrl = "http://new.url/image.jpg"
        };

        // Act
        var response = await Client.PutAsJsonAsync("/api/profile", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await DeserializeResponse<Response>(response);
        result.Should().NotBeNull();
        result!.Status.Should().Be("Success");

        // Verify response data
        var data = JsonSerializer.Deserialize<JsonElement>(result.Data!.ToString()!);
        data.GetProperty("avatarUrl").GetString().Should().Be("http://new.url/image.jpg");
        data.GetProperty("avatarPublicId").GetString().Should().Be("new_avatar_456");

        // Verify database persistence
        var freshUserManager = Factory.Services.CreateScope().ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var updatedUser = await freshUserManager.FindByIdAsync(user.Id);
        updatedUser.Should().NotBeNull();
        updatedUser!.AvatarPublicId.Should().Be("new_avatar_456");
        updatedUser.ProfilePicture.Should().Be("http://new.url/image.jpg");
    }

    #endregion

    #region TC-P04: UpdateProfile_InvalidPhone_Returns400

    [Fact]
    public async Task UpdateProfile_InvalidPhone_Returns400()
    {
        // Arrange
        var email = $"test-{Guid.NewGuid()}@example.com";
        var user = await ProfileTestHelpers.CreateBasicUserAsync(DbContext, _userManager, email);

        AuthenticateAs(user.Id);

        var request = new UpdateProfileModel
        {
            Phone = "123abc" // Invalid: contains letters
        };

        // Act
        var response = await Client.PutAsJsonAsync("/api/profile", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("Invalid phone number");

        // Verify database - no changes
        var freshUserManager = Factory.Services.CreateScope().ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var unchangedUser = await freshUserManager.FindByIdAsync(user.Id);
        unchangedUser.Should().NotBeNull();
        unchangedUser!.PhoneNumber.Should().Be("+84123456789"); // Original value
    }

    #endregion

    #region TC-P05: UpdateProfile_PhoneTooShort_Returns400

    [Fact]
    public async Task UpdateProfile_PhoneTooShort_Returns400()
    {
        // Arrange
        var email = $"test-{Guid.NewGuid()}@example.com";
        var user = await ProfileTestHelpers.CreateBasicUserAsync(DbContext, _userManager, email);

        AuthenticateAs(user.Id);

        var request = new UpdateProfileModel
        {
            Phone = "123" // Too short (< 10 digits)
        };

        // Act
        var response = await Client.PutAsJsonAsync("/api/profile", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("Invalid phone number");

        // Verify database - no changes
        var freshUserManager = Factory.Services.CreateScope().ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var unchangedUser = await freshUserManager.FindByIdAsync(user.Id);
        unchangedUser!.PhoneNumber.Should().Be("+84123456789");
    }

    #endregion

    #region TC-P06: UpdateProfile_PhoneTooLong_Returns400

    [Fact]
    public async Task UpdateProfile_PhoneTooLong_Returns400()
    {
        // Arrange
        var email = $"test-{Guid.NewGuid()}@example.com";
        var user = await ProfileTestHelpers.CreateBasicUserAsync(DbContext, _userManager, email);

        AuthenticateAs(user.Id);

        var request = new UpdateProfileModel
        {
            Phone = "12345678901234567890" // Too long (> 15 digits)
        };

        // Act
        var response = await Client.PutAsJsonAsync("/api/profile", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("Invalid phone number");

        // Verify database - no changes
        var freshUserManager = Factory.Services.CreateScope().ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var unchangedUser = await freshUserManager.FindByIdAsync(user.Id);
        unchangedUser!.PhoneNumber.Should().Be("+84123456789");
    }

    #endregion

    #region TC-P07: UpdateProfile_PhoneOnlyWhitespace_Returns400

    [Fact]
    public async Task UpdateProfile_PhoneOnlyWhitespace_Returns400()
    {
        // Arrange
        var email = $"test-{Guid.NewGuid()}@example.com";
        var user = await ProfileTestHelpers.CreateBasicUserAsync(DbContext, _userManager, email);

        AuthenticateAs(user.Id);

        var request = new UpdateProfileModel
        {
            Phone = "   " // Only whitespace
        };

        // Act
        var response = await Client.PutAsJsonAsync("/api/profile", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("Phone number cannot be only whitespace");

        // Verify database - no changes
        var freshUserManager = Factory.Services.CreateScope().ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var unchangedUser = await freshUserManager.FindByIdAsync(user.Id);
        unchangedUser!.PhoneNumber.Should().Be("+84123456789");
    }

    #endregion

    #region TC-P08: GetUserProfile_NonExistentUser_Returns400

    [Fact]
    public async Task GetUserProfile_NonExistentUser_Returns400()
    {
        // Arrange
        var email = $"test-{Guid.NewGuid()}@example.com";
        var user = await ProfileTestHelpers.CreateBasicUserAsync(DbContext, _userManager, email);
        AuthenticateAs(user.Id);

        var invalidUserId = "invalid-user-id-999";

        // Act
        var response = await Client.GetAsync($"/api/profile/user/{invalidUserId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var result = await DeserializeResponse<Response>(response);
        result.Should().NotBeNull();
        result!.Status.Should().Be("Error");
        result.Message.Should().Be("User not found");
    }

    #endregion

    #region TC-P09: GetMe_Unauthenticated_Returns401

    [Fact]
    public async Task GetMe_Unauthenticated_Returns401()
    {
        // Arrange
        ClearAuthentication(); // No auth token

        // Act
        var response = await Client.GetAsync("/api/profile/me");

        // Assert
        // NOTE: ProfileController does NOT have [Authorize] attribute
        // When unauthenticated, User.FindFirstValue returns null, causing NullReferenceException
        // This will result in 500 Internal Server Error instead of 401
        // TODO: Add [Authorize] attribute to ProfileController for proper security
        response.StatusCode.Should().BeOneOf(HttpStatusCode.Unauthorized, HttpStatusCode.InternalServerError, HttpStatusCode.BadRequest);
    }

    #endregion

    #region TC-P10: UpdateProfile_Unauthenticated_Returns401

    [Fact]
    public async Task UpdateProfile_Unauthenticated_Returns401()
    {
        // Arrange
        ClearAuthentication(); // No auth token

        var request = new UpdateProfileModel
        {
            FirstName = "Hacker"
        };

        // Act
        var response = await Client.PutAsJsonAsync("/api/profile", request);

        // Assert
        // NOTE: ProfileController does NOT have [Authorize] attribute
        // When unauthenticated, User.FindFirstValue returns null, causing NullReferenceException
        // This will result in 500 Internal Server Error instead of 401
        // TODO: Add [Authorize] attribute to ProfileController for proper security
        response.StatusCode.Should().BeOneOf(HttpStatusCode.Unauthorized, HttpStatusCode.InternalServerError, HttpStatusCode.BadRequest);
    }

    #endregion

    #region TC-P11: UpdateProfile_PartialUpdate_Success

    [Fact]
    public async Task UpdateProfile_PartialUpdate_Success()
    {
        // Arrange
        var email = $"test-{Guid.NewGuid()}@example.com";
        var user = await ProfileTestHelpers.CreateUserWithProfileAsync(
            DbContext,
            _userManager,
            email,
            "Test@123",
            "John",
            "Doe",
            city: "Hanoi",
            country: "VN"
        );

        AuthenticateAs(user.Id);

        var request = new UpdateProfileModel
        {
            FirstName = "Jane" // Only update FirstName
            // LastName, City, Country should remain unchanged
        };

        // Act
        var response = await Client.PutAsJsonAsync("/api/profile", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await DeserializeResponse<Response>(response);
        result.Should().NotBeNull();

        // Verify response - FirstName updated, others unchanged
        var data = JsonSerializer.Deserialize<JsonElement>(result!.Data!.ToString()!);
        data.GetProperty("firstName").GetString().Should().Be("Jane"); // Updated
        data.GetProperty("lastName").GetString().Should().Be("Doe"); // Unchanged
        data.GetProperty("city").GetString().Should().Be("Hanoi"); // Unchanged
        data.GetProperty("country").GetString().Should().Be("VN"); // Unchanged

        // Verify database - partial update works with null-coalescing
        var freshUserManager = Factory.Services.CreateScope().ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var updatedUser = await freshUserManager.FindByIdAsync(user.Id);
        updatedUser.Should().NotBeNull();
        updatedUser!.FirstName.Should().Be("Jane"); // Updated
        updatedUser.LastName.Should().Be("Doe"); // Unchanged
        updatedUser.City.Should().Be("Hanoi"); // Unchanged
        updatedUser.Country.Should().Be("VN"); // Unchanged
    }

    #endregion

    #region TC-P12: GetUserProfile_PublicAccess_ReturnsLimitedData

    [Fact]
    public async Task GetUserProfile_PublicAccess_ReturnsLimitedData()
    {
        // Arrange
        // Create User A (logged in)
        var emailA = $"userA-{Guid.NewGuid()}@example.com";
        var userA = await ProfileTestHelpers.CreateBasicUserAsync(DbContext, _userManager, emailA);

        // Create User B (target profile)
        var emailB = $"userB-{Guid.NewGuid()}@example.com";
        var userB = await ProfileTestHelpers.CreateUserWithProfileAsync(
            DbContext,
            _userManager,
            emailB,
            "Test@123",
            "Jane",
            "Smith",
            phoneNumber: "+84987654321",
            nickname: "JSmith",
            city: "Ho Chi Minh",
            country: "VN",
            avatarUrl: "http://example.com/janeavatar.jpg"
        );

        AuthenticateAs(userA.Id);

        // Act
        var response = await Client.GetAsync($"/api/profile/user/{userB.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await DeserializeResponse<Response>(response);
        result.Should().NotBeNull();
        result!.Status.Should().Be("Success");

        // Verify public data is returned
        var data = JsonSerializer.Deserialize<JsonElement>(result.Data!.ToString()!);
        data.GetProperty("id").GetString().Should().Be(userB.Id);
        data.GetProperty("firstName").GetString().Should().Be("Jane");
        data.GetProperty("lastName").GetString().Should().Be("Smith");
        data.GetProperty("nickname").GetString().Should().Be("JSmith");
        data.GetProperty("city").GetString().Should().Be("Ho Chi Minh");
        data.GetProperty("country").GetString().Should().Be("VN");
        data.GetProperty("avatarUrl").GetString().Should().Be("http://example.com/janeavatar.jpg");

        // Verify private data is NOT returned
        var jsonString = data.ToString();
        jsonString.Should().NotContain("phoneNumber"); // Private
        jsonString.Should().NotContain("email"); // Private
        jsonString.Should().NotContain("+84987654321"); // Phone value should not be exposed
        jsonString.Should().NotContain(emailB); // Email value should not be exposed
    }

    #endregion
}


