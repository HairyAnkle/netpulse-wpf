using System.Windows;
using NetPulse.Client.Services;
using NetPulse.Client.ViewModels;

namespace NetPulse.Client;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        var settingsService = new SettingsService();
        var apiClient = new ApiClientService(settingsService);
        DataContext = new DashboardViewModel(apiClient, settingsService);
    }
}
