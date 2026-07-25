using AsistentIno.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace AsistentIno.Services;
/// <summary>
/// Servis koji generise lokalne (in-app) notifikacije o promenama
/// statusa zadataka. UI (MainViewModel) se pretplacuje na dogadjaj
/// kako bi prikazao obavestenje korisniku.
/// </summary>
public interface INotificationService
{
    event EventHandler<MsgNotification>? NotificationRaised;

    void Notify(string message);
}
