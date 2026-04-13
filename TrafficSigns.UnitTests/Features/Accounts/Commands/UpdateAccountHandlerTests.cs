using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using TrafficSigns.Application.Common.Interfaces;
using TrafficSigns.Domain.Models;
using TrafficSigns.Infrastructure.Persistence;
using TrafficSigns.Web.Features.Accounts.Commands;
using Xunit;

namespace TrafficSigns.UnitTests.Features.Accounts.Commands;

public class UpdateAccountHandlerTests
{
    private readonly AppDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IPermissionService _permissionService;
    private readonly UpdateAccountHandler _handler;

    public UpdateAccountHandlerTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _db = new AppDbContext(options);

        _currentUser = Substitute.For<ICurrentUserService>();
        _permissionService = Substitute.For<IPermissionService>();
        _handler = new UpdateAccountHandler(_db, _currentUser, _permissionService);
    }

    [Fact]
    public async Task Case1_Handle_ShouldReturnFalse_WhenAccountNotFound()
    {
        // Arrange
        var command = new UpdateAccountCommand(Guid.NewGuid(), "Name", null, null, null);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task Case2_Handle_ShouldThrowUnauthorized_WhenPermissionServiceDenies()
    {
        // Arrange
        var accId = Guid.NewGuid();
        _db.Accounts.Add(new Account { Id = accId, Name = "Old Name", System = false });
        await _db.SaveChangesAsync();

        _permissionService.CanUpdateAccountAsync(accId, Arg.Any<bool>()).Returns(false);

        var command = new UpdateAccountCommand(accId, "New Name", null, null, null, false);

        // Act
        var act = () => _handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task Case3_Handle_ShouldDetectSystemFieldChange_AndPassToPermissionService()
    {
        // Arrange
        var accId = Guid.NewGuid();
        _db.Accounts.Add(new Account { Id = accId, Name = "Old Name", System = false });
        await _db.SaveChangesAsync();

        var command = new UpdateAccountCommand(accId, "New Name", null, null, null, true);

        _permissionService.CanUpdateAccountAsync(accId, true).Returns(true);

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        await _permissionService.Received().CanUpdateAccountAsync(accId, true);
    }

    [Fact]
    public async Task Case4_Handle_ShouldUpdateAllFields_AndTrimStrings()
    {
        // Arrange
        var accId = Guid.NewGuid();
        _db.Accounts.Add(new Account { Id = accId, Name = "Old Name", IsDeleted = false });
        await _db.SaveChangesAsync();

        _permissionService.CanUpdateAccountAsync(accId, Arg.Any<bool>()).Returns(true);

        var command = new UpdateAccountCommand(
            accId,
            "   Updated Name   ",
            "   Updated Desc   ",
            "   test@update.com   ",
            "   0987654321   ",
            false);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().BeTrue();
        var account = await _db.Accounts.FirstAsync(a => a.Id == accId);

        account.Name.Should().Be("Updated Name"); // Check Trim
        account.Desc.Should().Be("Updated Desc");
        account.Email.Should().Be("test@update.com");
        account.Phone.Should().Be("0987654321");
        account.UpdatedDt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task Case5_Handle_ShouldRecordAuditLog_InMetadata()
    {
        // Arrange
        var accId = Guid.NewGuid();
        var mockAdminId = Guid.NewGuid();
        _db.Accounts.Add(new Account { Id = accId, Name = "Test Account" });
        await _db.SaveChangesAsync();

        _permissionService.CanUpdateAccountAsync(accId, Arg.Any<bool>()).Returns(true);
        _currentUser.GetUsername().Returns("EditorUser");
        _currentUser.GetUserId().Returns(mockAdminId);

        var command = new UpdateAccountCommand(accId, "New Name", null, null, null);

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        var account = await _db.Accounts.FirstAsync(a => a.Id == accId);
        var log = account.Metadata["update_history"].ToString();
        log.Should().Contain($"Updated by EditorUser({mockAdminId})");
    }
}