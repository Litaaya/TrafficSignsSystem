using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using TrafficSigns.Application.Common.Interfaces;
using TrafficSigns.Domain.Models;
using TrafficSigns.Infrastructure.Persistence;
using TrafficSigns.Web.Features.Accounts.Commands;

namespace TrafficSigns.UnitTests.Features.Accounts.Commands;

public class DeleteAccountHandlerTests
{
    private readonly AppDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IPermissionService _permissionService;
    private readonly DeleteAccountHandler _handler;

    public DeleteAccountHandlerTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _db = new AppDbContext(options);

        _currentUser = Substitute.For<ICurrentUserService>();
        _permissionService = Substitute.For<IPermissionService>();
        _handler = new DeleteAccountHandler(_db, _currentUser, _permissionService);
    }

    [Fact]
    public async Task Case1_Handle_ShouldThrowUnauthorized_WhenPermissionDenied()
    {
        // Arrange
        var accId = Guid.NewGuid();
        _permissionService.CanManageAccountAsync(accId).Returns(false);
        var command = new DeleteAccountCommand(accId);

        // Act
        var act = () => _handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<UnauthorizedAccessException>().WithMessage("Access denied.");
    }

    [Fact]
    public async Task Case2_Handle_ShouldThrowException_WhenAccountNotFoundOrAlreadyInactive()
    {
        // Arrange
        var accId = Guid.NewGuid();
        _db.Accounts.Add(new Account { Id = accId, IsDeleted = true }); // Đã inactive
        await _db.SaveChangesAsync();

        _permissionService.CanManageAccountAsync(accId).Returns(true);
        var command = new DeleteAccountCommand(accId);

        // Act
        var act = () => _handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<Exception>().WithMessage("Invalid Account or Account already inactive");
    }

    [Fact]
    public async Task Case3_Handle_ShouldThrowException_WhenDeletingSystemAccountWithoutAdmin()
    {
        // Arrange
        var accId = Guid.NewGuid();
        _db.Accounts.Add(new Account { Id = accId, System = true, IsDeleted = false });
        await _db.SaveChangesAsync();

        _permissionService.CanManageAccountAsync(accId).Returns(true);
        _permissionService.IsAdmin().Returns(false);

        var command = new DeleteAccountCommand(accId);

        // Act
        var act = () => _handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<Exception>().WithMessage("*administrator privileges*");
    }

    [Fact]
    public async Task Case4_Handle_ShouldDeactivateAccount_WhenSuccess()
    {
        // Arrange
        var accId = Guid.NewGuid();
        _db.Accounts.Add(new Account { Id = accId, System = false, IsDeleted = false });
        await _db.SaveChangesAsync();

        _permissionService.CanManageAccountAsync(accId).Returns(true);
        var command = new DeleteAccountCommand(accId);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().BeTrue();
        var account = await _db.Accounts.FirstAsync(a => a.Id == accId);
        account.IsDeleted.Should().BeTrue();
        account.UpdatedDt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task Case5_Handle_ShouldCascadeDeactivate_AllLinkedAccountUsers()
    {
        // Arrange
        var accId = Guid.NewGuid();
        var user1 = Guid.NewGuid();
        var user2 = Guid.NewGuid();

        var account = new Account { Id = accId, IsDeleted = false };
        _db.Accounts.Add(account);

        _db.AccountUsers.Add(new AccountUser { AccountId = accId, UserId = user1, IsDeleted = false });
        _db.AccountUsers.Add(new AccountUser { AccountId = accId, UserId = user2, IsDeleted = false });
        await _db.SaveChangesAsync();

        _permissionService.CanManageAccountAsync(accId).Returns(true);
        var command = new DeleteAccountCommand(accId);

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        var links = await _db.AccountUsers.Where(au => au.AccountId == accId).ToListAsync();
        links.Should().HaveCount(2);
        links.All(l => l.IsDeleted).Should().BeTrue();
        links.All(l => l.Metadata["update_history"].ToString().Contains("Account deactivation")).Should().BeTrue();
    }

    [Fact]
    public async Task Case6_Handle_ShouldRecordAuditLog_WithCorrectActorInfo()
    {
        // Arrange
        var accId = Guid.NewGuid();
        var mockAdminId = Guid.NewGuid();
        _db.Accounts.Add(new Account { Id = accId, IsDeleted = false });
        await _db.SaveChangesAsync();

        _permissionService.CanManageAccountAsync(accId).Returns(true);
        _currentUser.GetUsername().Returns("OwnerBoss");
        _currentUser.GetUserId().Returns(mockAdminId);

        var command = new DeleteAccountCommand(accId);

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        var account = await _db.Accounts.FirstAsync(a => a.Id == accId);
        var log = account.Metadata["update_history"].ToString();
        log.Should().Contain($"Deactivated by OwnerBoss({mockAdminId})");
    }

    [Fact]
    public async Task Case7_Handle_ShouldAllowDeleteSystemAccount_WhenUserIsAdmin()
    {
        // Arrange
        var accId = Guid.NewGuid();
        _db.Accounts.Add(new Account { Id = accId, System = true, IsDeleted = false });
        await _db.SaveChangesAsync();

        _permissionService.CanManageAccountAsync(accId).Returns(true);
        _permissionService.IsAdmin().Returns(true);

        var command = new DeleteAccountCommand(accId);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().BeTrue();
        var account = await _db.Accounts.FirstAsync(a => a.Id == accId);
        account.IsDeleted.Should().BeTrue();
    }
}