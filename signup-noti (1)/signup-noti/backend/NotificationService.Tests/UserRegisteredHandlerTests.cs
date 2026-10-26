using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace NotificationService.Tests;

public sealed class FakeNotificationPublisher : INotificationPublisher
{
    public List<NotificationMessage> Queued { get; } = new();
    public bool Fail { get; set; }

    public Task PublishAsync(NotificationMessage message, CancellationToken ct = default)
    {
        if (Fail) throw new InvalidOperationException("rabbit down");
        Queued.Add(message);
        return Task.CompletedTask;
    }
}

public class UserRegisteredHandlerTests
{
    private readonly FakeNotificationPublisher _publisher = new();
    private readonly UserRegisteredHandler _handler;

    public UserRegisteredHandlerTests() =>
        _handler = new UserRegisteredHandler(_publisher, NullLogger<UserRegisteredHandler>.Instance);

    private static string Event(Guid? eventId = null, string? email = "a@example.com", string? fullName = "Nguyen Van A") =>
        JsonSerializer.Serialize(new
        {
            eventId = eventId ?? Guid.Parse("11111111-1111-1111-1111-111111111111"),
            eventType = "user.registered",
            occurredAt = DateTimeOffset.UtcNow,
            data = new { userId = Guid.NewGuid(), email, fullName },
        });

    [Fact] // N-01
    public async Task Valid_event_creates_one_WELCOME_EMAIL_message()
    {
        var result = await _handler.HandleAsync(Event());

        Assert.Equal(HandleResult.Queued, result);
        var message = Assert.Single(_publisher.Queued);
        Assert.Equal("WELCOME_EMAIL", message.Type);
        Assert.Equal("a@example.com", message.To);
        Assert.Equal(Guid.Parse("11111111-1111-1111-1111-111111111111"), message.SourceEventId);
        Assert.NotEqual(Guid.Empty, message.NotificationId);
        Assert.Contains("Nguyen Van A", message.Subject);
    }

    [Fact] // N-02
    public async Task Same_eventId_twice_publishes_once()
    {
        var raw = Event();

        await _handler.HandleAsync(raw);
        var second = await _handler.HandleAsync(raw);

        Assert.Equal(HandleResult.Duplicate, second);
        Assert.Single(_publisher.Queued);
    }

    [Fact] // N-03
    public async Task Event_missing_email_is_skipped_without_throwing()
    {
        var result = await _handler.HandleAsync(Event(email: null));

        Assert.Equal(HandleResult.Skipped, result);
        Assert.Empty(_publisher.Queued);
    }

    [Fact] // N-04
    public async Task Publish_failure_throws_and_redelivery_still_works()
    {
        var raw = Event();
        _publisher.Fail = true;

        await Assert.ThrowsAsync<InvalidOperationException>(() => _handler.HandleAsync(raw));

        _publisher.Fail = false;
        Assert.Equal(HandleResult.Queued, await _handler.HandleAsync(raw));
        Assert.Single(_publisher.Queued);
    }

    [Fact] // N-05
    public async Task Broken_json_is_skipped_without_crashing()
    {
        var result = await _handler.HandleAsync("{not json");

        Assert.Equal(HandleResult.Skipped, result);
        Assert.Empty(_publisher.Queued);
    }
}
