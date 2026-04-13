using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using TrafficSigns.Application.Common.Interfaces;
using TrafficSigns.Domain.Models;
using TrafficSigns.Infrastructure.Persistence;
using TrafficSigns.Web.Features.Users.Commands;

namespace TrafficSigns.UnitTests.Features.Users.Commands;

public class CreateUserHandlerTests
{
    private readonly AppDbContext _db;
    private readonly IKeycloakAdminService _keycloakService;
    private readonly ICurrentUserService _currentUser;
    private readonly IPermissionService _permissionService;
    private readonly CreateUserHandler _handler;

    public CreateUserHandlerTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _db = new AppDbContext(options);

        _keycloakService = Substitute.For<IKeycloakAdminService>();
        _currentUser = Substitute.For<ICurrentUserService>();
        _permissionService = Substitute.For<IPermissionService>();

        _handler = new CreateUserHandler(_keycloakService, _db, _currentUser, _permissionService);
    }

    [Fact]
    public async Task Case1_Handle_ShouldThrowUnauthorized_WhenNoPermission()
    {
        // Arrange
        _permissionService.CanManageGlobalUsersAsync().Returns(false);
        var command = new CreateUserCommand("user", "pass", "email@test.com", "0123");

        // Act
        var act = () => _handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task Case2_Handle_ShouldThrowException_WhenUserExistsAndIsInactive()
    {
        // Arrange
        _permissionService.CanManageGlobalUsersAsync().Returns(true);
        _db.Users.Add(new User { Username = "olduser", Email = "old@test.com", Phone = "123", IsDeleted = true });
        await _db.SaveChangesAsync();

        var command = new CreateUserCommand("olduser", "pass", "new@email.com", "999");

        // Act
        var act = () => _handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<Exception>().WithMessage("*already exists but has been inactivated*");
    }

    [Fact]
    public async Task Case3_Handle_ShouldThrowSpecificConflict_WhenEmailExists()
    {
        // Arrange
        _permissionService.CanManageGlobalUsersAsync().Returns(true);
        _db.Users.Add(new User { Username = "other", Email = "duplicate@test.com", Phone = "111", IsDeleted = false });
        await _db.SaveChangesAsync();

        var command = new CreateUserCommand("newuser", "pass", "duplicate@test.com", "222");

        // Act
        var act = () => _handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<Exception>().WithMessage("Email already exists.");
    }

    [Fact]
    public async Task Case4_Handle_ShouldThrowCombinedConflict_WhenEmailAndPhoneExist()
    {
        // Arrange
        _permissionService.CanManageGlobalUsersAsync().Returns(true);
        _db.Users.Add(new User { Username = "other", Email = "email@test.com", Phone = "0123", IsDeleted = false });
        await _db.SaveChangesAsync();

        var command = new CreateUserCommand("newuser", "pass", "email@test.com", "0123");

        // Act
        var act = () => _handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<Exception>().WithMessage("Email and Phone Number already exists.");
    }

    [Fact]
    public async Task Case5_Handle_ShouldCreateUserSuccessfully_WhenAllDataIsValid()
    {
        // Arrange
        var mockKeycloakId = Guid.NewGuid();
        _permissionService.CanManageGlobalUsersAsync().Returns(true);

        _keycloakService.CreateUserAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>())
            .Returns(mockKeycloakId);

        var command = new CreateUserCommand("cleanuser", "password123", "clean@test.com", "0999888777", "First", "Last");

        // Act
        var resultId = await _handler.Handle(command, CancellationToken.None);

        // Assert
        resultId.Should().Be(mockKeycloakId);

        var userInDb = await _db.Users.FindAsync(mockKeycloakId);
        userInDb.Should().NotBeNull();
        userInDb!.Username.Should().Be("cleanuser");
        userInDb.Email.Should().Be("clean@test.com");

        await _keycloakService.Received(1).CreateUserAsync("cleanuser", "clean@test.com", "password123", "First", "Last");
    }

    [Fact]
    public async Task Case6_Handle_ShouldTrimInputs_BeforeSaving()
    {
        // Arrange
        _permissionService.CanManageGlobalUsersAsync().Returns(true);
        _keycloakService.CreateUserAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>())
            .Returns(Guid.NewGuid());

        var command = new CreateUserCommand("  spaced  ", "pass", "  email@test.com  ", "  0123  ", "  First  ", "  Last  ");

        // Act
        var id = await _handler.Handle(command, CancellationToken.None);

        // Assert
        var user = await _db.Users.FindAsync(id);
        user!.Username.Should().Be("spaced");
        user.Email.Should().Be("email@test.com");
        user.FirstName.Should().Be("First");
    }

    [Fact]
    public async Task Case7_Handle_ShouldRecordAuditLog_InMetadata()
    {
        // Arrange
        var mockAdminId = Guid.NewGuid();
        _permissionService.CanManageGlobalUsersAsync().Returns(true);
        _keycloakService.CreateUserAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>())
            .Returns(Guid.NewGuid());

        _currentUser.GetUsername().Returns("RootAdmin");
        _currentUser.GetUserId().Returns(mockAdminId);

        var command = new CreateUserCommand("audituser", "pass", "audit@test.com", "111");

        // Act
        var id = await _handler.Handle(command, CancellationToken.None);

        // Assert
        var user = await _db.Users.FindAsync(id);
        user!.Metadata["update_history"].ToString().Should().Contain($"Created by RootAdmin({mockAdminId})");
    }
}