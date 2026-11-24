namespace Shared.Contracts.Dtos;

// Thong bao Notification.Service day toi may khach qua SignalR
// (method MessagingConstants.HubMethodDocumentNotification)
public record DocumentNotification(
    string Type,           // submitted | approved | rejected
    Guid DocumentId,
    string Title,
    string Recipient,      // ma nguoi nhan thong bao
    string Message,
    string? Comment,
    DateTimeOffset OccurredAt);
