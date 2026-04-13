using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using TrafficSigns.Application.Common.Interfaces;
using TrafficSigns.Domain.Models;
using TrafficSigns.Infrastructure.Persistence;
using TrafficSigns.Web.Features.Users.Commands;

namespace TrafficSigns.UnitTests.Features.Users.Commands;

public class DeleteUserHandlerTests
{
    private readonly AppDbContext _db;
    private readonly IKeycloakAdminService _keycloakService;
    private readonly ICurrentUserService _currentUser;
    private readonly IPermissionService _permissionService;
    private readonly DeleteUserHandler _handler;

    public DeleteUserHandlerTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _db = new AppDbContext(options);

        _keycloakService = Substitute.For<IKeycloakAdminService>();
        _currentUser = Substitute.For<ICurrentUserService>();
        _permissionService = Substitute.For<IPermissionService>();

        _handler = new DeleteUserHandler(_db, _keycloakService, _currentUser, _permissionService);
    }

    [Fact]
    public async Task Case1_Handle_ShouldThrowUnauthorized_WhenNoPermission()
    {
        // Arrange
        _permissionService.CanManageGlobalUsersAsync().Returns(false);
        var command = new DeleteUserCommand(Guid.NewGuid());

        // Act
        var act = () => _handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task Case2_Handle_ShouldThrowException_WhenUserNotFoundOrAlreadyInactive()
    {
        // Arrange
        var userId = Guid.NewGuid();
        _db.Users.Add(new User { Id = userId, Username = "ghost", IsDeleted = true }); // Đã inactive
        await _db.SaveChangesAsync();

        _permissionService.CanManageGlobalUsersAsync().Returns(true);
        var command = new DeleteUserCommand(userId);

        // Act
        var act = () => _handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<Exception>().WithMessage("User not found or already inactivated.");
    }

    [Fact]
    public async Task Case3_Handle_ShouldUpdateKeycloakAndUserStatus_WhenSuccess()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = new User { Id = userId, Username = "target_user", IsDeleted = false };
        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        _permissionService.CanManageGlobalUsersAsync().Returns(true);
        var command = new DeleteUserCommand(userId);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().BeTrue();

        await _keycloakService.Received(1).UpdateUserStatusAsync(userId, false);

        var updatedUser = await _db.Users.FirstAsync(u => u.Id == userId);
        updatedUser.IsDeleted.Should().BeTrue();
    }

    [Fact]
    public async Task Case4_Handle_ShouldCascadeDeactivate_AllAccountUsers()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = new User { Id = userId, Username = "multi_acc_user", IsDeleted = false };
        _db.Users.Add(user);

        _db.AccountUsers.Add(new AccountUser { AccountId = Guid.NewGuid(), UserId = userId, IsDeleted = false });
        _db.AccountUsers.Add(new AccountUser { AccountId = Guid.NewGuid(), UserId = userId, IsDeleted = false });
        await _db.SaveChangesAsync();

        _permissionService.CanManageGlobalUsersAsync().Returns(true);
        var command = new DeleteUserCommand(userId);

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        var links = await _db.AccountUsers.Where(au => au.UserId == userId).ToListAsync();
        links.Should().HaveCount(2);
        links.All(l => l.IsDeleted).Should().BeTrue();
        links.All(l => l.Metadata["update_history"].ToString().Contains("User deactivation")).Should().BeTrue();
    }

    [Fact]
    public async Task Case5_Handle_ShouldRecordAuditLog_WithCorrectActorInfo()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var mockAdminId = Guid.NewGuid();
        _db.Users.Add(new User { Id = userId, Username = "delete_me", IsDeleted = false });
        await _db.SaveChangesAsync();

        _permissionService.CanManageGlobalUsersAsync().Returns(true);
        _currentUser.GetUsername().Returns("SuperAdmin");
        _currentUser.GetUserId().Returns(mockAdminId);

        var command = new DeleteUserCommand(userId);

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        var updatedUser = await _db.Users.FirstAsync(u => u.Id == userId);
        var log = updatedUser.Metadata["update_history"].ToString();
        log.Should().Contain($"Deactivated by SuperAdmin({mockAdminId})");
    }
}