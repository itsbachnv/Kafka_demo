using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace UserService.Tests;

public sealed class RegisterEndpointTests : IDisposable
{
    private readonly TestApp _app = new();
    private readonly HttpClient _client;

    public RegisterEndpointTests() => _client = _app.CreateClient();

    public void Dispose()
    {
        _client.Dispose();
        _app.Dispose();
    }

    private static object Valid(string email = "a@example.com") =>
        new { email, fullName = "Nguyen Van A", password = "secret123" };

    private Task<HttpResponseMessage> Post(object body) => _client.PostAsJsonAsync("/api/users/register", body);

    [Fact] // U-01
    public async Task Register_valid_returns_201_and_publishes_one_event()
    {
        var res = await Post(Valid());

        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
        var body = await res.Content.ReadFromJsonAsync<JsonElement>();
        var userId = body.GetProperty("userId").GetGuid();

        var published = Assert.Single(_app.Publisher.Published);
        Assert.Equal(userId.ToString(), published.Key);
        Assert.Equal("user.registered", published.Event.EventType);
        Assert.Equal("a@example.com", published.Event.Data.Email);
        Assert.NotEqual(Guid.Empty, published.Event.EventId);
    }

    [Fact] // U-02
    public async Task Register_invalid_email_returns_400()
    {
        var res = await Post(new { email = "not-an-email", fullName = "A", password = "secret123" });

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.Empty(_app.Publisher.Published);
    }

    [Fact] // U-03
    public async Task Register_short_password_returns_400()
    {
        var res = await Post(new { email = "a@example.com", fullName = "A", password = "1234567" });

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.Empty(_app.Publisher.Published);
    }

    [Fact] // U-04
    public async Task Register_missing_fullName_returns_400()
    {
        var res = await Post(new { email = "a@example.com", password = "secret123" });

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact] // U-05
    public async Task Register_duplicate_email_case_insensitive_returns_409()
    {
        Assert.Equal(HttpStatusCode.Created, (await Post(Valid())).StatusCode);

        var res = await Post(Valid("A@Example.com"));

        Assert.Equal(HttpStatusCode.Conflict, res.StatusCode);
        Assert.Single(_app.Publisher.Published);
    }

    [Fact] // U-06
    public async Task Response_and_event_do_not_contain_password()
    {
        var res = await Post(Valid());
        var text = await res.Content.ReadAsStringAsync()
                   + JsonSerializer.Serialize(_app.Publisher.Published.Select(p => p.Event));

        Assert.DoesNotContain("secret123", text);
        Assert.DoesNotContain("password", text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact] // U-07
    public async Task Kafka_failure_still_returns_201_and_logs_publish_failed()
    {
        _app.Publisher.Fail = true;

        var res = await Post(Valid());

        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
        Assert.Contains(_app.Logs.Messages, m => m.Contains("publish_failed"));
    }

    [Fact] // U-08
    public async Task Health_returns_200()
    {
        var res = await _client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
    }
}
