using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using TrafficSigns.Application.Common.Interfaces;
using TrafficSigns.Domain.Models;
using TrafficSigns.Infrastructure.Persistence;
using TrafficSigns.Web.Features.AccountUsers.Commands;

namespace TrafficSigns.UnitTests.Features.AccountUsers.Commands;

public class RemoveUserFromAccountHandlerTests
{
    private readonly AppDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IPermissionService _permissionService;
    private readonly RemoveUserFromAccountHandler _handler;

    public RemoveUserFromAccountHandlerTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _db = new AppDbContext(options);

        _currentUser = Substitute.For<ICurrentUserService>();
        _permissionService = Substitute.For<IPermissionService>();

        _handler = new RemoveUserFromAccountHandler(_db, _currentUser, _permissionService);
    }

    [Fact]
    public async Task Case1_Handle_ShouldThrowUnauthorized_WhenPermissionDenied()
    {
        // Arrange
        var accId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        _permissionService.CanRemoveUserAsync(accId, userId).Returns(false);

        var command = new RemoveUserFromAccountCommand(accId, userId);

        // Act
        var act = () => _handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("Access denied or cannot remove the last owner.");
    }

    [Fact]
    public async Task Case2_Handle_ShouldThrowException_WhenAssociationDoesNotExist()
    {
        // Arrange
        var accId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        _permissionService.CanRemoveUserAsync(accId, userId).Returns(true);

        var command = new RemoveUserFromAccountCommand(accId, userId);

        // Act
        var act = () => _handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<Exception>()
            .WithMessage("User association not found or already inactive.");
    }

    [Fact]
    public async Task Case3_Handle_ShouldThrowException_WhenUserIsAlreadyInactive()
    {
        // Arrange
        var accId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        _db.AccountUsers.Add(new AccountUser
        {
            AccountId = accId,
            UserId = userId,
            IsDeleted = true
        });
        await _db.SaveChangesAsync();

        _permissionService.CanRemoveUserAsync(accId, userId).Returns(true);
        var command = new RemoveUserFromAccountCommand(accId, userId);

        // Act
        var act = () => _handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<Exception>()
            .WithMessage("User association not found or already inactive.");
    }

    [Fact]
    public async Task Case4_Handle_ShouldSetInactiveToTrue_WhenSuccess()
    {
        // Arrange
        var accId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var association = new AccountUser { AccountId = accId, UserId = userId, IsDeleted = false };

        _db.AccountUsers.Add(association);
        await _db.SaveChangesAsync();

        _permissionService.CanRemoveUserAsync(accId, userId).Returns(true);
        var command = new RemoveUserFromAccountCommand(accId, userId);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().BeTrue();

        var updated = await _db.AccountUsers.FirstAsync(au => au.AccountId == accId && au.UserId == userId);
        updated.IsDeleted.Should().BeTrue();
    }

    [Fact]
    public async Task Case5_Handle_ShouldRecordCorrectAuditLog_InMetadata()
    {
        // Arrange
        var accId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var mockAdminId = Guid.NewGuid();
        _db.AccountUsers.Add(new AccountUser { AccountId = accId, UserId = userId, IsDeleted = false });
        await _db.SaveChangesAsync();

        _permissionService.CanRemoveUserAsync(accId, userId).Returns(true);

        _currentUser.GetUsername().Returns("ManagerAccount");
        _currentUser.GetUserId().Returns(mockAdminId);

        var command = new RemoveUserFromAccountCommand(accId, userId);

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        var updated = await _db.AccountUsers.FirstAsync(au => au.AccountId == accId);
        updated.Metadata.Should().ContainKey("update_history");

        var log = updated.Metadata["update_history"].ToString();
        log.Should().Contain($"Removed by ManagerAccount({mockAdminId})");
    }
}