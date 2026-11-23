using Microsoft.AspNetCore.SignalR;
using Notification.Service.Hubs;

namespace Notification.Service.Consumers;

// Dich vu nen: subscribe queue MessagingConstants.NotificationQueue tren RabbitMQ,
// nhan su kien tai lieu va day thong bao toi may khach qua SignalR.
// TODO: cai goi RabbitMQ.Client, khai bao exchange/queue/binding va consume thong diep,
//       roi goi hub.Clients.All.SendAsync("DocumentStatusChanged", payload).
public class DocumentEventConsumer(
    IHubContext<NotificationHub> hub,
    ILogger<DocumentEventConsumer> logger) : BackgroundService
{
    private readonly IHubContext<NotificationHub> _hub = hub;

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("DocumentEventConsumer dang cho ket noi RabbitMQ...");
        return Task.CompletedTask;
    }
}
