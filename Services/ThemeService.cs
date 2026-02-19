using System;
using System.Windows;

namespace UyKonek.Services
{
    public class ThemeService
    {
        private const string DarkThemeUri  = "Themes/DarkTheme.xaml";
        private const string LightThemeUri = "Themes/LightTheme.xaml";

        private bool _isDark = true;
        public bool IsDark => _isDark;

        public event Action? ThemeChanged;

        /// <summary>Toggles between dark and light theme and returns the new state.</summary>
        public bool Toggle()
        {
            _isDark = !_isDark;
            Apply();
            ThemeChanged?.Invoke();
            return _isDark;
        }

        public void Apply()
        {
            var uri = new Uri(_isDark ? DarkThemeUri : LightThemeUri, UriKind.Relative);
            var dict = new ResourceDictionary { Source = uri };

            // Replace the first merged dictionary (index 0 = our theme slot)
            var merged = Application.Current.Resources.MergedDictionaries;
            if (merged.Count > 0)
                merged[0] = dict;
            else
                merged.Add(dict);
        }

        /// <summary>Call once at startup to load the default dark theme.</summary>
        public void Initialize()
        {
            _isDark = true;
            Apply();
        }
    }
}
