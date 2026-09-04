using System.Collections.ObjectModel;
using System.Windows;
using App.Core.Model;

namespace FldrSrtr
{
    public partial class FolderSettingsWindow : Window
    {
        private readonly WatchedFolder _folder;
        private readonly ObservableCollection<string> _filePatterns;
        private readonly ObservableCollection<string> _subfolders;

        public FolderSettingsWindow(WatchedFolder folder)
        {
            InitializeComponent();
            _folder = folder;

            PathTextBox.Text = folder.Path;
            RecursiveCheckBox.IsChecked = folder.Recursive;
            MaxDepthTextBox.Text = folder.MaxRecursionDepth.ToString();

            _filePatterns = new ObservableCollection<string>(folder.ExcludedFilePatterns);
            _subfolders = new ObservableCollection<string>(folder.ExcludedSubfolders);
            FilePatternsListBox.ItemsSource = _filePatterns;
            SubfoldersListBox.ItemsSource = _subfolders;
        }

        private void BrowsePath_Click(object sender, RoutedEventArgs e)
        {
            string path = ModernFolderPicker.PickFolder(Localization.Get("FolderSettings.PickFolder"), PathTextBox.Text);
            if (path != null)
            {
                PathTextBox.Text = path;
            }
        }

        private void AddFilePattern_Click(object sender, RoutedEventArgs e)
        {
            string pattern = NewFilePatternTextBox.Text.Trim();
            if (!string.IsNullOrEmpty(pattern))
            {
                _filePatterns.Add(pattern);
                NewFilePatternTextBox.Clear();
            }
        }

        private void RemoveFilePattern_Click(object sender, RoutedEventArgs e)
        {
            if (((FrameworkElement)sender).DataContext is string pattern)
            {
                _filePatterns.Remove(pattern);
            }
        }

        private void AddSubfolder_Click(object sender, RoutedEventArgs e)
        {
            string name = NewSubfolderTextBox.Text.Trim();
            if (!string.IsNullOrEmpty(name))
            {
                _subfolders.Add(name);
                NewSubfolderTextBox.Clear();
            }
        }

        private void RemoveSubfolder_Click(object sender, RoutedEventArgs e)
        {
            if (((FrameworkElement)sender).DataContext is string name)
            {
                _subfolders.Remove(name);
            }
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            string path = PathTextBox.Text.Trim();
            if (string.IsNullOrEmpty(path))
            {
                MessageBox.Show(this, Localization.Get("FolderSettings.PathRequired"), "FldrSrtr", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            _folder.Path = path;
            _folder.Recursive = RecursiveCheckBox.IsChecked == true;
            _folder.MaxRecursionDepth = int.TryParse(MaxDepthTextBox.Text, out int depth) ? depth : 10;

            _folder.ExcludedFilePatterns.Clear();
            foreach (string pattern in _filePatterns)
            {
                _folder.ExcludedFilePatterns.Add(pattern);
            }

            _folder.ExcludedSubfolders.Clear();
            foreach (string name in _subfolders)
            {
                _folder.ExcludedSubfolders.Add(name);
            }

            DialogResult = true;
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
}
