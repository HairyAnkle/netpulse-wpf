using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using UyKonek.Commands;
using UyKonek.Models;
using UyKonek.Services;

namespace UyKonek.ViewModels;

public sealed class DashboardViewModel : INotifyPropertyChanged
{
    private static readonly string InventoryFilePath = Path.Combine(AppContext.BaseDirectory, "inventory.json");
    private static readonly string AlertsFilePath = Path.Combine(AppContext.BaseDirectory, "alerts.json");
    private static readonly string WolTargetsFilePath = Path.Combine(AppContext.BaseDirectory, "wol_targets.json");

    private static readonly int[] CommonPorts = [20, 21, 22, 23, 25, 53, 80, 110, 135, 139, 143, 443, 445, 993, 995, 1433, 3306, 3389, 5900, 8080];
    private static readonly Dictionary<int, string> ServiceNames = new()
    {
        [20] = "FTP Data", [21] = "FTP", [22] = "SSH", [23] = "Telnet", [25] = "SMTP", [53] = "DNS", [67] = "DHCP", [68] = "DHCP",
        [80] = "HTTP", [110] = "POP3", [135] = "RPC", [139] = "NetBIOS", [143] = "IMAP", [443] = "HTTPS", [445] = "SMB",
        [993] = "IMAPS", [995] = "POP3S", [1433] = "MSSQL", [3306] = "MySQL", [3389] = "RDP", [5900] = "VNC", [8080] = "HTTP-Alt"
    };

    private readonly ApiClientService _apiClientService;
    private CancellationTokenSource? _scanCts;
    private CancellationTokenSource? _pingCts;
    private CancellationTokenSource? _portScanCts;
    private bool _isScanning;
    private string _statusMessage = "Ready to scan";
    private string? _errorMessage;
    private bool _isDark = true;
    private bool _backendOnline = true;
    private string _activeSection = "NETWORK SCAN";
    private DeviceModel? _selectedDevice;
    private bool _isDiagnosticBusy;
    private long _pingLatencyMs;
    private bool _pingAlive;
    private int _pingMeterValue;
    private long _scanDuration;

    private string _searchQuery = string.Empty;
    private DeviceType? _filterType;
    private string _filterStatus = "All";

    private string _selectedDeviceLatency = "—";
    private int _selectedDeviceOpenPorts;
    private double _selectedDeviceUptime;
    private double _selectedDeviceSignal;

    private string _pingTarget = "8.8.8.8";
    private int _packetCount = 4;
    private string _pingOutput = string.Empty;
    private double _pingMin;
    private double _pingMax;
    private double _pingAvg;
    private double _pingPacketLoss;
    private bool _isPingRunning;

    private string _traceTarget = "8.8.8.8";
    private string _traceStatus = "Idle";

    private string _portScanTarget = "127.0.0.1";
    private PortScanType _scanType = PortScanType.CommonPorts;
    private string _customPortRange = "1-1024";
    private bool _isPortScanRunning;
    private int _scannedCount;

    private string _wolMacAddress = string.Empty;
    private string _wolBroadcastIp = "255.255.255.255";
    private string _wolPort = "9";

