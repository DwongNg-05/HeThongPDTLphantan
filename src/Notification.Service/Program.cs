using Notification.Service.Consumers;
using Notification.Service.Hubs;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSignalR();
builder.Services.AddHostedService<DocumentEventConsumer>();

var app = builder.Build();

app.MapGet("/", () => Results.Ok(new { service = "Notification.Service", status = "running" }));
app.MapHub<NotificationHub>("/hubs/notifications");

app.Run();
