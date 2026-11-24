using System.Collections.Concurrent;
using Microsoft.AspNetCore.SignalR.Client;
using Shared.Contracts.Dtos;
using Shared.Contracts.Messaging;

namespace Document.Client.Services;

// Ket noi SignalR toi Notification.Service voi vai tro mot nguoi dung (userCode)
// va cho phep doi thong bao cua mot tai lieu cu the.
public sealed class NotificationListener : IAsyncDisposable
{
    private readonly HubConnection _connection;
    private readonly ConcurrentDictionary<(Guid, string), TaskCompletionSource<DocumentNotification>> _waiters = new();

    public string UserCode { get; }

    public NotificationListener(string notificationBaseUrl, string userCode)
    {
        UserCode = userCode;
        var url = $"{notificationBaseUrl.TrimEnd('/')}{MessagingConstants.NotificationHubPath}" +
                  $"?userCode={Uri.EscapeDataString(userCode)}";
        _connection = new HubConnectionBuilder().WithUrl(url).WithAutomaticReconnect().Build();

        _connection.On<DocumentNotification>(MessagingConstants.HubMethodDocumentNotification, n =>
        {
            Console.WriteLine($"  [thong bao -> {UserCode}] {n.Message}{(n.Comment is null ? "" : $" ({n.Comment})")}");
            if (_waiters.TryRemove((n.DocumentId, n.Type), out var tcs))
                tcs.TrySetResult(n);
        });
    }

    public async Task<bool> TryStartAsync()
    {
        try
        {
            await _connection.StartAsync();
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Khong ket noi duoc Notification.Service cho {UserCode}: {ex.Message}");
            return false;
        }
    }

    // Dang ky truoc khi goi API de khong bo lo thong bao den nhanh
    public Task<DocumentNotification> ExpectAsync(Guid documentId, string type) =>
        _waiters.GetOrAdd((documentId, type),
            _ => new TaskCompletionSource<DocumentNotification>(TaskCreationOptions.RunContinuationsAsynchronously)).Task;

    public ValueTask DisposeAsync() => _connection.DisposeAsync();
}
