using System.Windows;
using UyKonek.Services;

namespace UyKonek
{
    public partial class App : Application
    {
        /// <summary>Singleton ThemeService — accessible from anywhere via App.ThemeService.</summary>
        public static ThemeService ThemeService { get; } = new ThemeService();

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            // DarkTheme.xaml is already loaded by App.xaml (MergedDictionaries[0]).
            // No further initialization needed unless you want to restore a saved preference:
            //
            //   bool savedIsDark = LoadUserPreference();
            //   if (!savedIsDark) ThemeService.Toggle();
        }
    }
}
