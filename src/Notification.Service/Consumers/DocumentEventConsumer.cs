using System.Text.Json;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Options;
using Notification.Service.Hubs;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Shared.Contracts.Dtos;
using Shared.Contracts.Events;
using Shared.Contracts.Messaging;

namespace Notification.Service.Consumers;

// Dich vu nen: subscribe queue MessagingConstants.NotificationQueue (bind "document.*")
// tren exchange MessagingConstants.ExchangeName, chuyen su kien thanh DocumentNotification
// va day toi nguoi nhan qua SignalR:
//   document.submitted -> nguoi phe duyet (ApproverCode)
//   document.approved / document.rejected -> nguoi tao (CreatedBy)
public class DocumentEventConsumer(
    IHubContext<NotificationHub> hub,
    IOptions<RabbitMqOptions> options,
    ILogger<DocumentEventConsumer> logger) : BackgroundService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(5);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Lap lai cho den khi ket noi duoc (RabbitMQ co the khoi dong cham hon service)
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ConsumeAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogWarning("Mat ket noi RabbitMQ ({Message}), thu lai sau {Delay}s...",
                    ex.Message, RetryDelay.TotalSeconds);
                await Task.Delay(RetryDelay, stoppingToken).ContinueWith(_ => { });
            }
        }
    }

    private async Task ConsumeAsync(CancellationToken stoppingToken)
    {
        var o = options.Value;
        var factory = new ConnectionFactory
        {
            HostName = o.HostName,
            Port = o.Port,
            UserName = o.UserName,
            Password = o.Password,
            VirtualHost = o.VirtualHost,
            ClientProvidedName = "Notification.Service"
        };

        await using var connection = await factory.CreateConnectionAsync(stoppingToken);
        await using var channel = await connection.CreateChannelAsync(cancellationToken: stoppingToken);

        await channel.ExchangeDeclareAsync(MessagingConstants.ExchangeName, ExchangeType.Topic,
            durable: true, cancellationToken: stoppingToken);
        await channel.QueueDeclareAsync(MessagingConstants.NotificationQueue,
            durable: true, exclusive: false, autoDelete: false, cancellationToken: stoppingToken);
        await channel.QueueBindAsync(MessagingConstants.NotificationQueue, MessagingConstants.ExchangeName,
            MessagingConstants.RoutingKeyAll, cancellationToken: stoppingToken);
        await channel.BasicQosAsync(prefetchSize: 0, prefetchCount: 10, global: false, stoppingToken);

        var consumer = new AsyncEventingBasicConsumer(channel);
        consumer.ReceivedAsync += (_, ea) => HandleAsync(channel, ea, stoppingToken);

        await channel.BasicConsumeAsync(MessagingConstants.NotificationQueue, autoAck: false,
            consumer: consumer, cancellationToken: stoppingToken);
        logger.LogInformation("Dang lang nghe queue {Queue}", MessagingConstants.NotificationQueue);

        // Giu ket noi cho den khi dung service hoac connection bi dong
        var closed = new TaskCompletionSource();
        connection.ConnectionShutdownAsync += (_, _) => { closed.TrySetResult(); return Task.CompletedTask; };
        await using (stoppingToken.Register(() => closed.TrySetResult()))
            await closed.Task;

        if (!stoppingToken.IsCancellationRequested)
            throw new InvalidOperationException("Ket noi RabbitMQ da dong.");
    }

    private async Task HandleAsync(IChannel channel, BasicDeliverEventArgs ea, CancellationToken ct)
    {
        DocumentNotification? notification;
        try
        {
            notification = ToNotification(ea.RoutingKey, ea.Body);
        }
        catch (JsonException ex)
        {
            // Thong diep hong: khong requeue de tranh lap vo han
            logger.LogError(ex, "Thong diep {RoutingKey} khong hop le, bo qua", ea.RoutingKey);
            await channel.BasicNackAsync(ea.DeliveryTag, multiple: false, requeue: false, ct);
            return;
        }

        if (notification is null)
        {
            logger.LogWarning("Bo qua routing key khong ho tro: {RoutingKey}", ea.RoutingKey);
            await channel.BasicAckAsync(ea.DeliveryTag, multiple: false, ct);
            return;
        }

        try
        {
            await hub.Clients.Group(NotificationHub.GroupFor(notification.Recipient))
                .SendAsync(MessagingConstants.HubMethodDocumentNotification, notification, ct);
            await channel.BasicAckAsync(ea.DeliveryTag, multiple: false, ct);
            logger.LogInformation("[notify] {Type} {DocumentId} -> {Recipient}",
                notification.Type, notification.DocumentId, notification.Recipient);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Loi tam thoi: thu lai 1 lan, lan sau thi bo
            logger.LogError(ex, "Khong day duoc thong bao {DocumentId}", notification.DocumentId);
            await channel.BasicNackAsync(ea.DeliveryTag, multiple: false, requeue: !ea.Redelivered, ct);
        }
    }

    // Anh xa su kien -> thong bao cho nguoi nhan phu hop
    internal static DocumentNotification? ToNotification(string routingKey, ReadOnlyMemory<byte> body)
    {
        switch (routingKey)
        {
            case MessagingConstants.RoutingKeySubmitted:
            {
                var e = Deserialize<DocumentSubmittedEvent>(body);
                return new DocumentNotification("submitted", e.DocumentId, e.Title, e.ApproverCode,
                    $"{e.CreatedBy} da gui tai lieu \"{e.Title}\" cho ban phe duyet.", null, e.OccurredAt);
            }
            case MessagingConstants.RoutingKeyApproved:
            {
                var e = Deserialize<DocumentApprovedEvent>(body);
                return new DocumentNotification("approved", e.DocumentId, e.Title, e.CreatedBy,
                    $"Tai lieu \"{e.Title}\" da duoc {e.ApproverCode} phe duyet.", e.Comment, e.OccurredAt);
            }
            case MessagingConstants.RoutingKeyRejected:
            {
                var e = Deserialize<DocumentRejectedEvent>(body);
                return new DocumentNotification("rejected", e.DocumentId, e.Title, e.CreatedBy,
                    $"Tai lieu \"{e.Title}\" bi {e.ApproverCode} tu choi.", e.Reason, e.OccurredAt);
            }
            default:
                return null;
        }
    }

    private static T Deserialize<T>(ReadOnlyMemory<byte> body) =>
        JsonSerializer.Deserialize<T>(body.Span, JsonOptions)
        ?? throw new JsonException($"Body rong cho {typeof(T).Name}");
}
