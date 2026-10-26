using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using UserService;

namespace UserService.Tests;

public sealed class FakePublisher : IEventPublisher
{
    public List<(string Key, UserRegisteredEvent Event)> Published { get; } = new();
    public bool Fail { get; set; }

    public Task PublishAsync(string key, UserRegisteredEvent @event, CancellationToken ct = default)
    {
        if (Fail) throw new InvalidOperationException("kafka down");
        Published.Add((key, @event));
        return Task.CompletedTask;
    }
}

public sealed class ListLoggerProvider : ILoggerProvider
{
    public List<string> Messages { get; } = new();

    public ILogger CreateLogger(string categoryName) => new ListLogger(Messages);
    public void Dispose() { }

    private sealed class ListLogger(List<string> sink) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            lock (sink) sink.Add($"{logLevel}: {formatter(state, exception)}");
        }
    }
}

/// <summary>Mỗi test tạo 1 instance mới để repository luôn sạch.</summary>
public sealed class TestApp : WebApplicationFactory<Program>
{
    public FakePublisher Publisher { get; } = new();
    public ListLoggerProvider Logs { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureLogging(l => l.AddProvider(Logs));
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IEventPublisher>();
            services.AddSingleton<IEventPublisher>(Publisher);
            services.PostConfigure<KafkaOptions>(o => o.CreateTopicOnStartup = false);
        });
    }
}
