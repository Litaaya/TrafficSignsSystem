using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using TrafficSigns.Application.Common.Interfaces;
using TrafficSigns.Domain.Models;
using TrafficSigns.Infrastructure.Persistence;
using TrafficSigns.Web.Features.AccountUsers.Commands;

namespace TrafficSigns.UnitTests.Features.AccountUsers.Commands;

public class AssignUserToAccountHandlerTests
{
    private readonly AppDbContext _db;
    private readonly ICurrentUserService _currentUser = null!;
    private readonly IPermissionService _permissionService = null!;
    private readonly AssignUserToAccountHandler _handler;

    public AssignUserToAccountHandlerTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _db = new AppDbContext(options);

        _currentUser = Substitute.For<ICurrentUserService>();
        _permissionService = Substitute.For<IPermissionService>();
        _handler = new AssignUserToAccountHandler(_db, _currentUser, _permissionService);
    }

    [Fact]
    public async Task Case1_Handle_ShouldThrowUnauthorized_WhenUserHasNoPermission()
    {
        // Arrange
        var accId = Guid.NewGuid();
        _permissionService.CanManageAccountUsersAsync(accId).Returns(false);
        var command = new AssignUserToAccountCommand(accId, Guid.NewGuid());

        // Act
        var act = () => _handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task Case2_Handle_ShouldThrowException_WhenRoleIsInvalid()
    {
        // Arrange
        var accId = Guid.NewGuid();
        _permissionService.CanManageAccountUsersAsync(accId).Returns(true);
        var command = new AssignUserToAccountCommand(accId, Guid.NewGuid(), "Super-Admin");

        // Act
        var act = () => _handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<Exception>().WithMessage("*Invalid role*");
    }

    [Fact]
    public async Task Case3_Handle_ShouldThrowException_WhenUserDoesNotExist()
    {
        // Arrange
        var accId = Guid.NewGuid();
        _permissionService.CanManageAccountUsersAsync(accId).Returns(true);
        var command = new AssignUserToAccountCommand(accId, Guid.NewGuid(), "Viewer");

        // Act
        var act = () => _handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<Exception>().WithMessage("User not found.");
    }

    [Fact]
    public async Task Case4_Handle_ShouldThrowException_WhenAccountDoesNotExist()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var accId = Guid.NewGuid();
        _db.Users.Add(new User { Id = userId });
        await _db.SaveChangesAsync();

        _permissionService.CanManageAccountUsersAsync(accId).Returns(true);
        var command = new AssignUserToAccountCommand(accId, userId, "Viewer");

        // Act
        var act = () => _handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<Exception>().WithMessage("Account not found.");
    }

    [Fact]
    public async Task Case5_Handle_ShouldForceOwnerRole_WhenIsFirstUserInAccount()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var accId = Guid.NewGuid();
        _db.Users.Add(new User { Id = userId });
        _db.Accounts.Add(new Account { Id = accId });
        await _db.SaveChangesAsync();

        _permissionService.CanManageAccountUsersAsync(accId).Returns(true);

        var command = new AssignUserToAccountCommand(accId, userId, "Viewer");

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        var result = await _db.AccountUsers.FirstAsync(au => au.UserId == userId);
        result.Role.Should().Be("Owner");
    }

    [Fact]
    public async Task Case6_Handle_ShouldKeepRequestedRole_WhenAccountAlreadyHasUsers()
    {
        // Arrange
        var accId = Guid.NewGuid();
        var existingUserId = Guid.NewGuid();
        var newUserId = Guid.NewGuid();

        _db.Accounts.Add(new Account { Id = accId });
        _db.Users.Add(new User { Id = existingUserId });
        _db.Users.Add(new User { Id = newUserId });

        _db.AccountUsers.Add(new AccountUser { Id = Guid.NewGuid(), AccountId = accId, UserId = existingUserId, Role = "Owner" });
        await _db.SaveChangesAsync();

        _permissionService.CanManageAccountUsersAsync(accId).Returns(true);
        var command = new AssignUserToAccountCommand(accId, newUserId, "Member");

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        var result = await _db.AccountUsers.FirstAsync(au => au.UserId == newUserId);
        result.Role.Should().Be("Member");
    }

    [Fact]
    public async Task Case7_Handle_ShouldThrowException_WhenUserIsAlreadyActiveInAccount()
    {
        // Arrange
        var accId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        _db.Accounts.Add(new Account { Id = accId });
        _db.Users.Add(new User { Id = userId });
        _db.AccountUsers.Add(new AccountUser { AccountId = accId, UserId = userId, Inactive = false });
        await _db.SaveChangesAsync();

        _permissionService.CanManageAccountUsersAsync(accId).Returns(true);
        var command = new AssignUserToAccountCommand(accId, userId, "Viewer");

        // Act
        var act = () => _handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<Exception>().WithMessage("*already assigned*active*");
    }

    [Fact]
    public async Task Case8_Handle_ShouldReactivateAndUpdateRole_WhenUserWasInactive()
    {
        // Arrange
        var accId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        _db.Accounts.Add(new Account { Id = accId });
        _db.Users.Add(new User { Id = userId });

        var oldId = Guid.NewGuid();
        _db.AccountUsers.Add(new AccountUser { Id = oldId, AccountId = accId, UserId = userId, Inactive = true, Role = "Viewer" });
        await _db.SaveChangesAsync();

        _permissionService.CanManageAccountUsersAsync(accId).Returns(true);
        var command = new AssignUserToAccountCommand(accId, userId, "Member");

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        var result = await _db.AccountUsers.FindAsync(oldId);
        result!.Inactive.Should().BeFalse();
        result.Role.Should().Be("Member");
    }

    [Fact]
    public async Task Case9_Handle_ShouldCreateAuditLog_InMetadata()
    {
        // Arrange
        var accId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        _db.Accounts.Add(new Account { Id = accId });
        _db.Users.Add(new User { Id = userId });
        await _db.SaveChangesAsync();

        _permissionService.CanManageAccountUsersAsync(accId).Returns(true);
        _currentUser.GetUsername().Returns("AdminUser");

        var command = new AssignUserToAccountCommand(accId, userId, "Owner");

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        var result = await _db.AccountUsers.FirstAsync(au => au.UserId == userId);
        result.Metadata.Should().ContainKey("update_history");
        result.Metadata["update_history"].ToString().Should().Contain("Assigned as Owner by AdminUser");
    }
}