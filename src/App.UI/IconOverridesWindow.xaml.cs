using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows;

namespace FldrSrtr
{
    public partial class IconOverridesWindow : Window
    {
        private readonly Dictionary<string, string> _overrides;
        private readonly ObservableCollection<IconOverrideRow> _rows = new ObservableCollection<IconOverrideRow>();

        public IconOverridesWindow(Dictionary<string, string> overrides)
        {
            InitializeComponent();
            _overrides = overrides;

            foreach (string key in IconSetProvider.GetAllIconKeys())
            {
                _rows.Add(new IconOverrideRow
                {
                    Key = key,
                    OverridePath = overrides.TryGetValue(key, out string path) ? path : null
                });
            }

            RowsListBox.ItemsSource = _rows;
        }

        private void Browse_Click(object sender, RoutedEventArgs e)
        {
            if (!(((FrameworkElement)sender).DataContext is IconOverrideRow row))
            {
                return;
            }

            using (var dialog = new System.Windows.Forms.OpenFileDialog { Filter = Localization.Get("IconOverrides.PngFilter") })
            {
                if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    row.OverridePath = dialog.FileName;
                }
            }
        }

        private void Reset_Click(object sender, RoutedEventArgs e)
        {
            if (((FrameworkElement)sender).DataContext is IconOverrideRow row)
            {
                row.OverridePath = null;
            }
        }

        private void SaveAsPack_Click(object sender, RoutedEventArgs e)
        {
            var prompt = new TextPromptWindow(Localization.Get("IconOverrides.SaveAsPack.Prompt"), Localization.Get("IconOverrides.SaveAsPack")) { Owner = this };
            if (prompt.ShowDialog() != true || string.IsNullOrWhiteSpace(prompt.Value))
            {
                return;
            }

            string packName = prompt.Value;
            if (packName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            {
                MessageBox.Show(this, Localization.Get("IconOverrides.InvalidName"),
                    "FldrSrtr", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string packFolder = Path.Combine(IconSetProvider.IconSetsRootFolder, packName);
            if (Directory.Exists(packFolder))
            {
                MessageBoxResult overwrite = MessageBox.Show(this, Localization.Get("IconOverrides.ConfirmOverwrite", packName),
                    "FldrSrtr", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                if (overwrite != MessageBoxResult.Yes)
                {
                    return;
                }
            }

            Dictionary<string, string> keyToSourceFile = _rows.ToDictionary(row => row.Key, row => row.PreviewPath);
            IconSetProvider.SavePack(packName, keyToSourceFile);

            MessageBox.Show(this, Localization.Get("IconOverrides.SaveAsPack.Success", packName),
                "FldrSrtr", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            _overrides.Clear();
            foreach (IconOverrideRow row in _rows)
            {
                if (!string.IsNullOrEmpty(row.OverridePath))
                {
                    _overrides[row.Key] = row.OverridePath;
                }
            }

            DialogResult = true;
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
}
