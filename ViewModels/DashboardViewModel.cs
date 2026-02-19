using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
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

        public DashboardViewModel(ApiClientService apiClientService, SettingsService settingsService)
        {
            _apiClientService = apiClientService;
            BackendUrl = settingsService.BackendBaseUrl;
            Devices = new ObservableCollection<DeviceModel>();
            Devices.CollectionChanged += OnDevicesCollectionChanged;

            ScanCommand = new AsyncRelayCommand(ScanAsync, () => !IsScanning);
            CancelCommand = new AsyncRelayCommand(CancelAsync, () => IsScanning);
            CopyIpCommand = new AsyncRelayCommand(CopyIpAsync);
            CopyMacCommand = new AsyncRelayCommand(CopyMacAsync);
            ToggleThemeCommand = new AsyncRelayCommand(ToggleThemeAsync);

            // Sync initial state with ThemeService
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

        public AsyncRelayCommand ScanCommand { get; }
        public AsyncRelayCommand CancelCommand { get; }
        public AsyncRelayCommand CopyIpCommand { get; }
        public AsyncRelayCommand CopyMacCommand { get; }
        public AsyncRelayCommand ToggleThemeCommand { get; }

        public string BackendUrl { get; }

        public bool IsDark
        {
            get => _isDark;
            private set => SetProperty(ref _isDark, value);
        }

        /// <summary>Icon shown on the toggle button (sun in dark, moon in light).</summary>
        public string ThemeIcon => _isDark ? "☀" : "☾";

        /// <summary>Tooltip/label for the toggle button.</summary>
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

        public DeviceModel? SelectedDevice { get; set; }

        // ── Commands ────────────────────────────────────────────

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
                StatusMessage = $"Scan {result.Scan.ScanId} complete: {result.Scan.HostCount} hosts discovered";
            }
            catch (OperationCanceledException)
            {
                StatusMessage = "Scan canceled";
            }
            catch (Exception ex)
            {
                _backendOnline = false;
                OnPropertyChanged(nameof(BackendStatusLabel));
                ErrorMessage = ex.Message + "\nTip: Start backend with `uvicorn app.main:app --port 8787`";
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

        private void OnDevicesCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            OnPropertyChanged(nameof(NewDevicesCount));
            OnPropertyChanged(nameof(ScanStatusLabel));
            OnPropertyChanged(nameof(ScanStatusDetail));
        }

        // ── INotifyPropertyChanged ───────────────────────────────

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
