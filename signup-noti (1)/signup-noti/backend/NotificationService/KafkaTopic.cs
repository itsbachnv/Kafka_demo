using Confluent.Kafka;
using Confluent.Kafka.Admin;

namespace NotificationService;

public static class KafkaTopic
{
    /// <summary>Tạo topic nếu chưa có; thử lại tối đa 30 lần cho tới khi broker sẵn sàng.</summary>
    public static async Task EnsureAsync(KafkaOptions o, ILogger logger, CancellationToken ct)
    {
        for (var attempt = 1; attempt <= 30 && !ct.IsCancellationRequested; attempt++)
        {
            try
            {
                using var admin = new AdminClientBuilder(new AdminClientConfig
                {
                    BootstrapServers = o.BootstrapServers,
                }).Build();

                await admin.CreateTopicsAsync(
                    new[] { new TopicSpecification { Name = o.Topic, NumPartitions = o.Partitions, ReplicationFactor = 1 } },
                    new CreateTopicsOptions { RequestTimeout = TimeSpan.FromSeconds(10) });

                logger.LogInformation("Kafka topic {Topic} created", o.Topic);
                return;
            }
            catch (CreateTopicsException ex) when (ex.Results.All(r => r.Error.Code == ErrorCode.TopicAlreadyExists))
            {
                logger.LogInformation("Kafka topic {Topic} already exists", o.Topic);
                return;
            }
            catch (Exception ex)
            {
                logger.LogWarning("Kafka not ready (attempt {Attempt}): {Message}", attempt, ex.Message);
                try { await Task.Delay(TimeSpan.FromSeconds(2), ct); }
                catch (OperationCanceledException) { return; }
            }
        }
    }
}
