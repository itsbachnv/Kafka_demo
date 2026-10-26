namespace NotificationService;

// Các trường để nullable để phát hiện message thiếu trường thay vì crash khi deserialize.
public sealed record UserRegisteredEvent(
    Guid? EventId,
    string? EventType,
    DateTimeOffset? OccurredAt,
    UserRegisteredData? Data);

public sealed record UserRegisteredData(Guid? UserId, string? Email, string? FullName);

public sealed record NotificationMessage(
    Guid NotificationId,
    Guid SourceEventId,
    string Type,
    string To,
    string Subject,
    string Body);

public sealed class KafkaOptions
{
    public string BootstrapServers { get; set; } = "localhost:29092";
    public string Topic { get; set; } = "user.registered";
    public string GroupId { get; set; } = "notification-service";
    public int Partitions { get; set; } = 3;
}

public sealed class RabbitOptions
{
    public string Url { get; set; } = "amqp://guest:guest@localhost:5672";
}
