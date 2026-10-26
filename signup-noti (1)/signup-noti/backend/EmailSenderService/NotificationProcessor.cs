using System.Text.Json;
using Microsoft.Extensions.Options;

namespace EmailSenderService;

public enum ProcessResult
{
    /// <summary>Đã gửi xong, hoặc là bản trùng: ack.</summary>
    Ack,

    /// <summary>Hỏng hoặc hết số lần thử: nack không requeue để vào DLQ.</summary>
    Dead,
}

/// <summary>Xử lý 1 thông báo: validate, chống gửi trùng, gửi với retry và backoff luỹ thừa.</summary>
public sealed class NotificationProcessor
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IEmailSender _sender;
    private readonly ILogger<NotificationProcessor> _logger;
    private readonly RetryOptions _retry;
    private readonly Func<TimeSpan, CancellationToken, Task> _delay;
    private readonly int _maxSeen;
    private readonly HashSet<Guid> _sent = new();
    private readonly Queue<Guid> _order = new();
    private readonly object _lock = new();

    public NotificationProcessor(
        IEmailSender sender,
        IOptions<RetryOptions> retry,
        ILogger<NotificationProcessor> logger,
        Func<TimeSpan, CancellationToken, Task>? delay = null,
        int maxSeen = 10_000)
    {
        _sender = sender;
        _retry = retry.Value;
        _logger = logger;
        _delay = delay ?? ((span, ct) => Task.Delay(span, ct));
        _maxSeen = maxSeen;
    }

    public async Task<ProcessResult> ProcessAsync(string rawJson, CancellationToken ct = default)
    {
        NotificationMessage? n;
        try
        {
            n = JsonSerializer.Deserialize<NotificationMessage>(rawJson, JsonOptions);
        }
        catch (JsonException)
        {
            _logger.LogWarning("invalid_json_dead");
            return ProcessResult.Dead;
        }

        if (!IsValid(n))
        {
            _logger.LogWarning("invalid_notification_dead");
            return ProcessResult.Dead;
        }

        var id = n!.NotificationId!.Value;
        if (IsSent(id))
        {
            _logger.LogInformation("duplicate_skipped notificationId={NotificationId}", id);
            return ProcessResult.Ack;
        }

        for (var attempt = 1; attempt <= _retry.MaxAttempts; attempt++)
        {
            try
            {
                await _sender.SendAsync(n.To!, n.Subject!, n.Body!, ct);
                Remember(id);
                _logger.LogInformation(
                    "email_sent notificationId={NotificationId} sourceEventId={SourceEventId} to={To} attempt={Attempt}",
                    id, n.SourceEventId, MaskEmail(n.To!), attempt);
                return ProcessResult.Ack;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(
                    "send_failed notificationId={NotificationId} attempt={Attempt}: {Message}",
                    id, attempt, ex.Message);

                if (attempt < _retry.MaxAttempts)
                    await _delay(TimeSpan.FromMilliseconds(_retry.BaseDelayMs * Math.Pow(2, attempt - 1)), ct);
            }
        }

        _logger.LogError("moved_to_dlq notificationId={NotificationId}", id);
        return ProcessResult.Dead;
    }

    private static bool IsValid(NotificationMessage? n) =>
        n is not null
        && n.NotificationId is { } id && id != Guid.Empty
        && !string.IsNullOrWhiteSpace(n.To) && n.To.Contains('@')
        && n.Subject is not null
        && n.Body is not null;

    private bool IsSent(Guid id)
    {
        lock (_lock) return _sent.Contains(id);
    }

    private void Remember(Guid id)
    {
        lock (_lock)
        {
            if (!_sent.Add(id)) return;
            _order.Enqueue(id);
            while (_order.Count > _maxSeen) _sent.Remove(_order.Dequeue());
        }
    }

    private static string MaskEmail(string email)
    {
        var parts = email.Split('@');
        var name = parts[0];
        return $"{name[..Math.Min(2, name.Length)]}***@{(parts.Length > 1 ? parts[1] : "")}";
    }
}
