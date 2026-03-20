using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Net;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using UyKonek.Commands;
using UyKonek.Models;
using UyKonek.Services;

namespace UyKonek.ViewModels
{
    public sealed class DashboardViewModel : INotifyPropertyChanged
    {
        private readonly ApiClientService _apiClientService;
        private CancellationTokenSource? _scanCts;
        private bool _isScanning;
        private string _statusMessage = "Ready to scan";
        private string? _errorMessage;
        private bool _isDark = true;
        private bool _backendOnline = true;
        private string _activeSection = "NETWORK SCAN";
        private DeviceModel? _selectedDevice;
        private DeviceModel? _diagnosticTargetDevice;
        private string _diagnosticTargetIp = string.Empty;
        private string _diagnosticOutput = "Select diagnostics tab, pick device/target, then run.";
        private bool _isDiagnosticBusy;
        private long _pingLatencyMs;
        private bool _pingAlive;
        private int _pingMeterValue;

        public DashboardViewModel(ApiClientService apiClientService, SettingsService settingsService)
        {
            _apiClientService = apiClientService;
            BackendUrl = settingsService.BackendBaseUrl;
            Devices = new ObservableCollection<DeviceModel>();
            DiagnosticLines = new ObservableCollection<string>();
            Devices.CollectionChanged += OnDevicesCollectionChanged;

            ScanCommand = new AsyncRelayCommand(ScanAsync, () => !IsScanning);
            CancelCommand = new AsyncRelayCommand(CancelAsync, () => IsScanning);
            CopyIpCommand = new AsyncRelayCommand(CopyIpAsync);
            CopyMacCommand = new AsyncRelayCommand(CopyMacAsync);
            ToggleThemeCommand = new AsyncRelayCommand(ToggleThemeAsync);
            ShowNetworkScanCommand = new AsyncRelayCommand(ShowNetworkScanAsync);
            ShowDeviceInventoryCommand = new AsyncRelayCommand(ShowDeviceInventoryAsync);
            ShowDeviceDetailsCommand = new AsyncRelayCommand(ShowDeviceDetailsAsync);
            ShowAlertsCommand = new AsyncRelayCommand(ShowAlertsAsync);
            ShowPingCommand = new AsyncRelayCommand(ShowPingAsync);
            ShowTracerouteCommand = new AsyncRelayCommand(ShowTracerouteAsync);
            ShowOpenPortsCommand = new AsyncRelayCommand(ShowOpenPortsAsync);
            ShowWakeOnLanCommand = new AsyncRelayCommand(ShowWakeOnLanAsync);
            UseSelectedDeviceCommand = new AsyncRelayCommand(UseSelectedDeviceAsync);
            RunDiagnosticCommand = new AsyncRelayCommand(RunDiagnosticAsync, () => !IsDiagnosticBusy);

            _isDark = App.ThemeService.IsDark;
            App.ThemeService.ThemeChanged += () =>
            {
                _isDark = App.ThemeService.IsDark;
                OnPropertyChanged(nameof(IsDark));
                OnPropertyChanged(nameof(ThemeIcon));
                OnPropertyChanged(nameof(ThemeLabel));
            };
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        public ObservableCollection<DeviceModel> Devices { get; }
        public ObservableCollection<string> DiagnosticLines { get; }

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

        public string BackendUrl { get; }

        public bool IsDark
        {
            get => _isDark;
            private set => SetProperty(ref _isDark, value);
        }

        public string ThemeIcon => _isDark ? "☀" : "☾";
        public string ThemeLabel => _isDark ? "LIGHT MODE" : "DARK MODE";

        public bool IsScanning
        {
            get => _isScanning;
            private set
            {
                if (SetProperty(ref _isScanning, value))
                {
                    ScanCommand.RaiseCanExecuteChanged();
                    CancelCommand.RaiseCanExecuteChanged();
                    OnPropertyChanged(nameof(ScanStatusLabel));
                    OnPropertyChanged(nameof(ScanStatusDetail));
                }
            }
        }

        public bool IsDiagnosticBusy
        {
            get => _isDiagnosticBusy;
            private set
            {
                if (SetProperty(ref _isDiagnosticBusy, value))
                {
                    RunDiagnosticCommand.RaiseCanExecuteChanged();
                }
            }
        }

        public string DiagnosticTargetIp
        {
            get => _diagnosticTargetIp;
            set => SetProperty(ref _diagnosticTargetIp, value);
        }

        public DeviceModel? DiagnosticTargetDevice
        {
            get => _diagnosticTargetDevice;
            set
            {
                if (SetProperty(ref _diagnosticTargetDevice, value) && value is not null)
                {
                    DiagnosticTargetIp = value.Ip;
                }
            }
        }

        public string DiagnosticOutput
        {
            get => _diagnosticOutput;
            private set => SetProperty(ref _diagnosticOutput, value);
        }

        public long PingLatencyMs
        {
            get => _pingLatencyMs;
            private set => SetProperty(ref _pingLatencyMs, value);
        }

        public bool PingAlive
        {
            get => _pingAlive;
            private set => SetProperty(ref _pingAlive, value);
        }

        public int PingMeterValue
        {
            get => _pingMeterValue;
            private set => SetProperty(ref _pingMeterValue, value);
        }

        public string StatusMessage
        {
            get => _statusMessage;
            private set => SetProperty(ref _statusMessage, value);
        }

        public string? ErrorMessage
        {
            get => _errorMessage;
            private set
            {
                if (SetProperty(ref _errorMessage, value))
                {
                    OnPropertyChanged(nameof(ScanStatusLabel));
                    OnPropertyChanged(nameof(ScanStatusDetail));
                }
            }
        }

        public int NewDevicesCount => Devices.Count(d => d.IsNew);
        public int UnknownVendorCount => Devices.Count(d => string.IsNullOrWhiteSpace(d.Vendor) || d.Vendor == "Unknown");
        public int NewAlertCount => NewDevicesCount + UnknownVendorCount;

        public string ScanStatusLabel
        {
            get
            {
                if (IsScanning) return "SCANNING";
                if (!string.IsNullOrWhiteSpace(ErrorMessage)) return "ERROR";
                if (Devices.Count > 0) return "COMPLETE";
                return "IDLE";
            }
        }

        public string ScanStatusDetail
        {
            get
            {
                if (IsScanning) return "discovering devices";
                if (!string.IsNullOrWhiteSpace(ErrorMessage)) return "scan failed";
                if (Devices.Count > 0) return $"{Devices.Count} discovered device(s)";
                return "ready to scan";
            }
        }

        public string BackendStatusLabel => _backendOnline ? "ONLINE" : "OFFLINE";

        public string ActiveSection
        {
            get => _activeSection;
            private set
            {
                if (SetProperty(ref _activeSection, value))
                {
                    OnPropertyChanged(nameof(IsNetworkScanSection));
                    OnPropertyChanged(nameof(IsDeviceInventorySection));
                    OnPropertyChanged(nameof(IsDeviceDetailsSection));
                    OnPropertyChanged(nameof(IsAlertsSection));
                    OnPropertyChanged(nameof(IsDeviceTableSection));
                    OnPropertyChanged(nameof(IsPingSection));
                    OnPropertyChanged(nameof(IsTracerouteSection));
                    OnPropertyChanged(nameof(IsOpenPortsSection));
                    OnPropertyChanged(nameof(IsWakeOnLanSection));
                    OnPropertyChanged(nameof(IsDiagnosticsSection));
                }
            }
        }

        public bool IsNetworkScanSection => string.Equals(ActiveSection, "NETWORK SCAN", StringComparison.Ordinal);
        public bool IsDeviceInventorySection => string.Equals(ActiveSection, "DEVICE INVENTORY", StringComparison.Ordinal);
        public bool IsDeviceDetailsSection => string.Equals(ActiveSection, "DEVICE DETAILS", StringComparison.Ordinal);
        public bool IsAlertsSection => string.Equals(ActiveSection, "ALERTS", StringComparison.Ordinal);
        public bool IsPingSection => string.Equals(ActiveSection, "PING", StringComparison.Ordinal);
        public bool IsTracerouteSection => string.Equals(ActiveSection, "TRACEROUTE", StringComparison.Ordinal);
        public bool IsOpenPortsSection => string.Equals(ActiveSection, "OPEN PORTS", StringComparison.Ordinal);
        public bool IsWakeOnLanSection => string.Equals(ActiveSection, "WAKE-ON-LAN", StringComparison.Ordinal);
        public bool IsDiagnosticsSection => IsPingSection || IsTracerouteSection || IsOpenPortsSection || IsWakeOnLanSection;
        public bool IsDeviceTableSection => IsNetworkScanSection || IsDeviceInventorySection;

        public DeviceModel? SelectedDevice
        {
            get => _selectedDevice;
            set
            {
                if (SetProperty(ref _selectedDevice, value))
                {
                    OnPropertyChanged(nameof(HasSelectedDevice));
                }
            }
        }

        public bool HasSelectedDevice => SelectedDevice is not null;

        private async Task ScanAsync()
        {
            IsScanning = true;
            ErrorMessage = null;
            StatusMessage = "Scanning local network...";
            Devices.Clear();
            _scanCts = new CancellationTokenSource();

            try
            {
                var result = await _apiClientService.ScanDevicesAsync(_scanCts.Token);
                foreach (var device in result.Devices)
                    Devices.Add(device);

                _backendOnline = true;
                OnPropertyChanged(nameof(BackendStatusLabel));
                StatusMessage = $"Scan complete: {result.Scan.HostCount} hosts discovered";
            }
            catch (OperationCanceledException)
            {
                StatusMessage = "Scan canceled";
            }
            catch (Exception ex)
            {
                _backendOnline = false;
                OnPropertyChanged(nameof(BackendStatusLabel));
                ErrorMessage = ex.Message + "\nTip: Start Go backend with `cd uykonek-go-backend && go run main.go`";
                StatusMessage = "Scan failed";
            }
            finally
            {
                IsScanning = false;
                _scanCts?.Dispose();
                _scanCts = null;
            }
        }

        private Task CancelAsync()
        {
            _scanCts?.Cancel();
            return Task.CompletedTask;
        }

        private Task CopyIpAsync()
        {
            if (SelectedDevice is { Ip.Length: > 0 })
            {
                System.Windows.Clipboard.SetText(SelectedDevice.Ip);
                StatusMessage = $"Copied IP {SelectedDevice.Ip}";
            }
            return Task.CompletedTask;
        }

        private Task CopyMacAsync()
        {
            if (SelectedDevice is { Mac.Length: > 0 })
            {
                System.Windows.Clipboard.SetText(SelectedDevice.Mac);
                StatusMessage = $"Copied MAC {SelectedDevice.Mac}";
            }
            return Task.CompletedTask;
        }

        private Task ToggleThemeAsync()
        {
            App.ThemeService.Toggle();
            return Task.CompletedTask;
        }

        private Task ShowNetworkScanAsync() => SetSectionAsync("NETWORK SCAN", Devices.Count > 0
            ? $"Scan complete: {Devices.Count} hosts discovered"
            : "Ready to scan");

        private Task ShowDeviceInventoryAsync() => SetSectionAsync("DEVICE INVENTORY", Devices.Count > 0
            ? $"Inventory loaded: {Devices.Count} device(s)"
            : "No devices in inventory yet");

        private Task ShowDeviceDetailsAsync() => SetSectionAsync("DEVICE DETAILS", SelectedDevice is null
            ? "Select a device from Network Scan/Inventory to view details"
            : $"Viewing details for {SelectedDevice.Ip}");

        private Task ShowAlertsAsync() => SetSectionAsync("ALERTS", NewAlertCount > 0
            ? $"{NewAlertCount} alert signal(s) detected"
            : "No alerts at the moment");

        private Task ShowPingAsync() => SetSectionAsync("PING", "Run ping diagnostics with meter-style latency feedback.");
        private Task ShowTracerouteAsync() => SetSectionAsync("TRACEROUTE", "Traceroute analytics with hop lines.");
        private Task ShowOpenPortsAsync() => SetSectionAsync("OPEN PORTS", "Scan common open ports and analyze exposure.");
        private Task ShowWakeOnLanAsync() => SetSectionAsync("WAKE-ON-LAN", "Send magic packet to selected device MAC.");

        private Task UseSelectedDeviceAsync()
        {
            if (SelectedDevice is null)
            {
                DiagnosticOutput = "No table device selected yet.";
                return Task.CompletedTask;
            }

            DiagnosticTargetDevice = SelectedDevice;
            DiagnosticTargetIp = SelectedDevice.Ip;
            DiagnosticOutput = $"Diagnostic target set to {SelectedDevice.Ip}";
            return Task.CompletedTask;
        }

        private async Task RunDiagnosticAsync()
        {
            if (!IsDiagnosticsSection)
            {
                DiagnosticOutput = "Select a diagnostics tab first.";
                return;
            }

            if (!IPAddress.TryParse(DiagnosticTargetIp, out _))
            {
                DiagnosticOutput = "Invalid target IP address.";
                return;
            }

            IsDiagnosticBusy = true;
            DiagnosticLines.Clear();
            try
            {
                switch (ActiveSection)
                {
                    case "PING":
                        var ping = await _apiClientService.PingAsync(DiagnosticTargetIp, CancellationToken.None);
                        PingAlive = ping.Alive;
                        PingLatencyMs = ping.LatencyMs;
                        PingMeterValue = CalculatePingMeter(ping.LatencyMs, ping.Alive);
                        DiagnosticOutput = ping.Alive
                            ? $"Target reachable ({ping.LatencyMs} ms)."
                            : "Target did not respond to ping.";
                        break;

                    case "OPEN PORTS":
                        var ports = await _apiClientService.ScanPortsAsync(DiagnosticTargetIp, CancellationToken.None);
                        DiagnosticLines.Add($"Target: {ports.Ip}");
                        if (ports.OpenPorts.Length == 0)
                        {
                            DiagnosticLines.Add("No common ports detected open.");
                        }
                        else
                        {
                            DiagnosticLines.Add($"Open ports: {string.Join(", ", ports.OpenPorts)}");
                            DiagnosticLines.Add($"Exposure score: {Math.Min(100, ports.OpenPorts.Length * 12)} / 100");
                        }
                        DiagnosticOutput = "Open ports analysis complete.";
                        break;

                    case "TRACEROUTE":
                        var hops = await _apiClientService.TracerouteAsync(DiagnosticTargetIp, CancellationToken.None);
                        foreach (var hop in hops.Take(20))
                        {
                            DiagnosticLines.Add(hop);
                        }
                        DiagnosticOutput = hops.Count > 0
                            ? $"Traceroute collected {hops.Count} line(s)."
                            : "Traceroute returned no hop data.";
                        break;

                    case "WAKE-ON-LAN":
                        if (DiagnosticTargetDevice is null)
                        {
                            DiagnosticOutput = "Select a target device first (needs MAC address).";
                            break;
                        }
                        var wol = await _apiClientService.SendWakeOnLanAsync(DiagnosticTargetDevice.Mac);
                        DiagnosticLines.Add($"Target IP: {DiagnosticTargetDevice.Ip}");
                        DiagnosticLines.Add($"Target MAC: {DiagnosticTargetDevice.Mac}");
                        DiagnosticLines.Add(wol);
                        DiagnosticOutput = "Wake-on-LAN packet sent.";
                        break;
                }
            }
            catch (Exception ex)
            {
                DiagnosticOutput = $"Diagnostics failed: {ex.Message}";
                DiagnosticLines.Add(ex.Message);
            }
            finally
            {
                IsDiagnosticBusy = false;
            }
        }

        private static int CalculatePingMeter(long latencyMs, bool alive)
        {
            if (!alive) return 0;
            if (latencyMs <= 20) return 100;
            if (latencyMs <= 50) return 80;
            if (latencyMs <= 100) return 60;
            if (latencyMs <= 200) return 40;
            if (latencyMs <= 400) return 20;
            return 10;
        }

        private Task SetSectionAsync(string section, string status)
        {
            ActiveSection = section;
            StatusMessage = status;
            return Task.CompletedTask;
        }

        private void OnDevicesCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            OnPropertyChanged(nameof(NewDevicesCount));
            OnPropertyChanged(nameof(UnknownVendorCount));
            OnPropertyChanged(nameof(NewAlertCount));
            OnPropertyChanged(nameof(ScanStatusLabel));
            OnPropertyChanged(nameof(ScanStatusDetail));
        }

        private bool SetProperty<T>(ref T storage, T value, [CallerMemberName] string? propertyName = null)
        {
            if (Equals(storage, value)) return false;
            storage = value;
            OnPropertyChanged(propertyName);
            return true;
        }

        private void OnPropertyChanged(string? propertyName)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