    public DashboardViewModel(ApiClientService apiClientService, SettingsService settingsService)
    {
        _apiClientService = apiClientService;
        BackendUrl = settingsService.BackendBaseUrl;

        Devices = new ObservableCollection<DeviceModel>();
        Devices.CollectionChanged += OnDevicesCollectionChanged;
        VendorStats = new ObservableCollection<VendorStat>();
        DeviceTypeStats = new ObservableCollection<DeviceTypeStat>();
        ScanHistory = new ObservableCollection<int>();

        InventoryDevices = new ObservableCollection<InventoryDevice>();
        FilteredInventoryDevices = new ObservableCollection<InventoryDevice>();
        Alerts = new ObservableCollection<AlertItem>();
        PingResults = new ObservableCollection<PingResult>();
        TraceHops = new ObservableCollection<TraceHop>();
        PortResults = new ObservableCollection<PortResult>();
        WolTargets = new ObservableCollection<WolTarget>();

        ScanCommand = new AsyncRelayCommand(ScanAsync, () => !IsScanning);
        CancelCommand = new AsyncRelayCommand(CancelAsync, () => IsScanning);
        CopyIpCommand = new AsyncRelayCommand(CopyIpAsync);
        CopyMacCommand = new AsyncRelayCommand(CopyMacAsync);
        ToggleThemeCommand = new AsyncRelayCommand(ToggleThemeAsync);
        ShowNetworkScanCommand = new AsyncRelayCommand(() => SetSectionAsync("NETWORK SCAN", "Ready to scan"));
        ShowDeviceInventoryCommand = new AsyncRelayCommand(() => SetSectionAsync("DEVICE INVENTORY", "Manage inventory"));
        ShowDeviceDetailsCommand = new AsyncRelayCommand(() => SetSectionAsync("DEVICE DETAILS", "Inspect selected device"));
        ShowAlertsCommand = new AsyncRelayCommand(() => SetSectionAsync("ALERTS", "Alert feed"));
        ShowPingCommand = new AsyncRelayCommand(() => SetSectionAsync("PING", "Ping diagnostics"));
        ShowTracerouteCommand = new AsyncRelayCommand(() => SetSectionAsync("TRACEROUTE", "Trace route"));
        ShowOpenPortsCommand = new AsyncRelayCommand(() => SetSectionAsync("OPEN PORTS", "Port scan"));
        ShowWakeOnLanCommand = new AsyncRelayCommand(() => SetSectionAsync("WAKE-ON-LAN", "Wake devices"));
        UseSelectedDeviceCommand = new AsyncRelayCommand(UseSelectedDeviceAsync);
        RunDiagnosticCommand = new AsyncRelayCommand(RunDiagnosticAsync, () => !IsDiagnosticBusy);

        AddDeviceCommand = new AsyncRelayCommand(AddDeviceAsync);
        EditDeviceCommand = new AsyncRelayCommand(EditDeviceAsync, () => SelectedInventoryDevice is not null);
        DeleteDeviceCommand = new AsyncRelayCommand(DeleteDeviceAsync, () => SelectedInventoryDevice is not null);

        PingSelectedCommand = new AsyncRelayCommand(PingSelectedAsync, () => SelectedDevice is not null);
        ScanPortsForSelectedCommand = new AsyncRelayCommand(ScanPortsForSelectedAsync, () => SelectedDevice is not null);

        ClearAllAlertsCommand = new AsyncRelayCommand(ClearAllAlertsAsync);
        MarkAllReadCommand = new AsyncRelayCommand(MarkAllReadAsync);

        StartPingCommand = new AsyncRelayCommand(StartPingAsync, () => !IsPingRunning);
        StopPingCommand = new AsyncRelayCommand(StopPingAsync, () => IsPingRunning);

        StartTraceCommand = new AsyncRelayCommand(StartTraceAsync);
        StartPortScanCommand = new AsyncRelayCommand(StartPortScanAsync, () => !IsPortScanRunning);
        StopPortScanCommand = new AsyncRelayCommand(StopPortScanAsync, () => IsPortScanRunning);

        SendWolCommand = new AsyncRelayCommand(SendWolAsync);
        TestReachabilityCommand = new AsyncRelayCommand(TestReachabilityAsync);
        AddTargetCommand = new AsyncRelayCommand(AddTargetAsync);
        WakeTargetCommand = new AsyncRelayCommand(WakeSelectedTargetAsync, () => SelectedWolTarget is not null);
        RemoveTargetCommand = new AsyncRelayCommand(RemoveSelectedTargetAsync, () => SelectedWolTarget is not null);

        _isDark = App.ThemeService.IsDark;
        App.ThemeService.ThemeChanged += () =>
        {
            _isDark = App.ThemeService.IsDark;
            OnPropertyChanged(nameof(IsDark));
            OnPropertyChanged(nameof(ThemeIcon));
            OnPropertyChanged(nameof(ThemeLabel));
        };

        LoadInventory();
        LoadAlerts();
        LoadWolTargets();
        RefreshInventoryFilters();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<DeviceModel> Devices { get; }
    public ObservableCollection<VendorStat> VendorStats { get; }
    public ObservableCollection<DeviceTypeStat> DeviceTypeStats { get; }
    public ObservableCollection<int> ScanHistory { get; }

    public ObservableCollection<InventoryDevice> InventoryDevices { get; }
    public ObservableCollection<InventoryDevice> FilteredInventoryDevices { get; }
    public InventoryDevice? SelectedInventoryDevice { get; set; }

    public ObservableCollection<AlertItem> Alerts { get; }
    public ObservableCollection<PingResult> PingResults { get; }
    public ObservableCollection<TraceHop> TraceHops { get; }
    public ObservableCollection<PortResult> PortResults { get; }
    public ObservableCollection<WolTarget> WolTargets { get; }
    public WolTarget? SelectedWolTarget { get; set; }

    public AsyncRelayCommand ScanCommand { get; }
    public AsyncRelayCommand CancelCommand { get; }
    public AsyncRelayCommand CopyIpCommand { get; }
    public AsyncRelayCommand CopyMacCommand { get; }
    public AsyncRelayCommand ToggleThemeCommand { get; }
    public AsyncRelayCommand ShowNetworkScanCommand { get; }
    public AsyncRelayCommand ShowDeviceInventoryCommand { get; }
    public AsyncRelayCommand ShowDeviceDetailsCommand { get; }
    public AsyncRelayCommand ShowAlertsCommand { get; }
    public AsyncRelayCommand ShowPingCommand { get; }
    public AsyncRelayCommand ShowTracerouteCommand { get; }
    public AsyncRelayCommand ShowOpenPortsCommand { get; }
    public AsyncRelayCommand ShowWakeOnLanCommand { get; }
    public AsyncRelayCommand UseSelectedDeviceCommand { get; }
    public AsyncRelayCommand RunDiagnosticCommand { get; }
    public AsyncRelayCommand AddDeviceCommand { get; }
    public AsyncRelayCommand EditDeviceCommand { get; }
    public AsyncRelayCommand DeleteDeviceCommand { get; }
    public AsyncRelayCommand PingSelectedCommand { get; }
    public AsyncRelayCommand ScanPortsForSelectedCommand { get; }
    public AsyncRelayCommand ClearAllAlertsCommand { get; }
    public AsyncRelayCommand MarkAllReadCommand { get; }
    public AsyncRelayCommand StartPingCommand { get; }
    public AsyncRelayCommand StopPingCommand { get; }
    public AsyncRelayCommand StartTraceCommand { get; }
    public AsyncRelayCommand StartPortScanCommand { get; }
    public AsyncRelayCommand StopPortScanCommand { get; }
    public AsyncRelayCommand SendWolCommand { get; }
    public AsyncRelayCommand TestReachabilityCommand { get; }
    public AsyncRelayCommand AddTargetCommand { get; }
    public AsyncRelayCommand WakeTargetCommand { get; }
    public AsyncRelayCommand RemoveTargetCommand { get; }

    public string BackendUrl { get; }
    public bool IsDark { get => _isDark; private set => SetProperty(ref _isDark, value); }
    public string ThemeIcon => _isDark ? "☀" : "☾";
    public string ThemeLabel => _isDark ? "LIGHT MODE" : "DARK MODE";
    public bool IsScanning { get => _isScanning; private set { if (SetProperty(ref _isScanning, value)) { ScanCommand.RaiseCanExecuteChanged(); CancelCommand.RaiseCanExecuteChanged(); } } }
    public string StatusMessage { get => _statusMessage; private set => SetProperty(ref _statusMessage, value); }
    public string? ErrorMessage { get => _errorMessage; private set => SetProperty(ref _errorMessage, value); }

    public int NewDevicesCount => Devices.Count(d => d.IsNew);
    public int OnlineDevicesCount => Devices.Count;
    public long ScanDuration { get => _scanDuration; private set => SetProperty(ref _scanDuration, value); }

    public string ActiveSection { get => _activeSection; private set => SetProperty(ref _activeSection, value); }

    public DeviceModel? SelectedDevice
    {
        get => _selectedDevice;
        set
        {
            if (!SetProperty(ref _selectedDevice, value)) return;
            PingSelectedCommand.RaiseCanExecuteChanged();
            ScanPortsForSelectedCommand.RaiseCanExecuteChanged();
            _ = PopulateSelectedDeviceMetricsAsync();
        }
    }

    public string SearchQuery { get => _searchQuery; set { if (SetProperty(ref _searchQuery, value)) RefreshInventoryFilters(); } }
    public DeviceType? FilterType { get => _filterType; set { if (SetProperty(ref _filterType, value)) RefreshInventoryFilters(); } }
    public string FilterStatus { get => _filterStatus; set { if (SetProperty(ref _filterStatus, value)) RefreshInventoryFilters(); } }

    public int NamedCount => InventoryDevices.Count(d => !string.IsNullOrWhiteSpace(d.CustomName));
    public int UnnamedCount => InventoryDevices.Count - NamedCount;
    public int OnlineCount => InventoryDevices.Count;
    public int OfflineCount => 0;

    public string SelectedDeviceLatency { get => _selectedDeviceLatency; set => SetProperty(ref _selectedDeviceLatency, value); }
    public int SelectedDeviceOpenPorts { get => _selectedDeviceOpenPorts; set => SetProperty(ref _selectedDeviceOpenPorts, value); }
    public double SelectedDeviceUptime { get => _selectedDeviceUptime; set => SetProperty(ref _selectedDeviceUptime, value); }
    public double SelectedDeviceSignal { get => _selectedDeviceSignal; set => SetProperty(ref _selectedDeviceSignal, value); }

    public int TotalAlerts => Alerts.Count;
    public int NewDeviceAlerts => Alerts.Count(a => a.Severity == AlertSeverity.Warning);
    public int SecurityAlerts => Alerts.Count(a => a.Severity == AlertSeverity.Security);
    public int InfoAlerts => Alerts.Count(a => a.Severity == AlertSeverity.Info);

    public string PingTarget { get => _pingTarget; set => SetProperty(ref _pingTarget, value); }
    public int PacketCount { get => _packetCount; set => SetProperty(ref _packetCount, value); }
    public string PingOutput { get => _pingOutput; set => SetProperty(ref _pingOutput, value); }
    public double PingMin { get => _pingMin; set => SetProperty(ref _pingMin, value); }
    public double PingMax { get => _pingMax; set => SetProperty(ref _pingMax, value); }
    public double PingAvg { get => _pingAvg; set => SetProperty(ref _pingAvg, value); }
    public double PingPacketLoss { get => _pingPacketLoss; set => SetProperty(ref _pingPacketLoss, value); }
    public bool IsPingRunning { get => _isPingRunning; set { if (SetProperty(ref _isPingRunning, value)) { StartPingCommand.RaiseCanExecuteChanged(); StopPingCommand.RaiseCanExecuteChanged(); } } }

    public string TraceTarget { get => _traceTarget; set => SetProperty(ref _traceTarget, value); }
    public string TraceStatus { get => _traceStatus; set => SetProperty(ref _traceStatus, value); }

    public string PortScanTarget { get => _portScanTarget; set => SetProperty(ref _portScanTarget, value); }
    public PortScanType ScanType { get => _scanType; set => SetProperty(ref _scanType, value); }
    public string CustomPortRange { get => _customPortRange; set => SetProperty(ref _customPortRange, value); }
    public bool IsPortScanRunning { get => _isPortScanRunning; set { if (SetProperty(ref _isPortScanRunning, value)) { StartPortScanCommand.RaiseCanExecuteChanged(); StopPortScanCommand.RaiseCanExecuteChanged(); } } }
    public int ScannedCount { get => _scannedCount; set => SetProperty(ref _scannedCount, value); }
    public int OpenCount => PortResults.Count(p => p.State == PortState.Open);
    public int FilteredCount => PortResults.Count(p => p.State == PortState.Filtered);
    public int CriticalPortCount => PortResults.Count(p => p.RiskLevel == RiskLevel.Critical);
    public int FirewallCoverage => Math.Max(0, 100 - (OpenCount * 5));
    public int RiskScore => Math.Clamp(PortResults.Sum(p => p.RiskLevel switch { RiskLevel.Critical => 20, RiskLevel.High => 12, RiskLevel.Medium => 6, _ => 2 }), 0, 100);

    public string WolMacAddress { get => _wolMacAddress; set => SetProperty(ref _wolMacAddress, value); }
    public string WolBroadcastIp { get => _wolBroadcastIp; set => SetProperty(ref _wolBroadcastIp, value); }
    public string WolPort { get => _wolPort; set => SetProperty(ref _wolPort, value); }

    public bool IsDiagnosticBusy { get => _isDiagnosticBusy; set => SetProperty(ref _isDiagnosticBusy, value); }
    public long PingLatencyMs { get => _pingLatencyMs; private set => SetProperty(ref _pingLatencyMs, value); }
    public bool PingAlive { get => _pingAlive; private set => SetProperty(ref _pingAlive, value); }
    public int PingMeterValue { get => _pingMeterValue; private set => SetProperty(ref _pingMeterValue, value); }

    private async Task ScanAsync()
    {
        IsScanning = true;
        ErrorMessage = null;
        StatusMessage = "Scanning local network...";
        Devices.Clear();
        var started = Stopwatch.StartNew();
        _scanCts = new CancellationTokenSource();

        try
        {
            var result = await _apiClientService.ScanDevicesAsync(_scanCts.Token);
            foreach (var d in result.Devices)
            {
                Devices.Add(d);
                if (d.IsNew)
                {
                    AddAlert(new AlertItem
                    {
                        Title = "NEW DEVICE DETECTED",
                        Message = $"{d.Ip} ({d.Mac}) joined network",
                        Severity = AlertSeverity.Warning,
                        Timestamp = DateTimeOffset.UtcNow,
                        IsRead = false
                    });
                }
            }

            ScanDuration = started.ElapsedMilliseconds;
            ScanHistory.Add(Devices.Count);
            while (ScanHistory.Count > 8) ScanHistory.RemoveAt(0);
            ComputeStats();
            MergeInventoryFromScan();
            StatusMessage = $"Scan complete: {Devices.Count} hosts discovered";
        }
        finally
        {
            IsScanning = false;
            _scanCts?.Dispose();
            _scanCts = null;
        }
    }

    private void ComputeStats()
    {
        VendorStats.Clear();
        var groups = Devices
            .GroupBy(d => string.IsNullOrWhiteSpace(d.Vendor) ? "Unknown" : d.Vendor!)
            .OrderByDescending(g => g.Count())
            .Take(6)
            .ToList();
        foreach (var g in groups)
        {
            VendorStats.Add(new VendorStat { Vendor = g.Key, Count = g.Count(), Percent = Devices.Count == 0 ? 0 : g.Count() * 100.0 / Devices.Count });
        }

        DeviceTypeStats.Clear();
        var map = new Dictionary<string, int> { ["Mobile"] = 0, ["Computer"] = 0, ["Router"] = 0, ["IoT"] = 0, ["Unknown"] = 0 };
        foreach (var d in Devices)
        {
            map[InferType(d).ToString()]++;
        }

        foreach (var kv in map.Where(kv => kv.Value > 0))
        {
            DeviceTypeStats.Add(new DeviceTypeStat { Label = kv.Key, Count = kv.Value, Percent = Devices.Count == 0 ? 0 : kv.Value * 100.0 / Devices.Count });
        }

        OnPropertyChanged(nameof(NewDevicesCount));
    }

    private void MergeInventoryFromScan()
    {
        foreach (var device in Devices)
        {
            if (InventoryDevices.Any(i => i.Mac == device.Mac || i.Ip == device.Ip)) continue;
            InventoryDevices.Add(new InventoryDevice
            {
                Ip = device.Ip,
                Mac = device.Mac,
                Hostname = device.Hostname,
                Vendor = device.Vendor,
                FirstSeen = device.FirstSeen,
                LastSeen = device.LastSeen,
                IsNew = device.IsNew,
                DeviceType = InferType(device)
            });
        }

        SaveInventory();
        RefreshInventoryFilters();
    }

    private static DeviceType InferType(DeviceModel device)
    {
        var host = (device.Hostname ?? string.Empty).ToLowerInvariant();
        var vendor = (device.Vendor ?? string.Empty).ToLowerInvariant();
        if (host.Contains("iphone") || host.Contains("android") || vendor.Contains("apple") || vendor.Contains("samsung")) return DeviceType.Mobile;
        if (host.Contains("router") || vendor.Contains("cisco") || vendor.Contains("tp-link")) return DeviceType.Router;
        if (host.Contains("pc") || host.Contains("laptop") || vendor.Contains("dell") || vendor.Contains("hp")) return DeviceType.Computer;
        if (host.Contains("cam") || host.Contains("iot") || vendor.Contains("xiaomi")) return DeviceType.IoT;
        return DeviceType.Unknown;
    }

    private Task CancelAsync() { _scanCts?.Cancel(); return Task.CompletedTask; }
    private Task CopyIpAsync() { if (!string.IsNullOrWhiteSpace(SelectedDevice?.Ip)) System.Windows.Clipboard.SetText(SelectedDevice.Ip); return Task.CompletedTask; }
    private Task CopyMacAsync() { if (!string.IsNullOrWhiteSpace(SelectedDevice?.Mac)) System.Windows.Clipboard.SetText(SelectedDevice.Mac); return Task.CompletedTask; }
    private Task ToggleThemeAsync() { App.ThemeService.Toggle(); return Task.CompletedTask; }
    private Task SetSectionAsync(string section, string status) { ActiveSection = section; StatusMessage = status; return Task.CompletedTask; }
    private Task UseSelectedDeviceAsync() { if (SelectedDevice is not null) { PingTarget = SelectedDevice.Ip; TraceTarget = SelectedDevice.Ip; PortScanTarget = SelectedDevice.Ip; WolMacAddress = SelectedDevice.Mac; } return Task.CompletedTask; }
    private Task RunDiagnosticAsync() => Task.CompletedTask;

    private async Task PopulateSelectedDeviceMetricsAsync()
    {
        if (SelectedDevice is null) return;
        await PingSelectedAsync();
        SelectedDeviceOpenPorts = 0;
        SelectedDeviceSignal = 80;
        SelectedDeviceUptime = 99;
    }

    private async Task PingSelectedAsync()
    {
        if (SelectedDevice is null) return;
        try
        {
            var ping = await _apiClientService.PingAsync(SelectedDevice.Ip, CancellationToken.None);
            SelectedDeviceLatency = ping.Alive ? $"{ping.LatencyMs} ms" : "unreachable";
        }
        catch
        {
            SelectedDeviceLatency = "timeout";
        }
    }

    private Task ScanPortsForSelectedAsync()
    {
        if (SelectedDevice is null) return Task.CompletedTask;
        PortScanTarget = SelectedDevice.Ip;
        ActiveSection = "OPEN PORTS";
        return StartPortScanAsync();
    }

    private Task AddDeviceAsync()
    {
        InventoryDevices.Add(new InventoryDevice { CustomName = "New Device", DeviceType = DeviceType.Unknown, Notes = "", Ip = "0.0.0.0" });
        SaveInventory();
        RefreshInventoryFilters();
        return Task.CompletedTask;
    }

    private Task EditDeviceAsync()
    {
        if (SelectedInventoryDevice is null) return Task.CompletedTask;
        SelectedInventoryDevice.CustomName = string.IsNullOrWhiteSpace(SelectedInventoryDevice.CustomName) ? "Named Device" : SelectedInventoryDevice.CustomName + " *";
        SaveInventory();
        RefreshInventoryFilters();
        return Task.CompletedTask;
    }

    private Task DeleteDeviceAsync()
    {
        if (SelectedInventoryDevice is null) return Task.CompletedTask;
        InventoryDevices.Remove(SelectedInventoryDevice);
        SelectedInventoryDevice = null;
        SaveInventory();
        RefreshInventoryFilters();
        return Task.CompletedTask;
    }

    private void RefreshInventoryFilters()
    {
        FilteredInventoryDevices.Clear();
        IEnumerable<InventoryDevice> query = InventoryDevices;
        if (!string.IsNullOrWhiteSpace(SearchQuery))
        {
            query = query.Where(d => (d.CustomName?.Contains(SearchQuery, StringComparison.OrdinalIgnoreCase) ?? false)
                                 || d.Ip.Contains(SearchQuery, StringComparison.OrdinalIgnoreCase)
                                 || d.Mac.Contains(SearchQuery, StringComparison.OrdinalIgnoreCase));
        }

        if (FilterType.HasValue)
            query = query.Where(d => d.DeviceType == FilterType.Value);

        foreach (var item in query)
            FilteredInventoryDevices.Add(item);

        OnPropertyChanged(nameof(NamedCount));
        OnPropertyChanged(nameof(UnnamedCount));
        OnPropertyChanged(nameof(OnlineCount));
        OnPropertyChanged(nameof(OfflineCount));
    }

    private void AddAlert(AlertItem alert)
    {
        Alerts.Insert(0, alert);
        SaveAlerts();
        OnPropertyChanged(nameof(TotalAlerts));
        OnPropertyChanged(nameof(NewDeviceAlerts));
        OnPropertyChanged(nameof(SecurityAlerts));
        OnPropertyChanged(nameof(InfoAlerts));
    }

    private Task ClearAllAlertsAsync()
    {
        Alerts.Clear();
        SaveAlerts();
        OnPropertyChanged(nameof(TotalAlerts));
        return Task.CompletedTask;
    }

    private Task MarkAllReadAsync()
    {
        foreach (var a in Alerts) a.IsRead = true;
        SaveAlerts();
        return Task.CompletedTask;
    }

    private async Task StartPingAsync()
    {
        if (string.IsNullOrWhiteSpace(PingTarget)) return;
        IsPingRunning = true;
        PingResults.Clear();
        PingOutput = string.Empty;
        _pingCts = new CancellationTokenSource();
        var latencies = new List<long>();
        var failures = 0;
        var totalPackets = PacketCount <= 0 ? int.MaxValue : PacketCount;

        for (var i = 1; i <= totalPackets && !_pingCts.IsCancellationRequested; i++)
        {
            try
            {
                var reply = await _apiClientService.PingAsync(PingTarget, _pingCts.Token);
                if (reply.Alive)
                {
                    latencies.Add(reply.LatencyMs);
                    PingResults.Add(new PingResult { SequenceNumber = i, Latency = reply.LatencyMs, Status = "Success" });
                    PingOutput += $"Reply from {PingTarget}: time={reply.LatencyMs}ms\n";
                }
                else
                {
                    failures++;
                    PingResults.Add(new PingResult { SequenceNumber = i, Latency = 0, Status = "Timeout" });
                    PingOutput += "Request timed out\n";
                }
            }
            catch
            {
                failures++;
                PingResults.Add(new PingResult { SequenceNumber = i, Latency = 0, Status = "Error" });
            }

            if (PingResults.Count > 30) PingResults.RemoveAt(0);
            if (PacketCount <= 0) await Task.Delay(1000, _pingCts.Token);
        }

        if (latencies.Count > 0)
        {
            PingMin = latencies.Min();
            PingMax = latencies.Max();
            PingAvg = latencies.Average();
        }

        var sent = latencies.Count + failures;
        PingPacketLoss = sent == 0 ? 0 : failures * 100.0 / sent;
        IsPingRunning = false;
    }

    private Task StopPingAsync() { _pingCts?.Cancel(); IsPingRunning = false; return Task.CompletedTask; }

    private async Task StartTraceAsync()
    {
        TraceHops.Clear();
        TraceStatus = "Running...";
        var lines = await _apiClientService.TracerouteAsync(TraceTarget, CancellationToken.None);
        foreach (var line in lines)
        {
            var hop = ParseTraceHop(line);
            if (hop is not null) TraceHops.Add(hop);
        }
        TraceStatus = $"Complete ({TraceHops.Count} hops)";
    }

    private static TraceHop? ParseTraceHop(string line)
    {
        var normalized = Regex.Replace(line.Trim(), "\\s+", " ");
        var m = Regex.Match(normalized, "^(\\d+) (.+)$");
        if (!m.Success) return null;
        var hopNumber = int.Parse(m.Groups[1].Value);
        var ip = Regex.Match(normalized, "(\\d+\\.\\d+\\.\\d+\\.\\d+)");
        var rtts = Regex.Matches(normalized, "(\\d+)\\s*ms").Select(x => x.Groups[1].Value + "ms").ToList();
        return new TraceHop
        {
            HopNumber = hopNumber,
            IpAddress = ip.Success ? ip.Groups[1].Value : "*",
            Hostname = ip.Success ? ip.Groups[1].Value : "timeout",
            Rtt1 = rtts.ElementAtOrDefault(0) ?? "*",
            Rtt2 = rtts.ElementAtOrDefault(1) ?? "*",
            Rtt3 = rtts.ElementAtOrDefault(2) ?? "*",
            Status = !rtts.Any() ? "Timeout" : (rtts.Any(s => int.Parse(s.Replace("ms", "")) > 80) ? "Slow" : "Fast")
        };
    }

    private async Task StartPortScanAsync()
    {
        if (!IPAddress.TryParse(PortScanTarget, out _)) return;
        IsPortScanRunning = true;
        PortResults.Clear();
        _portScanCts = new CancellationTokenSource();
        var backendResult = await _apiClientService.ScanPortsAsync(PortScanTarget, _portScanCts.Token);
        var portsToDisplay = ScanType switch
        {
            PortScanType.CommonPorts => CommonPorts.AsEnumerable(),
            PortScanType.Top100 => Enumerable.Range(1, 100),
            PortScanType.FullScan => Enumerable.Range(1, 1024),
            PortScanType.CustomRange => ParseRange(CustomPortRange),
            _ => CommonPorts.AsEnumerable()
        };

        var openSet = backendResult.OpenPorts.ToHashSet();
        foreach (var port in portsToDisplay)
        {
            var isOpen = openSet.Contains(port);
            var risk = port switch
            {
                23 or 3389 => RiskLevel.Critical,
                22 or 445 or 5900 => RiskLevel.High,
                80 or 8080 => RiskLevel.Medium,
                _ => RiskLevel.Low
            };
            var result = new PortResult
            {
                Port = port,
                Protocol = "TCP",
                ServiceName = ServiceNames.GetValueOrDefault(port, "Unknown"),
                State = isOpen ? PortState.Open : PortState.Closed,
                RiskLevel = isOpen ? risk : RiskLevel.Low
            };
            PortResults.Add(result);
            if (isOpen && result.RiskLevel is RiskLevel.High or RiskLevel.Critical)
            {
                AddAlert(new AlertItem
                {
                    Title = "HIGH RISK PORT",
                    Message = $"{PortScanTarget}:{port} {result.ServiceName} is OPEN",
                    Severity = AlertSeverity.Security,
                    Timestamp = DateTimeOffset.UtcNow,
                    IsRead = false
                });
            }
        }

        ScannedCount = PortResults.Count;
        OnPropertyChanged(nameof(OpenCount));
        OnPropertyChanged(nameof(FilteredCount));
        OnPropertyChanged(nameof(RiskScore));
        OnPropertyChanged(nameof(CriticalPortCount));
        OnPropertyChanged(nameof(FirewallCoverage));
        IsPortScanRunning = false;
    }

    private Task StopPortScanAsync() { _portScanCts?.Cancel(); IsPortScanRunning = false; return Task.CompletedTask; }

    private static IEnumerable<int> ParseRange(string range)
    {
        var parts = range.Split('-', StringSplitOptions.TrimEntries);
        if (parts.Length == 2 && int.TryParse(parts[0], out var a) && int.TryParse(parts[1], out var b) && a > 0 && b >= a)
            return Enumerable.Range(a, Math.Min(65535, b) - a + 1);
        return CommonPorts;
    }

    private static async Task<PortResult> ProbePortAsync(string host, int port)
    {
        using var client = new TcpClient();
        try
        {
            var connectTask = client.ConnectAsync(host, port);
            var completed = await Task.WhenAny(connectTask, Task.Delay(250));
            var open = completed == connectTask && client.Connected;
            var risk = port switch
            {
                23 or 3389 => RiskLevel.Critical,
                22 or 445 or 5900 => RiskLevel.High,
                80 or 8080 => RiskLevel.Medium,
                _ => RiskLevel.Low
            };
            return new PortResult { Port = port, State = open ? PortState.Open : PortState.Filtered, Protocol = "TCP", ServiceName = ServiceNames.GetValueOrDefault(port, "Unknown"), RiskLevel = open ? risk : RiskLevel.Low };
        }
        catch
        {
            return new PortResult { Port = port, State = PortState.Closed, Protocol = "TCP", ServiceName = ServiceNames.GetValueOrDefault(port, "Unknown"), RiskLevel = RiskLevel.Low };
        }
    }

    private async Task SendWolAsync()
    {
        _ = int.TryParse(WolPort, out _);
        _ = IPAddress.TryParse(WolBroadcastIp, out _);
        if (string.IsNullOrWhiteSpace(WolMacAddress)) return;
        var response = await _apiClientService.SendWakeOnLanAsync(WolMacAddress);
        StatusMessage = response;
    }

    private async Task TestReachabilityAsync()
    {
        if (SelectedDevice is null) return;
        using var ping = new Ping();
        for (var i = 0; i < 15; i++)
        {
            var reply = await ping.SendPingAsync(SelectedDevice.Ip, 1000);
            if (reply.Status == IPStatus.Success)
            {
                StatusMessage = $"{SelectedDevice.Ip} is online.";
                return;
            }

            await Task.Delay(2000);
        }

        StatusMessage = "Reachability test timed out.";
    }

    private Task AddTargetAsync()
    {
        WolTargets.Add(new WolTarget { CustomName = "Saved Target", MacAddress = WolMacAddress });
        SaveWolTargets();
        return Task.CompletedTask;
    }

    private async Task WakeSelectedTargetAsync()
    {
        if (SelectedWolTarget is null) return;
        WolMacAddress = SelectedWolTarget.MacAddress;
        await SendWolAsync();
        SelectedWolTarget.LastWoken = DateTimeOffset.UtcNow;
        SaveWolTargets();
    }

    private Task RemoveSelectedTargetAsync()
    {
        if (SelectedWolTarget is null) return Task.CompletedTask;
        WolTargets.Remove(SelectedWolTarget);
        SaveWolTargets();
        return Task.CompletedTask;
    }

    private void OnDevicesCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        OnPropertyChanged(nameof(NewDevicesCount));
        OnPropertyChanged(nameof(OnlineDevicesCount));
    }

