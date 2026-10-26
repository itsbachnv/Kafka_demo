using Confluent.Kafka;
using Microsoft.Extensions.Options;

namespace NotificationService;

/// <summary>Consumer Kafka: đọc user.registered, giao cho handler, commit offset sau khi xử lý xong (at-least-once).</summary>
public sealed class UserRegisteredConsumer : BackgroundService
{
    private readonly KafkaOptions _kafka;
    private readonly RabbitNotificationPublisher _rabbit;
    private readonly UserRegisteredHandler _handler;
    private readonly ILogger<UserRegisteredConsumer> _logger;

    public UserRegisteredConsumer(
        IOptions<KafkaOptions> kafka,
        RabbitNotificationPublisher rabbit,
        UserRegisteredHandler handler,
        ILogger<UserRegisteredConsumer> logger)
    {
        _kafka = kafka.Value;
        _rabbit = rabbit;
        _handler = handler;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Yield(); // không chặn việc khởi động host

        try
        {
            await _rabbit.ConnectAsync(stoppingToken);
            await KafkaTopic.EnsureAsync(_kafka, _logger, stoppingToken);

            using var consumer = new ConsumerBuilder<string, string>(new ConsumerConfig
            {
                BootstrapServers = _kafka.BootstrapServers,
                GroupId = _kafka.GroupId,
                AutoOffsetReset = AutoOffsetReset.Earliest,
                EnableAutoCommit = false,
            }).Build();

            consumer.Subscribe(_kafka.Topic);
            _logger.LogInformation("notification-service started, consuming {Topic}", _kafka.Topic);

            while (!stoppingToken.IsCancellationRequested)
            {
                ConsumeResult<string, string>? result;
                try
                {
                    result = consumer.Consume(stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (ConsumeException ex)
                {
                    _logger.LogWarning("consume_error: {Reason}", ex.Error.Reason);
                    continue;
                }

                if (result?.Message is null) continue;

                try
                {
                    await _handler.HandleAsync(result.Message.Value, stoppingToken);
                    consumer.Commit(result);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    // Lỗi tạm thời (ví dụ RabbitMQ): quay lại đúng offset này và thử lại sau 2 giây.
                    _logger.LogError(ex, "handle_failed offset={Offset}, retrying", result.TopicPartitionOffset);
                    consumer.Seek(result.TopicPartitionOffset);
                    try { await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken); }
                    catch (OperationCanceledException) { break; }
                }
            }

            consumer.Close();
        }
        catch (OperationCanceledException)
        {
            // đang tắt
        }
        catch (Exception ex)
        {
            _logger.LogCritical(ex, "notification consumer crashed");
            throw; // để host dừng và Docker restart service
        }
    }
}
