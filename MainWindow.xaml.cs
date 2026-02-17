using System.Windows;
using UyKonek.Services;
using UyKonek.ViewModels;

namespace UyKonek
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();

            var settingsService = new SettingsService();
            var apiClientService = new ApiClientService(settingsService);
            DataContext = new DashboardViewModel(apiClientService, settingsService);
        }
    }
}
