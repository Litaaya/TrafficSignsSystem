using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using TrafficSigns.Application.Common.Interfaces;
using TrafficSigns.Infrastructure.Persistence;
using TrafficSigns.Web.Features.Accounts.Commands;

namespace TrafficSigns.UnitTests.Features.Accounts.Commands;

public class CreateAccountHandlerTests
{
    private readonly AppDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IPermissionService _permissionService;
    private readonly CreateAccountHandler _handler;

    public CreateAccountHandlerTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _db = new AppDbContext(options);

        _currentUser = Substitute.For<ICurrentUserService>();
        _permissionService = Substitute.For<IPermissionService>();
        _handler = new CreateAccountHandler(_db, _currentUser, _permissionService);
    }

    [Fact]
    public async Task Case1_Handle_ShouldThrowUnauthorized_WhenUserIsNotAdmin()
    {
        // Arrange
        _permissionService.IsAdmin().Returns(false);
        var command = new CreateAccountCommand("New Workspace", "Desc", "test@gmail.com", "0123456789");

        // Act
        var act = () => _handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<UnauthorizedAccessException>().WithMessage("Access denied.");
    }

    [Fact]
    public async Task Case2_Handle_ShouldCreateAccount_WhenUserIsAdmin()
    {
        // Arrange
        _permissionService.IsAdmin().Returns(true);
        var command = new CreateAccountCommand("Global Account", "Description", "contact@global.com", "0999888777");

        // Act
        var accountId = await _handler.Handle(command, CancellationToken.None);

        // Assert
        accountId.Should().NotBeEmpty();

        var account = await _db.Accounts.FirstOrDefaultAsync(a => a.Id == accountId);
        account.Should().NotBeNull();
        account!.Name.Should().Be("Global Account");
        account.Inactive.Should().BeFalse();
        account.CreatedDt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task Case3_Handle_ShouldTrimInputData_ToPreventMessyData()
    {
        // Arrange
        _permissionService.IsAdmin().Returns(true);
        var command = new CreateAccountCommand(
            "   Space Account   ",
            "   Some Description   ",
            "  email@test.com  ",
            "  0123456789  ");

        // Act
        var accountId = await _handler.Handle(command, CancellationToken.None);

        // Assert
        var account = await _db.Accounts.FirstAsync(a => a.Id == accountId);
        account.Name.Should().Be("Space Account");
        account.Desc.Should().Be("Some Description");
        account.Email.Should().Be("email@test.com");
        account.Phone.Should().Be("0123456789");
    }

    [Fact]
    public async Task Case4_Handle_ShouldRecordCorrectAuditLog_InMetadata()
    {
        // Arrange
        var mockAdminId = Guid.NewGuid();
        _permissionService.IsAdmin().Returns(true);
        _currentUser.GetUsername().Returns("SuperAdmin");
        _currentUser.GetUserId().Returns(mockAdminId);

        var command = new CreateAccountCommand("Audit Test Account", null, null, null);

        // Act
        var accountId = await _handler.Handle(command, CancellationToken.None);

        // Assert
        var account = await _db.Accounts.FirstAsync(a => a.Id == accountId);
        account.Metadata.Should().ContainKey("update_history");

        var log = account.Metadata["update_history"].ToString();
        log.Should().Contain($"Created by SuperAdmin({mockAdminId})");
    }

    [Fact]
    public async Task Case5_Handle_ShouldSetSystemFlag_Correctly()
    {
        // Arrange
        _permissionService.IsAdmin().Returns(true);
        var command = new CreateAccountCommand("System Workspace", null, null, null, true);

        // Act
        var accountId = await _handler.Handle(command, CancellationToken.None);

        // Assert
        var account = await _db.Accounts.FirstAsync(a => a.Id == accountId);
        account.System.Should().BeTrue();
    }
}