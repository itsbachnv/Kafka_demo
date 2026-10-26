namespace UserService;

public sealed record RegisterRequest(string? Email, string? FullName, string? Password);

public sealed record User(Guid Id, string Email, string FullName, string PasswordHash, DateTimeOffset CreatedAt);

public sealed record UserRegisteredData(Guid UserId, string Email, string FullName);

public sealed record UserRegisteredEvent(
    Guid EventId,
    string EventType,
    DateTimeOffset OccurredAt,
    UserRegisteredData Data);

public sealed class KafkaOptions
{
    public string BootstrapServers { get; set; } = "localhost:29092";
    public string Topic { get; set; } = "user.registered";
    public int Partitions { get; set; } = 3;

    /// <summary>Tự tạo topic khi khởi động (tắt trong test).</summary>
    public bool CreateTopicOnStartup { get; set; } = true;
}
