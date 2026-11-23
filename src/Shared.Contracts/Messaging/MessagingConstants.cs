namespace Shared.Contracts.Messaging;

public static class MessagingConstants
{
    public const string ExchangeName = "document.events";              // topic exchange
    public const string NotificationQueue = "notification.document-events";

    public const string RoutingKeySubmitted = "document.submitted";
    public const string RoutingKeyApproved = "document.approved";
    public const string RoutingKeyRejected = "document.rejected";
}
