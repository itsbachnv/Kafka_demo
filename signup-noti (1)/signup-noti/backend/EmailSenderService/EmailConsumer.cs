using System.Text;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace EmailSenderService;

public static class RabbitTopology
{
    public const string Exchange = "notification";
    public const string Dlx = "notification.dlx";
    public const string Queue = "notification.email";
    public const string Dlq = "notification.email.dlq";
    public const string RoutingKey = "email";
    public const string DeadRoutingKey = "email.dead";

    // Topology phải GIỐNG HỆT với NotificationService.
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

/// <summary>Consumer RabbitMQ: ack khi gửi xong, nack không requeue (vào DLQ) khi hỏng hoặc hết số lần thử.</summary>
public sealed class EmailConsumer : BackgroundService
{
    private readonly RabbitOptions _options;
    private readonly NotificationProcessor _processor;
    private readonly ILogger<EmailConsumer> _logger;

    public EmailConsumer(
        IOptions<RabbitOptions> options,
        NotificationProcessor processor,
        ILogger<EmailConsumer> logger)
    {
        _options = options.Value;
        _processor = processor;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Yield(); // không chặn việc khởi động host

        try
        {
            await using var connection = await ConnectWithRetryAsync(stoppingToken);
            await using var channel = await connection.CreateChannelAsync(cancellationToken: stoppingToken);

            await RabbitTopology.DeclareAsync(channel, stoppingToken);
            await channel.BasicQosAsync(prefetchSize: 0, prefetchCount: _options.Prefetch, global: false, cancellationToken: stoppingToken);

            var consumer = new AsyncEventingBasicConsumer(channel);
            consumer.ReceivedAsync += async (_, ea) =>
            {
                ProcessResult result;
                try
                {
                    var raw = Encoding.UTF8.GetString(ea.Body.Span);
                    result = await _processor.ProcessAsync(raw, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    return; // đang tắt: không ack, RabbitMQ sẽ giao lại sau
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "unexpected_error");
                    result = ProcessResult.Dead;
                }

                if (result == ProcessResult.Ack)
                    await channel.BasicAckAsync(ea.DeliveryTag, multiple: false);
                else
                    await channel.BasicNackAsync(ea.DeliveryTag, multiple: false, requeue: false);
            };

            await channel.BasicConsumeAsync(RabbitTopology.Queue, autoAck: false, consumer: consumer, cancellationToken: stoppingToken);
            _logger.LogInformation("email-sender-service started, consuming {Queue}", RabbitTopology.Queue);

            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            // đang tắt
        }
        catch (Exception ex)
        {
            _logger.LogCritical(ex, "email consumer crashed");
            throw; // để host dừng và Docker restart service
        }
    }

    private async Task<IConnection> ConnectWithRetryAsync(CancellationToken ct)
    {
        var factory = new ConnectionFactory { Uri = new Uri(_options.Url) };

        for (var attempt = 1; ; attempt++)
        {
            try
            {
                return await factory.CreateConnectionAsync(ct);
            }
            catch (Exception ex) when (attempt < 30 && !ct.IsCancellationRequested)
            {
                _logger.LogWarning("rabbitmq_not_ready attempt={Attempt}: {Message}", attempt, ex.Message);
                await Task.Delay(TimeSpan.FromSeconds(2), ct);
            }
        }
    }
}
