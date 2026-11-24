using Microsoft.AspNetCore.SignalR;
using Shared.Contracts.Messaging;

namespace Notification.Service.Hubs;

// May khach ket noi toi /hubs/notifications?userCode=QL001 de nhan thong bao day (push).
// Moi ket noi duoc dua vao group "user:{ma}" -> consumer chi gui toi dung nguoi nhan.
public class NotificationHub(ILogger<NotificationHub> logger) : Hub
{
    public static string GroupFor(string userCode) =>
        MessagingConstants.UserGroupPrefix + userCode.Trim().ToUpperInvariant();

    public override async Task OnConnectedAsync()
    {
        var userCode = Context.GetHttpContext()?.Request.Query["userCode"].ToString();
        if (!string.IsNullOrWhiteSpace(userCode))
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, GroupFor(userCode));
            logger.LogInformation("{UserCode} da ket noi ({ConnectionId})", userCode, Context.ConnectionId);
        }
        await base.OnConnectedAsync();
    }
}
