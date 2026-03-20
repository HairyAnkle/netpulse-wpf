using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace UyKonek.Models;

public enum DeviceType
{
    Mobile,
    Computer,
    Router,
    IoT,
    Unknown,
}

public sealed class InventoryDevice : DeviceModel, INotifyPropertyChanged
{
    private string _customName = string.Empty;
    private DeviceType _deviceType = DeviceType.Unknown;
    private string _notes = string.Empty;

    public string CustomName
    {
        get => _customName;
        set => SetProperty(ref _customName, value);
    }

    public DeviceType DeviceType
    {
        get => _deviceType;
        set => SetProperty(ref _deviceType, value);
    }

    public string Notes
    {
        get => _notes;
        set => SetProperty(ref _notes, value);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void SetProperty<T>(ref T field, T value, [CallerMemberName] string? prop = null)
    {
        if (Equals(field, value)) return;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(prop));
    }
}

public enum AlertSeverity
{
    Info,
    Warning,
    Security,
}

public sealed class AlertItem
{
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public AlertSeverity Severity { get; set; }
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;
    public bool IsRead { get; set; }
}

public sealed class PingResult
{
    public int SequenceNumber { get; set; }
    public double Latency { get; set; }
    public string Status { get; set; } = string.Empty;
    public double Height => Math.Clamp(Latency, 1, 120);
}

public sealed class TraceHop
{
    public int HopNumber { get; set; }
    public string IpAddress { get; set; } = string.Empty;
    public string Hostname { get; set; } = string.Empty;
    public string Rtt1 { get; set; } = "*";
    public string Rtt2 { get; set; } = "*";
    public string Rtt3 { get; set; } = "*";
    public string Status { get; set; } = "Timeout";
}

public enum PortScanType
{
    CommonPorts,
    Top100,
    FullScan,
    CustomRange,
}

public enum PortState
{
    Open,
    Filtered,
    Closed,
}

public enum RiskLevel
{
    Low,
    Medium,
    High,
    Critical,
}

public sealed class PortResult
{
    public int Port { get; set; }
    public string Protocol { get; set; } = "TCP";
    public string ServiceName { get; set; } = "Unknown";
    public PortState State { get; set; }
    public RiskLevel RiskLevel { get; set; }
}

public sealed class WolTarget
{
    public string CustomName { get; set; } = string.Empty;
    public string MacAddress { get; set; } = string.Empty;
    public DateTimeOffset? LastWoken { get; set; }
    public bool IsOnline { get; set; }
}

public sealed class VendorStat
{
    public string Vendor { get; set; } = string.Empty;
    public int Count { get; set; }
    public double Percent { get; set; }
}

public sealed class DeviceTypeStat
{
    public string Label { get; set; } = string.Empty;
    public int Count { get; set; }
    public double Percent { get; set; }
}
