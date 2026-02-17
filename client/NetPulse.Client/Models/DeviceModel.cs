using System;

namespace NetPulse.Client.Models;

public sealed class DeviceModel
{
    public string Ip { get; set; } = string.Empty;
    public string Mac { get; set; } = string.Empty;
    public string? Hostname { get; set; }
    public string? Vendor { get; set; }
    public DateTimeOffset FirstSeen { get; set; }
    public DateTimeOffset LastSeen { get; set; }
    public bool IsNew { get; set; }
}
