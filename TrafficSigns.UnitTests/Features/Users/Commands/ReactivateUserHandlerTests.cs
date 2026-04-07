using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using TrafficSigns.Application.Common.Interfaces;
using TrafficSigns.Domain.Models;
using TrafficSigns.Infrastructure.Persistence;
using TrafficSigns.Web.Features.Users.Commands;

namespace TrafficSigns.UnitTests.Features.Users.Commands;

public class ReactivateUserHandlerTests
{
    private readonly AppDbContext _db;
    private readonly IKeycloakAdminService _keycloakService;
    private readonly ICurrentUserService _currentUser;
    private readonly IPermissionService _permissionService;
    private readonly ReactivateUserHandler _handler;

    public ReactivateUserHandlerTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _db = new AppDbContext(options);

        _keycloakService = Substitute.For<IKeycloakAdminService>();
        _currentUser = Substitute.For<ICurrentUserService>();
        _permissionService = Substitute.For<IPermissionService>();

        _handler = new ReactivateUserHandler(_keycloakService, _db, _currentUser, _permissionService);
    }

    [Fact]
    public async Task Case1_Handle_ShouldThrowUnauthorized_WhenNoPermission()
    {
        // Arrange
        _permissionService.CanManageGlobalUsersAsync().Returns(false);
        var command = new ReactivateUserCommand(Guid.NewGuid(), "NewPass123!");

        // Act
        var act = () => _handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task Case2_Handle_ShouldThrowException_WhenUserNotFound()
    {
        // Arrange
        _permissionService.CanManageGlobalUsersAsync().Returns(true);
        var command = new ReactivateUserCommand(Guid.NewGuid(), "NewPass123!");

        // Act
        var act = () => _handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<Exception>().WithMessage("User not found.");
    }

    [Fact]
    public async Task Case3_Handle_ShouldThrowException_WhenUserIsAlreadyActive()
    {
        // Arrange
        var userId = Guid.NewGuid();
        _db.Users.Add(new User { Id = userId, Username = "active_user", Inactive = false });
        await _db.SaveChangesAsync();

        _permissionService.CanManageGlobalUsersAsync().Returns(true);
        var command = new ReactivateUserCommand(userId, "NewPass123!");

        // Act
        var act = () => _handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<Exception>().WithMessage("User is already active.");
    }

    [Fact]
    public async Task Case4_Handle_ShouldInvokeKeycloakUpdateAndResetPassword_WhenSuccess()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var newPass = "StrongPassword99!";
        _db.Users.Add(new User { Id = userId, Username = "inactive_user", Inactive = true });
        await _db.SaveChangesAsync();

        _permissionService.CanManageGlobalUsersAsync().Returns(true);
        var command = new ReactivateUserCommand(userId, newPass);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().Be(userId);

        await _keycloakService.Received(1).UpdateUserStatusAsync(userId, true);

        await _keycloakService.Received(1).ResetPasswordAsync(userId, newPass);

        var userInDb = await _db.Users.FirstAsync(u => u.Id == userId);
        userInDb.Inactive.Should().BeFalse();
    }

    [Fact]
    public async Task Case5_Handle_ShouldRecordCorrectAuditLog_InMetadata()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var mockAdminId = Guid.NewGuid();
        _db.Users.Add(new User { Id = userId, Username = "reactivate_me", Inactive = true });
        await _db.SaveChangesAsync();

        _permissionService.CanManageGlobalUsersAsync().Returns(true);
        _currentUser.GetUsername().Returns("SuperAdmin");
        _currentUser.GetUserId().Returns(mockAdminId);

        var command = new ReactivateUserCommand(userId, "Pass123!");

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        var userInDb = await _db.Users.FirstAsync(u => u.Id == userId);
        var log = userInDb.Metadata["update_history"].ToString();
        log.Should().Contain($"Reactivated by SuperAdmin({mockAdminId})");
    }
}