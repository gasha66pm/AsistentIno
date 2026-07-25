using AsistentIno.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace AsistentIno.Services;

public sealed class NotificationService : INotificationService
{
    public event EventHandler<MsgNotification>? NotificationRaised;

    public void Notify( string message)
    {
        var notification = new MsgNotification
        {
            Message = message
        };

        NotificationRaised?.Invoke(this, notification);
    }
}