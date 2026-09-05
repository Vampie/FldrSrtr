using System.Windows;
using System.Windows.Media;
using ModernWpf;

namespace FldrSrtr
{
    /// <summary>
    /// Lets the user pick a base (system/light/dark) and an accent color, previewing the change
    /// live across the whole app as they go (see ThemeProvider.Apply), then optionally save it as
    /// a brand new named theme that shows up in Settings' Theme dropdown right away. Cancel
    /// reverts to whatever theme was active before this window opened.
    /// </summary>
    public partial class ThemeEditorWindow : Window
    {
        private readonly string _previousThemeName;
        private string _accentHex;
        private bool _initialized;

        public string SavedThemeName { get; private set; }

        public ThemeEditorWindow(string currentThemeName, ApplicationTheme? currentBase, string currentAccentHex)
        {
            InitializeComponent();
            _previousThemeName = currentThemeName;
            _accentHex = currentAccentHex;

            // Setting IsChecked here fires Base_Checked immediately, while this window's own visual
            // tree is still under construction — calling ThemeManager mid-construction was found to
            // corrupt this window's rendering (it painted solid accent-color instead of its content).
            // _initialized stays false until construction is done, so that first Checked is a no-op.
            if (currentBase == ApplicationTheme.Light) LightRadio.IsChecked = true;
            else if (currentBase == ApplicationTheme.Dark) DarkRadio.IsChecked = true;
            else SystemRadio.IsChecked = true;

            UpdateSwatch();
            _initialized = true;
        }

        private ApplicationTheme? SelectedBase =>
            LightRadio.IsChecked == true ? ApplicationTheme.Light :
            DarkRadio.IsChecked == true ? ApplicationTheme.Dark :
            (ApplicationTheme?)null;

        private void UpdateSwatch()
        {
            AccentSwatch.Background = string.IsNullOrEmpty(_accentHex)
                ? Brushes.Transparent
                : new SolidColorBrush((Color)ColorConverter.ConvertFromString(_accentHex));
        }

        private void ApplyPreview()
        {
            ThemeProvider.Apply(SelectedBase, _accentHex);
        }

        private void Base_Checked(object sender, RoutedEventArgs e)
        {
            if (_initialized)
            {
                ApplyPreview();
            }
        }

        private void PickColor_Click(object sender, RoutedEventArgs e)
        {
            using (var dialog = new System.Windows.Forms.ColorDialog { FullOpen = true })
            {
                if (!string.IsNullOrEmpty(_accentHex))
                {
                    var current = (Color)ColorConverter.ConvertFromString(_accentHex);
                    dialog.Color = System.Drawing.Color.FromArgb(current.R, current.G, current.B);
                }

                if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    System.Drawing.Color c = dialog.Color;
                    _accentHex = $"#{c.R:X2}{c.G:X2}{c.B:X2}";
                    UpdateSwatch();
                    ApplyPreview();
                }
            }
        }

        private void DefaultColor_Click(object sender, RoutedEventArgs e)
        {
            _accentHex = null;
            UpdateSwatch();
            ApplyPreview();
        }

        private void SaveAsTheme_Click(object sender, RoutedEventArgs e)
        {
            string name = NewThemeNameTextBox.Text.Trim();
            if (string.IsNullOrEmpty(name) || name.IndexOfAny(System.IO.Path.GetInvalidFileNameChars()) >= 0)
            {
                MessageBox.Show(this, Localization.Get("ThemeEditor.InvalidName"),
                    "FldrSrtr", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            ThemeProvider.SaveCustomTheme(name, SelectedBase, _accentHex);
            SavedThemeName = name;

            MessageBox.Show(this, Localization.Get("ThemeEditor.SaveAsTheme.Success", name),
                "FldrSrtr", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void Ok_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(_accentHex) && SavedThemeName == null)
            {
                // A custom accent color has no built-in dropdown entry to fall back to — it must
                // be saved under a name first, otherwise Settings would have nothing to select
                // and this choice would be lost the moment the app restarts.
                MessageBox.Show(this, Localization.Get("ThemeEditor.MustSaveToKeep"),
                    "FldrSrtr", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (SavedThemeName == null)
            {
                // No custom accent chosen: this preview matches one of the built-ins exactly, so
                // Settings can select that directly without needing a saved theme file at all.
                SavedThemeName = SelectedBase == ApplicationTheme.Light ? ThemeProvider.LightThemeName
                    : SelectedBase == ApplicationTheme.Dark ? ThemeProvider.DarkThemeName
                    : ThemeProvider.SystemThemeName;
            }

            DialogResult = true;
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            ThemeProvider.ApplySetting(_previousThemeName);
            DialogResult = false;
        }
    }
}
