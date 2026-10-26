namespace EmailSenderService;

// Các trường nullable để phát hiện message thiếu trường thay vì crash khi deserialize.
public sealed record NotificationMessage(
    Guid? NotificationId,
    Guid? SourceEventId,
    string? Type,
    string? To,
    string? Subject,
    string? Body);

public sealed class RabbitOptions
{
    public string Url { get; set; } = "amqp://guest:guest@localhost:5672";
    public ushort Prefetch { get; set; } = 10;
}

public sealed class SmtpOptions
{
    public string Host { get; set; } = "localhost";
    public int Port { get; set; } = 1025;
    public string From { get; set; } = "no-reply@example.com";
}

public sealed class RetryOptions
{
    public int MaxAttempts { get; set; } = 3;
    public int BaseDelayMs { get; set; } = 1000;
}