    private void LoadInventory() => LoadCollection(InventoryFilePath, InventoryDevices);
    private void SaveInventory() => SaveCollection(InventoryFilePath, InventoryDevices);
    private void LoadAlerts() => LoadCollection(AlertsFilePath, Alerts);
    private void SaveAlerts() => SaveCollection(AlertsFilePath, Alerts);
    private void LoadWolTargets() => LoadCollection(WolTargetsFilePath, WolTargets);
    private void SaveWolTargets() => SaveCollection(WolTargetsFilePath, WolTargets);

    private static void LoadCollection<T>(string path, ObservableCollection<T> target)
    {
        if (!File.Exists(path)) return;
        var items = JsonSerializer.Deserialize<List<T>>(File.ReadAllText(path));
        if (items is null) return;
        foreach (var item in items) target.Add(item);
    }

    private static void SaveCollection<T>(string path, ObservableCollection<T> data)
        => File.WriteAllText(path, JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true }));

    private static byte[]? NormalizeMac(string? mac)
    {
        if (string.IsNullOrWhiteSpace(mac)) return null;
        var parts = mac.Replace('-', ':').Split(':', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 6) return null;
        var bytes = new byte[6];
        for (var i = 0; i < 6; i++) if (!byte.TryParse(parts[i], System.Globalization.NumberStyles.HexNumber, null, out bytes[i])) return null;
        return bytes;
    }

    private static byte[] BuildMagicPacket(byte[] mac)
    {
        var packet = new byte[102];
        for (var i = 0; i < 6; i++) packet[i] = 0xFF;
        for (var i = 1; i <= 16; i++) Buffer.BlockCopy(mac, 0, packet, i * 6, 6);
        return packet;
    }

    private bool SetProperty<T>(ref T storage, T value, [CallerMemberName] string? propertyName = null)
    {
        if (Equals(storage, value)) return false;
        storage = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
