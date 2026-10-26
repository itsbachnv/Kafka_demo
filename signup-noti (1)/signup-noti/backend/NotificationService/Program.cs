using NotificationService;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.Configure<KafkaOptions>(builder.Configuration.GetSection("Kafka"));
builder.Services.Configure<RabbitOptions>(builder.Configuration.GetSection("RabbitMq"));

builder.Services.AddSingleton<RabbitNotificationPublisher>();
builder.Services.AddSingleton<INotificationPublisher>(sp => sp.GetRequiredService<RabbitNotificationPublisher>());
builder.Services.AddSingleton<UserRegisteredHandler>();
builder.Services.AddHostedService<UserRegisteredConsumer>();

builder.Build().Run();
