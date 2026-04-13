using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using TrafficSigns.Application.Common.Interfaces;
using TrafficSigns.Domain.Models;
using TrafficSigns.Infrastructure.Persistence;
using TrafficSigns.Web.Features.Accounts.Commands;

namespace TrafficSigns.UnitTests.Features.Accounts.Commands;

public class ReactivateAccountHandlerTests
{
    private readonly AppDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IPermissionService _permissionService;
    private readonly ReactivateAccountHandler _handler;

    public ReactivateAccountHandlerTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _db = new AppDbContext(options);

        _currentUser = Substitute.For<ICurrentUserService>();
        _permissionService = Substitute.For<IPermissionService>();
        _handler = new ReactivateAccountHandler(_db, _currentUser, _permissionService);
    }

    [Fact]
    public async Task Case1_Handle_ShouldThrowUnauthorized_WhenUserIsNotAdmin()
    {
        // Arrange
        _permissionService.IsAdmin().Returns(false);
        var command = new ReactivateAccountCommand(Guid.NewGuid());

        // Act
        var act = () => _handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<UnauthorizedAccessException>().WithMessage("Access denied.");
    }

    [Fact]
    public async Task Case2_Handle_ShouldThrowException_WhenAccountNotFound()
    {
        // Arrange
        _permissionService.IsAdmin().Returns(true);
        var command = new ReactivateAccountCommand(Guid.NewGuid());

        // Act
        var act = () => _handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<Exception>().WithMessage("Invalid Account.");
    }

    [Fact]
    public async Task Case3_Handle_ShouldThrowException_WhenAccountIsAlreadyActive()
    {
        // Arrange
        var accId = Guid.NewGuid();
        _db.Accounts.Add(new Account { Id = accId, IsDeleted = false });
        await _db.SaveChangesAsync();

        _permissionService.IsAdmin().Returns(true);
        var command = new ReactivateAccountCommand(accId);

        // Act
        var act = () => _handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<Exception>().WithMessage("Account is already active.");
    }

    [Fact]
    public async Task Case4_Handle_ShouldReactivateAccount_WhenSuccess()
    {
        // Arrange
        var accId = Guid.NewGuid();
        _db.Accounts.Add(new Account { Id = accId, IsDeleted = true }); // Đang bị khóa
        await _db.SaveChangesAsync();

        _permissionService.IsAdmin().Returns(true);
        var command = new ReactivateAccountCommand(accId);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().Be(accId);

        var account = await _db.Accounts.FirstAsync(a => a.Id == accId);
        account.IsDeleted.Should().BeFalse();
        account.UpdatedDt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task Case5_Handle_ShouldRecordAuditLog_WithCorrectActorInfo()
    {
        // Arrange
        var accId = Guid.NewGuid();
        var mockAdminId = Guid.NewGuid();
        _db.Accounts.Add(new Account { Id = accId, IsDeleted = true });
        await _db.SaveChangesAsync();

        _permissionService.IsAdmin().Returns(true);
        _currentUser.GetUsername().Returns("RootAdmin");
        _currentUser.GetUserId().Returns(mockAdminId);

        var command = new ReactivateAccountCommand(accId);

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        var account = await _db.Accounts.FirstAsync(a => a.Id == accId);
        account.Metadata.Should().ContainKey("update_history");

        var log = account.Metadata["update_history"].ToString();
        log.Should().Contain($"Reactivated by RootAdmin({mockAdminId})");
    }
}