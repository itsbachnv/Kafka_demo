using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace NotificationService;

public static class RabbitTopology
{
    public const string Exchange = "notification";
    public const string Dlx = "notification.dlx";
    public const string Queue = "notification.email";
    public const string Dlq = "notification.email.dlq";
    public const string RoutingKey = "email";
    public const string DeadRoutingKey = "email.dead";

    // Topology phải GIỐNG HỆT với EmailSenderService.
    public static async Task DeclareAsync(IChannel ch, CancellationToken ct)
    {
        await ch.ExchangeDeclareAsync(Dlx, ExchangeType.Direct, durable: true, cancellationToken: ct);
        await ch.QueueDeclareAsync(Dlq, durable: true, exclusive: false, autoDelete: false, cancellationToken: ct);
        await ch.QueueBindAsync(Dlq, Dlx, DeadRoutingKey, cancellationToken: ct);

        await ch.ExchangeDeclareAsync(Exchange, ExchangeType.Direct, durable: true, cancellationToken: ct);
        await ch.QueueDeclareAsync(
            Queue,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: new Dictionary<string, object?>
            {
                ["x-dead-letter-exchange"] = Dlx,
                ["x-dead-letter-routing-key"] = DeadRoutingKey,
            },
            cancellationToken: ct);
        await ch.QueueBindAsync(Queue, Exchange, RoutingKey, cancellationToken: ct);
    }
}

public sealed class RabbitNotificationPublisher : INotificationPublisher, IAsyncDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly RabbitOptions _options;
    private readonly ILogger<RabbitNotificationPublisher> _logger;
    private IConnection? _connection;
    private IChannel? _channel;

    public RabbitNotificationPublisher(IOptions<RabbitOptions> options, ILogger<RabbitNotificationPublisher> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    /// <summary>Kết nối RabbitMQ, thử lại tối đa 30 lần cho tới khi broker sẵn sàng.</summary>
    public async Task ConnectAsync(CancellationToken ct)
    {
        var factory = new ConnectionFactory { Uri = new Uri(_options.Url) };

        for (var attempt = 1; ; attempt++)
        {
            try
            {
                _connection = await factory.CreateConnectionAsync(ct);
                _channel = await _connection.CreateChannelAsync(
                    new CreateChannelOptions(
                        publisherConfirmationsEnabled: true,
                        publisherConfirmationTrackingEnabled: true),
                    ct);
                await RabbitTopology.DeclareAsync(_channel, ct);
                _logger.LogInformation("Connected to RabbitMQ");
                return;
            }
            catch (Exception ex) when (attempt < 30 && !ct.IsCancellationRequested)
            {
                _logger.LogWarning("rabbitmq_not_ready attempt={Attempt}: {Message}", attempt, ex.Message);
                await Task.Delay(TimeSpan.FromSeconds(2), ct);
            }
        }
    }

    public async Task PublishAsync(NotificationMessage message, CancellationToken ct = default)
    {
        var channel = _channel ?? throw new InvalidOperationException("RabbitMQ chưa kết nối");
        var body = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(message, JsonOptions));
        var props = new BasicProperties
        {
            Persistent = true,
            ContentType = "application/json",
            MessageId = message.NotificationId.ToString(),
        };

        // Với publisher confirms + tracking, await trả về khi broker xác nhận; nack sẽ ném exception.
        await channel.BasicPublishAsync(
            exchange: RabbitTopology.Exchange,
            routingKey: RabbitTopology.RoutingKey,
            mandatory: true,
            basicProperties: props,
            body: body,
            cancellationToken: ct);
    }

    public async ValueTask DisposeAsync()
    {
        if (_channel is not null) await _channel.CloseAsync();
        if (_connection is not null) await _connection.CloseAsync();
    }
}
