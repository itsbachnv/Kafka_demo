using EmailSenderService;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.Configure<RabbitOptions>(builder.Configuration.GetSection("RabbitMq"));
builder.Services.Configure<SmtpOptions>(builder.Configuration.GetSection("Smtp"));
builder.Services.Configure<RetryOptions>(builder.Configuration.GetSection("Retry"));

builder.Services.AddSingleton<IEmailSender, SmtpEmailSender>();
builder.Services.AddSingleton<NotificationProcessor>(sp => new NotificationProcessor(
    sp.GetRequiredService<IEmailSender>(),
    sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<RetryOptions>>(),
    sp.GetRequiredService<ILogger<NotificationProcessor>>()));
builder.Services.AddHostedService<EmailConsumer>();

builder.Build().Run();
