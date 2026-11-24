using System.Text.Json;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using Shared.Contracts.Messaging;

namespace Document.Api.Messaging;

// Publish su kien len topic exchange MessagingConstants.ExchangeName.
// - Ket noi lazy, tu ket noi lai neu RabbitMQ bi mat ket noi.
// - Bat publisher confirm: chi coi la gui thanh cong khi broker xac nhan.
// - Thong diep persistent (DeliveryMode = 2) de khong mat khi broker khoi dong lai.
public sealed class RabbitMqEventPublisher(
    IOptions<RabbitMqOptions> options,
    ILogger<RabbitMqEventPublisher> logger) : IEventPublisher, IAsyncDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly RabbitMqOptions _options = options.Value;
    private readonly SemaphoreSlim _lock = new(1, 1);   // IChannel khong thread-safe khi publish dong thoi
    private IConnection? _connection;
    private IChannel? _channel;

    public async Task PublishAsync<T>(string routingKey, T @event)
    {
        var body = JsonSerializer.SerializeToUtf8Bytes(@event, JsonOptions);
        var props = new BasicProperties
        {
            ContentType = "application/json",
            DeliveryMode = DeliveryModes.Persistent,
            MessageId = Guid.NewGuid().ToString(),
            Type = typeof(T).Name,
            Timestamp = new AmqpTimestamp(DateTimeOffset.UtcNow.ToUnixTimeSeconds())
        };

        await _lock.WaitAsync();
        try
        {
            var channel = await GetChannelAsync();
            await channel.BasicPublishAsync(MessagingConstants.ExchangeName, routingKey,
                mandatory: false, basicProperties: props, body: body);
            logger.LogInformation("[publish] {RoutingKey} ({MessageId})", routingKey, props.MessageId);
        }
        catch (Exception ex)
        {
            await ResetAsync();
            throw new EventPublishException(routingKey, ex);
        }
        finally
        {
            _lock.Release();
        }
    }

    private async Task<IChannel> GetChannelAsync()
    {
        if (_channel is { IsOpen: true }) return _channel;

        await ResetAsync();
        var factory = new ConnectionFactory
        {
            HostName = _options.HostName,
            Port = _options.Port,
            UserName = _options.UserName,
            Password = _options.Password,
            VirtualHost = _options.VirtualHost,
            ClientProvidedName = "Document.Api"
        };
        _connection = await factory.CreateConnectionAsync();
        _channel = await _connection.CreateChannelAsync(new CreateChannelOptions(
            publisherConfirmationsEnabled: true,
            publisherConfirmationTrackingEnabled: true));

        await _channel.ExchangeDeclareAsync(MessagingConstants.ExchangeName, ExchangeType.Topic, durable: true);
        return _channel;
    }

    private async Task ResetAsync()
    {
        try { if (_channel is not null) await _channel.DisposeAsync(); } catch { /* bo qua */ }
        try { if (_connection is not null) await _connection.DisposeAsync(); } catch { /* bo qua */ }
        _channel = null;
        _connection = null;
    }

    public async ValueTask DisposeAsync()
    {
        await ResetAsync();
        _lock.Dispose();
    }
}

public class EventPublishException(string routingKey, Exception inner)
    : Exception($"Khong publish duoc su kien '{routingKey}' len RabbitMQ.", inner)
{
    public string RoutingKey { get; } = routingKey;
}
