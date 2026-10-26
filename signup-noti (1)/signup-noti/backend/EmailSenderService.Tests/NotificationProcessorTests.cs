using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace EmailSenderService.Tests;

public sealed class FakeEmailSender : IEmailSender
{
    public List<(string To, string Subject, string Body)> Sent { get; } = new();
    public int Calls { get; private set; }

    /// <summary>Số lần gọi đầu tiên sẽ ném lỗi (int.MaxValue = luôn lỗi).</summary>
    public int FailFirst { get; set; }

    public Task SendAsync(string to, string subject, string body, CancellationToken ct = default)
    {
        Calls++;
        if (Calls <= FailFirst) throw new InvalidOperationException("smtp down");
        Sent.Add((to, subject, body));
        return Task.CompletedTask;
    }
}

public class NotificationProcessorTests
{
    private readonly FakeEmailSender _sender = new();
    private readonly List<TimeSpan> _delays = new();
    private readonly NotificationProcessor _processor;

    public NotificationProcessorTests()
    {
        _processor = new NotificationProcessor(
            _sender,
            Options.Create(new RetryOptions { MaxAttempts = 3, BaseDelayMs = 1000 }),
            NullLogger<NotificationProcessor>.Instance,
            delay: (span, _) =>
            {
                _delays.Add(span);
                return Task.CompletedTask;
            });
    }

    private static string Notification(Guid? id = null, string? to = "a@example.com") =>
        JsonSerializer.Serialize(new
        {
            notificationId = id ?? Guid.Parse("22222222-2222-2222-2222-222222222222"),
            sourceEventId = Guid.NewGuid(),
            type = "WELCOME_EMAIL",
            to,
            subject = "Chào mừng",
            body = "Xin chào",
        });

    [Fact] // E-01
    public async Task Successful_send_returns_Ack()
    {
        var result = await _processor.ProcessAsync(Notification());

        Assert.Equal(ProcessResult.Ack, result);
        var sent = Assert.Single(_sender.Sent);
        Assert.Equal("a@example.com", sent.To);
    }

    [Fact] // E-02
    public async Task Fails_once_then_succeeds_returns_Ack_after_one_retry()
    {
        _sender.FailFirst = 1;

        var result = await _processor.ProcessAsync(Notification());

        Assert.Equal(ProcessResult.Ack, result);
        Assert.Equal(2, _sender.Calls);
        Assert.Equal(new[] { TimeSpan.FromMilliseconds(1000) }, _delays);
    }

    [Fact] // E-03
    public async Task Fails_every_time_returns_Dead_after_three_attempts()
    {
        _sender.FailFirst = int.MaxValue;

        var result = await _processor.ProcessAsync(Notification());

        Assert.Equal(ProcessResult.Dead, result);
        Assert.Equal(3, _sender.Calls);
        Assert.Equal(
            new[] { TimeSpan.FromMilliseconds(1000), TimeSpan.FromMilliseconds(2000) },
            _delays);
    }

    [Fact] // E-04
    public async Task Same_notificationId_twice_sends_only_one_email()
    {
        var raw = Notification();

        Assert.Equal(ProcessResult.Ack, await _processor.ProcessAsync(raw));
        Assert.Equal(ProcessResult.Ack, await _processor.ProcessAsync(raw));

        Assert.Single(_sender.Sent);
    }

    [Fact] // E-05
    public async Task Invalid_or_broken_message_returns_Dead()
    {
        Assert.Equal(ProcessResult.Dead, await _processor.ProcessAsync(Notification(to: null)));
        Assert.Equal(ProcessResult.Dead, await _processor.ProcessAsync("{oops"));
        Assert.Empty(_sender.Sent);
    }
}
