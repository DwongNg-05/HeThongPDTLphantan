namespace Shared.Contracts.Messaging;

public static class MessagingConstants
{
    public const string ExchangeName = "document.events";              // topic exchange
    public const string NotificationQueue = "notification.document-events";

    public const string RoutingKeySubmitted = "document.submitted";
    public const string RoutingKeyApproved = "document.approved";
    public const string RoutingKeyRejected = "document.rejected";
    public const string RoutingKeyAll = "document.*";                   // binding cho Notification.Service

    // SignalR
    public const string NotificationHubPath = "/hubs/notifications";
    public const string HubMethodDocumentNotification = "DocumentNotification";
    public const string UserGroupPrefix = "user:";                     // moi nguoi dung 1 group: user:{ma}
}
