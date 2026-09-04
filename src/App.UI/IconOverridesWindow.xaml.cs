using System.Collections.Generic;
using System.Collections.ObjectModel;
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

            using (var dialog = new System.Windows.Forms.OpenFileDialog { Filter = "PNG-afbeeldingen (*.png)|*.png" })
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
