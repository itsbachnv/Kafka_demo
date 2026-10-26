using System.Text.Json;

namespace NotificationService;

public enum HandleResult
{
    Queued,
    Duplicate,
    Skipped,
}

public interface INotificationPublisher
{
    Task PublishAsync(NotificationMessage message, CancellationToken ct = default);
}

/// <summary>
/// Xử lý 1 message từ Kafka.
/// - Message hỏng/sai cấu trúc: bỏ qua (không throw) để không kẹt consumer.
/// - Publish lỗi: throw để consumer thử lại; eventId chưa bị đánh dấu đã xử lý.
/// </summary>
public sealed class UserRegisteredHandler
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly INotificationPublisher _publisher;
    private readonly ILogger<UserRegisteredHandler> _logger;
    private readonly int _maxSeen;
    private readonly HashSet<Guid> _seen = new();
    private readonly Queue<Guid> _order = new();
    private readonly object _lock = new();

    public UserRegisteredHandler(
        INotificationPublisher publisher,
        ILogger<UserRegisteredHandler> logger,
        int maxSeen = 10_000)
    {
        _publisher = publisher;
        _logger = logger;
        _maxSeen = maxSeen;
    }

    public async Task<HandleResult> HandleAsync(string rawJson, CancellationToken ct = default)
    {
        UserRegisteredEvent? evt;
        try
        {
            evt = JsonSerializer.Deserialize<UserRegisteredEvent>(rawJson, JsonOptions);
        }
        catch (JsonException)
        {
            _logger.LogWarning("invalid_json_skipped");
            return HandleResult.Skipped;
        }

        if (!IsValid(evt))
        {
            _logger.LogWarning("invalid_event_skipped eventId={EventId}", evt?.EventId);
            return HandleResult.Skipped;
        }

        var eventId = evt!.EventId!.Value;
        if (IsSeen(eventId))
        {
            _logger.LogInformation("duplicate_skipped eventId={EventId}", eventId);
            return HandleResult.Duplicate;
        }

        var data = evt.Data!;
        var message = new NotificationMessage(
            NotificationId: Guid.NewGuid(),
            SourceEventId: eventId,
            Type: "WELCOME_EMAIL",
            To: data.Email!,
            Subject: $"Chào mừng {data.FullName}!",
            Body: $"Xin chào {data.FullName},\n\nCảm ơn bạn đã đăng ký tài khoản. Chúc bạn có trải nghiệm tuyệt vời!");

        await _publisher.PublishAsync(message, ct);
        Remember(eventId);

        _logger.LogInformation(
            "notification_queued eventId={EventId} notificationId={NotificationId} to={To}",
            eventId, message.NotificationId, MaskEmail(message.To));
        return HandleResult.Queued;
    }

    private static bool IsValid(UserRegisteredEvent? e) =>
        e is not null
        && e.EventType == "user.registered"
        && e.EventId is { } id && id != Guid.Empty
        && e.Data is { } d
        && !string.IsNullOrWhiteSpace(d.Email) && d.Email.Contains('@')
        && !string.IsNullOrWhiteSpace(d.FullName);

    private bool IsSeen(Guid id)
    {
        lock (_lock) return _seen.Contains(id);
    }

    private void Remember(Guid id)
    {
        lock (_lock)
        {
            if (!_seen.Add(id)) return;
            _order.Enqueue(id);
            while (_order.Count > _maxSeen) _seen.Remove(_order.Dequeue());
        }
    }

    public static string MaskEmail(string email)
    {
        var parts = email.Split('@');
        var name = parts[0];
        return $"{name[..Math.Min(2, name.Length)]}***@{(parts.Length > 1 ? parts[1] : "")}";
    }
}
