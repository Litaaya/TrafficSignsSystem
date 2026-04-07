using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using TrafficSigns.Application.Common.Interfaces;
using TrafficSigns.Domain.Models;
using TrafficSigns.Infrastructure.Persistence;
using TrafficSigns.Web.Features.AccountUsers.Commands;

namespace TrafficSigns.UnitTests.Features.AccountUsers.Commands;

public class UpdateUserInAccountHandlerTests
{
    private readonly AppDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IPermissionService _permissionService;
    private readonly UpdateUserInAccountHandler _handler;

    public UpdateUserInAccountHandlerTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _db = new AppDbContext(options);

        _currentUser = Substitute.For<ICurrentUserService>();
        _permissionService = Substitute.For<IPermissionService>();
        _handler = new UpdateUserInAccountHandler(_db, _currentUser, _permissionService);
    }

    [Fact]
    public async Task Case1_Handle_ShouldThrowUnauthorized_WhenNoPermission()
    {
        // Arrange
        var accId = Guid.NewGuid();
        _permissionService.CanManageAccountUsersAsync(accId).Returns(false);
        var command = new UpdateUserInAccountCommand(accId, Guid.NewGuid(), "Member");

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
        var command = new UpdateUserInAccountCommand(accId, Guid.NewGuid(), "InvalidRole");

        // Act
        var act = () => _handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<Exception>().WithMessage("*Allowed roles are*");
    }

    [Fact]
    public async Task Case3_Handle_ShouldThrowException_WhenAssociationNotFoundOrInactive()
    {
        // Arrange
        var accId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        _permissionService.CanManageAccountUsersAsync(accId).Returns(true);

        _db.AccountUsers.Add(new AccountUser { AccountId = accId, UserId = userId, Inactive = true });
        await _db.SaveChangesAsync();

        var command = new UpdateUserInAccountCommand(accId, userId, "Member");

        // Act
        var act = () => _handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<Exception>().WithMessage("User association not found or is inactive.");
    }

    [Fact]
    public async Task Case4_Handle_ShouldThrowException_WhenChangingRoleOfLastOwner()
    {
        // Arrange
        var accId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        _db.AccountUsers.Add(new AccountUser { AccountId = accId, UserId = userId, Role = "Owner", Inactive = false });
        await _db.SaveChangesAsync();

        _permissionService.CanManageAccountUsersAsync(accId).Returns(true);

        var command = new UpdateUserInAccountCommand(accId, userId, "Member");

        // Act
        var act = () => _handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<Exception>().WithMessage("Cannot change the role of the last owner*");
    }

    [Fact]
    public async Task Case5_Handle_ShouldAllowRoleChange_WhenAnotherOwnerExists()
    {
        // Arrange
        var accId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var otherOwnerId = Guid.NewGuid();

        _db.AccountUsers.Add(new AccountUser { AccountId = accId, UserId = userId, Role = "Owner", Inactive = false });
        _db.AccountUsers.Add(new AccountUser { AccountId = accId, UserId = otherOwnerId, Role = "Owner", Inactive = false });
        await _db.SaveChangesAsync();

        _permissionService.CanManageAccountUsersAsync(accId).Returns(true);
        var command = new UpdateUserInAccountCommand(accId, userId, "Member");

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().BeTrue();
        var updated = await _db.AccountUsers.FirstAsync(au => au.UserId == userId);
        updated.Role.Should().Be("Member");
    }

    [Fact]
    public async Task Case6_Handle_ShouldUpdateRole_ForNormalMember()
    {
        // Arrange
        var accId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        _db.AccountUsers.Add(new AccountUser { AccountId = accId, UserId = userId, Role = "Viewer", Inactive = false });
        await _db.SaveChangesAsync();

        _permissionService.CanManageAccountUsersAsync(accId).Returns(true);
        var command = new UpdateUserInAccountCommand(accId, userId, "Member");

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        var updated = await _db.AccountUsers.FirstAsync(au => au.UserId == userId);
        updated.Role.Should().Be("Member");
    }

    [Fact]
    public async Task Case7_Handle_ShouldRecordCorrectAuditLog_InMetadata()
    {
        // Arrange
        var accId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var mockAdminId = Guid.NewGuid();

        _db.AccountUsers.Add(new AccountUser { AccountId = accId, UserId = userId, Role = "Viewer", Inactive = false });
        await _db.SaveChangesAsync();

        _permissionService.CanManageAccountUsersAsync(accId).Returns(true);
        _currentUser.GetUsername().Returns("AdminEditor");
        _currentUser.GetUserId().Returns(mockAdminId);

        var command = new UpdateUserInAccountCommand(accId, userId, "Owner");

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        var updated = await _db.AccountUsers.FirstAsync(au => au.UserId == userId);
        var log = updated.Metadata["update_history"].ToString();
        log.Should().Contain($"Role updated to Owner by AdminEditor({mockAdminId})");
    }
}