using System.Windows;
using System.Windows.Input;
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

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2)
                WindowState = WindowState == WindowState.Maximized
                    ? WindowState.Normal
                    : WindowState.Maximized;
            else
                DragMove();
        }

        private void MinimizeWindow_Click(object sender, RoutedEventArgs e)
            => WindowState = WindowState.Minimized;

        private void MaximizeWindow_Click(object sender, RoutedEventArgs e)
            => WindowState = WindowState == WindowState.Maximized
                ? WindowState.Normal
                : WindowState.Maximized;

        private void CloseWindow_Click(object sender, RoutedEventArgs e)
            => Close();
    }
}

