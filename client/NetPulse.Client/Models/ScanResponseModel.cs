using System;
using System.Collections.Generic;

namespace NetPulse.Client.Models;

public sealed class ScanResponseModel
{
    public ScanMetadataModel Scan { get; set; } = new();
    public List<DeviceModel> Devices { get; set; } = new();
}

public sealed class ScanMetadataModel
{
    public int ScanId { get; set; }
    public string Subnet { get; set; } = string.Empty;
    public DateTimeOffset TsStart { get; set; }
    public DateTimeOffset TsEnd { get; set; }
    public int HostCount { get; set; }
}
