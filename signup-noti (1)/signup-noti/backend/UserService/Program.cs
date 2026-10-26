using UserService;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<KafkaOptions>(builder.Configuration.GetSection("Kafka"));
builder.Services.AddSingleton<IUserRepository, InMemoryUserRepository>();
builder.Services.AddSingleton<IPasswordHasher, Pbkdf2PasswordHasher>();
builder.Services.AddSingleton<IEventPublisher, KafkaEventPublisher>();
builder.Services.AddHostedService<KafkaTopicInitializer>();

var app = builder.Build();

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

app.MapPost("/api/users/register", async (
    RegisterRequest? request,
    IUserRepository repo,
    IPasswordHasher hasher,
    IEventPublisher publisher,
    ILogger<Program> logger) =>
{
    var errors = RegisterValidator.Validate(request);
    if (errors.Count > 0)
        return Results.BadRequest(new { errors });

    var user = new User(
        Id: Guid.NewGuid(),
        Email: request!.Email!.Trim().ToLowerInvariant(),
        FullName: request.FullName!.Trim(),
        PasswordHash: hasher.Hash(request.Password!),
        CreatedAt: DateTimeOffset.UtcNow);

    if (!await repo.TryAddAsync(user))
        return Results.Conflict(new { error = "Email đã được đăng ký" });

    var @event = new UserRegisteredEvent(
        EventId: Guid.NewGuid(),
        EventType: "user.registered",
        OccurredAt: DateTimeOffset.UtcNow,
        Data: new UserRegisteredData(user.Id, user.Email, user.FullName));

    try
    {
        await publisher.PublishAsync(user.Id.ToString(), @event);
    }
    catch (Exception ex)
    {
        // User đã lưu; ghi log để có thể gửi bù (rủi ro R1, hướng xử lý: Transactional Outbox ở v2).
        logger.LogError(ex, "publish_failed eventId={EventId} userId={UserId}", @event.EventId, user.Id);
    }

    return Results.Created($"/api/users/{user.Id}", new { userId = user.Id, email = user.Email, fullName = user.FullName });
});

app.Run();

// Cho phép WebApplicationFactory<Program> trong project test.
public partial class Program { }
