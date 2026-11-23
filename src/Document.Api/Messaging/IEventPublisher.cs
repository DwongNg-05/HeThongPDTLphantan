namespace Document.Api.Messaging;

public interface IEventPublisher
{
    Task PublishAsync<T>(string routingKey, T @event);
}
