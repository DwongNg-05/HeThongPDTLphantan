using System.Text.Json;

namespace Document.Api.Messaging;

// TODO: cai goi RabbitMQ.Client va publish len exchange MessagingConstants.ExchangeName.
// Hien tai chi ghi log de luong xu ly van chay duoc khi chua co RabbitMQ.
public class RabbitMqEventPublisher(ILogger<RabbitMqEventPublisher> logger) : IEventPublisher
{
    public Task PublishAsync<T>(string routingKey, T @event)
    {
        logger.LogInformation("[publish] {RoutingKey}: {Payload}", routingKey, JsonSerializer.Serialize(@event));
        return Task.CompletedTask;
    }
}
