using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using TrafficSigns.Application.Common.Interfaces;
using TrafficSigns.Domain.Models;
using TrafficSigns.Infrastructure.Persistence;
using TrafficSigns.Web.Features.Users.Commands;
using Xunit;

namespace TrafficSigns.UnitTests.Features.Users.Commands;

public class UpdateUserHandlerTests
{
    private readonly AppDbContext _db;
    private readonly IKeycloakAdminService _keycloakService;
    private readonly ICurrentUserService _currentUser;
    private readonly UpdateUserHandler _handler;

    public UpdateUserHandlerTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _db = new AppDbContext(options);

        _keycloakService = Substitute.For<IKeycloakAdminService>();
        _currentUser = Substitute.For<ICurrentUserService>();

        _handler = new UpdateUserHandler(_db, _keycloakService, _currentUser);
    }

    [Fact]
    public async Task Case1_Handle_ShouldReturnFalse_WhenUserNotFound()
    {
        // Arrange
        var command = new UpdateUserCommand(Guid.NewGuid(), "new@test.com", "0999");

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task Case2_Handle_ShouldReturnFalse_WhenUserIsInactive()
    {
        // Arrange
        var userId = Guid.NewGuid();
        _db.Users.Add(new User { Id = userId, Username = "old", IsDeleted = true });
        await _db.SaveChangesAsync();

        var command = new UpdateUserCommand(userId, "new@test.com", "0999");

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task Case3_Handle_ShouldThrowException_WhenEmailIsTakenByAnotherUser()
    {
        // Arrange
        var targetUserId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        var commonEmail = "taken@test.com";

        _db.Users.Add(new User { Id = targetUserId, Username = "target", Email = "old@test.com", Phone = "1" });
        _db.Users.Add(new User { Id = otherUserId, Username = "other", Email = commonEmail, Phone = "2" });
        await _db.SaveChangesAsync();

        var command = new UpdateUserCommand(targetUserId, commonEmail, "3");

        // Act
        var act = () => _handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<Exception>().WithMessage("Email is already taken by another user.");
    }

    [Fact]
    public async Task Case4_Handle_ShouldThrowException_WhenPhoneIsTakenByAnotherUser()
    {
        // Arrange
        var targetUserId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        var commonPhone = "0988888888";

        _db.Users.Add(new User { Id = targetUserId, Username = "target", Email = "1@test.com", Phone = "111" });
        _db.Users.Add(new User { Id = otherUserId, Username = "other", Email = "2@test.com", Phone = commonPhone });
        await _db.SaveChangesAsync();

        var command = new UpdateUserCommand(targetUserId, "new@test.com", commonPhone);

        // Act
        var act = () => _handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<Exception>().WithMessage("Phone Number is already taken by another user.");
    }

    [Fact]
    public async Task Case5_Handle_ShouldInvokeKeycloakUpdate_WhenDataIsValid()
    {
        // Arrange
        var userId = Guid.NewGuid();
        _db.Users.Add(new User { Id = userId, Username = "user1", Email = "old@test.com", Phone = "123" });
        await _db.SaveChangesAsync();

        var command = new UpdateUserCommand(userId, "updated@test.com", "999", "NewFirst", "NewLast");

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        await _keycloakService.Received(1).UpdateUserAsync(
            userId,
            "updated@test.com",
            "NewFirst",
            "NewLast");
    }

    [Fact]
    public async Task Case6_Handle_ShouldUpdateLocalDatabaseFields_WhenSuccess()
    {
        // Arrange
        var userId = Guid.NewGuid();
        _db.Users.Add(new User { Id = userId, Username = "user1", Email = "old@test.com", Phone = "123" });
        await _db.SaveChangesAsync();

        var command = new UpdateUserCommand(userId, "   new@test.com   ", "   0123   ", "  First  ", "  Last  ");

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().BeTrue();
        var user = await _db.Users.FindAsync(userId);
        user!.Email.Should().Be("new@test.com");
        user.Phone.Should().Be("0123");
        user.FirstName.Should().Be("First");
        user.LastName.Should().Be("Last");
        user.UpdatedDt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task Case7_Handle_ShouldUpdateMetadataLog_WithCorrectActor()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var adminId = Guid.NewGuid();
        _db.Users.Add(new User { Id = userId, Username = "user1", Email = "old@test.com", Phone = "123" });
        await _db.SaveChangesAsync();

        _currentUser.GetUsername().Returns("AdminUser");
        _currentUser.GetUserId().Returns(adminId);

        var command = new UpdateUserCommand(userId, "new@test.com", "0123");

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        var user = await _db.Users.FindAsync(userId);
        user!.Metadata.Should().ContainKey("update_history");
        user.Metadata["update_history"].Should().Contain($"Updated by AdminUser({adminId})");
    }
}