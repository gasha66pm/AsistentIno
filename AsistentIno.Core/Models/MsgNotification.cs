using System;
using System.Collections.Generic;
using System.Text;

namespace AsistentIno.Models;
/// <summary>
/// Lokalna (in-app) notifikacija koja se generise pri svakoj promeni statusa zadatka.
/// </summary>
public sealed class MsgNotification
{
    public required string Message { get; init; }
    public DateTime Timestamp { get; init; } = DateTime.Now;
}
