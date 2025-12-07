using Microsoft.AspNetCore.Identity;
using Moq;
using PoolMate.Api.Dtos.Auth;
using PoolMate.Api.Models;

namespace PoolMateBackend.Tests.UnitTests.Services;

/// <summary>
/// Unit tests for AuthService.RegisterAsync method.
/// </summary>
public class AuthServiceRegisterTests : AuthServiceTestBase
{
    private const string BaseUri = "https://test.com";

    #region RegisterAsync Tests

    /// <summary>
    /// Test case: User already exists - should return error and CreateAsync should NOT be called.
    /// </summary>
    [Fact]
    public async Task Register_UserAlreadyExists_ReturnsError()
    {
        // Arrange
        var registerModel = new RegisterModel
        {
            Username = "existinguser",
            Email = "existing@example.com",
            Password = "Password123!",
            ConfirmPassword = "Password123!"
        };

        var existingUser = new ApplicationUser
        {
            Id = "existing-user-id",
            UserName = "existinguser",
            Email = "existing@example.com"
        };

        MockUserManager
            .Setup(x => x.FindByNameAsync("existinguser"))
            .ReturnsAsync(existingUser);

        // Act
        var result = await Sut.RegisterAsync(registerModel, BaseUri);

        // Assert
        Assert.False(result.Success);
        Assert.Equal("Error", result.Status);
        Assert.Equal("User already exists!", result.Message);

        // Verify CreateAsync was NOT called
        MockUserManager.Verify(
            x => x.CreateAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>()),
            Times.Never
        );
    }

    /// <summary>
    /// Test case: CreateAsync failed - should return identity errors and AddToRoleAsync should NOT be called.
    /// </summary>
    [Fact]
    public async Task Register_CreateFailed_ReturnsIdentityErrors()
    {
        // Arrange
        var registerModel = new RegisterModel
        {
            Username = "newuser",
            Email = "newuser@example.com",
            Password = "weak",
            ConfirmPassword = "weak"
        };

        var identityErrors = new[]
        {
            new IdentityError { Code = "PasswordTooShort", Description = "Password must be at least 6 characters." },
            new IdentityError { Code = "PasswordRequiresDigit", Description = "Password must have at least one digit." }
        };

        MockUserManager
            .Setup(x => x.FindByNameAsync("newuser"))
            .ReturnsAsync((ApplicationUser?)null);

        MockUserManager
            .Setup(x => x.CreateAsync(It.IsAny<ApplicationUser>(), "weak"))
            .ReturnsAsync(IdentityResult.Failed(identityErrors));

        // Act
        var result = await Sut.RegisterAsync(registerModel, BaseUri);

        // Assert
        Assert.False(result.Success);
        Assert.Equal("Error", result.Status);
        Assert.Contains("Password must be at least 6 characters.", result.Message);
        Assert.Contains("Password must have at least one digit.", result.Message);

        // Verify AddToRoleAsync was NOT called
        MockUserManager.Verify(
            x => x.AddToRoleAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>()),
            Times.Never
        );
    }

    /// <summary>
    /// Test case (Edge Case): Role does not exist - should create role first, then send email.
    /// </summary>
    [Fact]
    public async Task Register_Success_RoleDoesNotExist_CreatesRoleThenSendsEmail()
    {
        // Arrange
        var registerModel = new RegisterModel
        {
            Username = "newuser",
            Email = "newuser@example.com",
            Password = "Password123!",
            ConfirmPassword = "Password123!"
        };

        MockUserManager
            .Setup(x => x.FindByNameAsync("newuser"))
            .ReturnsAsync((ApplicationUser?)null);

        MockUserManager
            .Setup(x => x.CreateAsync(It.IsAny<ApplicationUser>(), "Password123!"))
            .ReturnsAsync(IdentityResult.Success);

        // Role does NOT exist
        MockRoleManager
            .Setup(x => x.RoleExistsAsync(UserRoles.PLAYER))
            .ReturnsAsync(false);

        MockRoleManager
            .Setup(x => x.CreateAsync(It.Is<IdentityRole>(r => r.Name == UserRoles.PLAYER)))
            .ReturnsAsync(IdentityResult.Success);

        MockUserManager
            .Setup(x => x.AddToRoleAsync(It.IsAny<ApplicationUser>(), UserRoles.PLAYER))
            .ReturnsAsync(IdentityResult.Success);

        MockUserManager
            .Setup(x => x.GenerateEmailConfirmationTokenAsync(It.IsAny<ApplicationUser>()))
            .ReturnsAsync("confirmation-token");

        MockEmailSender
            .Setup(x => x.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await Sut.RegisterAsync(registerModel, BaseUri);

        // Assert
        Assert.True(result.Success);
        Assert.Equal("Success", result.Status);
        Assert.Equal("User created. Please check your email to confirm.", result.Message);

        // Verify CreateAsync for Role was called exactly once (because role didn't exist)
        MockRoleManager.Verify(
            x => x.CreateAsync(It.Is<IdentityRole>(r => r.Name == UserRoles.PLAYER)),
            Times.Once
        );

        // Verify AddToRoleAsync was called
        MockUserManager.Verify(
            x => x.AddToRoleAsync(It.IsAny<ApplicationUser>(), UserRoles.PLAYER),
            Times.Once
        );

        // Verify email was sent
        MockEmailSender.Verify(
            x => x.SendAsync("newuser@example.com", It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Once
        );
    }

    /// <summary>
    /// Test case (Happy Path): Role exists - should NOT create role, add user to role and send email.
    /// </summary>
    [Fact]
    public async Task Register_Success_RoleExists_SendsEmail()
    {
        // Arrange
        var registerModel = new RegisterModel
        {
            Username = "newuser",
            Email = "newuser@example.com",
            Password = "Password123!",
            ConfirmPassword = "Password123!"
        };

        MockUserManager
            .Setup(x => x.FindByNameAsync("newuser"))
            .ReturnsAsync((ApplicationUser?)null);

        MockUserManager
            .Setup(x => x.CreateAsync(It.IsAny<ApplicationUser>(), "Password123!"))
            .ReturnsAsync(IdentityResult.Success);

        // Role already exists
        MockRoleManager
            .Setup(x => x.RoleExistsAsync(UserRoles.PLAYER))
            .ReturnsAsync(true);

        MockUserManager
            .Setup(x => x.AddToRoleAsync(It.IsAny<ApplicationUser>(), UserRoles.PLAYER))
            .ReturnsAsync(IdentityResult.Success);

        MockUserManager
            .Setup(x => x.GenerateEmailConfirmationTokenAsync(It.IsAny<ApplicationUser>()))
            .ReturnsAsync("confirmation-token");

        MockEmailSender
            .Setup(x => x.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await Sut.RegisterAsync(registerModel, BaseUri);

        // Assert
        Assert.True(result.Success);
        Assert.Equal("Success", result.Status);
        Assert.Equal("User created. Please check your email to confirm.", result.Message);

        // Verify CreateAsync for Role was NOT called (because role already exists)
        MockRoleManager.Verify(
            x => x.CreateAsync(It.IsAny<IdentityRole>()),
            Times.Never
        );

        // Verify AddToRoleAsync was called exactly once
        MockUserManager.Verify(
            x => x.AddToRoleAsync(It.IsAny<ApplicationUser>(), UserRoles.PLAYER),
            Times.Once
        );

        // Verify email was sent exactly once
        MockEmailSender.Verify(
            x => x.SendAsync("newuser@example.com", It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Once
        );
    }

    #endregion
}

