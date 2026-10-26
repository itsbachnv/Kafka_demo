using System.Text.Json;
using Confluent.Kafka;
using Microsoft.Extensions.Options;

namespace UserService;

public interface IEventPublisher
{
    Task PublishAsync(string key, UserRegisteredEvent @event, CancellationToken ct = default);
}

public sealed class KafkaEventPublisher : IEventPublisher, IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IProducer<string, string> _producer;
    private readonly string _topic;

    public KafkaEventPublisher(IOptions<KafkaOptions> options)
    {
        var o = options.Value;
        _topic = o.Topic;
        _producer = new ProducerBuilder<string, string>(new ProducerConfig
        {
            BootstrapServers = o.BootstrapServers,
            Acks = Acks.All,
            EnableIdempotence = true,
            MessageTimeoutMs = 10_000,
        }).Build();
    }

    public async Task PublishAsync(string key, UserRegisteredEvent @event, CancellationToken ct = default)
    {
        var json = JsonSerializer.Serialize(@event, JsonOptions);
        await _producer.ProduceAsync(_topic, new Message<string, string> { Key = key, Value = json }, ct);
    }

    public void Dispose()
    {
        _producer.Flush(TimeSpan.FromSeconds(5));
        _producer.Dispose();
    }
}
