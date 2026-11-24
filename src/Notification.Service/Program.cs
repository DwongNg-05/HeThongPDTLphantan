using Notification.Service.Consumers;
using Notification.Service.Hubs;
using Shared.Contracts.Messaging;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<RabbitMqOptions>(builder.Configuration.GetSection(RabbitMqOptions.SectionName));
builder.Services.AddSignalR();
builder.Services.AddHostedService<DocumentEventConsumer>();

var app = builder.Build();

app.MapGet("/", () => Results.Ok(new { service = "Notification.Service", status = "running" }));
app.MapHub<NotificationHub>(MessagingConstants.NotificationHubPath);

app.Run();
