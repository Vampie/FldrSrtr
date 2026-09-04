using System.Windows;
using App.Core.Model;

namespace FldrSrtr
{
    public partial class ConflictDialog : Window
    {
        public ConflictResolution Decision { get; private set; } = ConflictResolution.Rename;

        public ConflictDialog(string existingPath, string incomingPath)
        {
            InitializeComponent();
            MessageText.Text = Localization.Get("ConflictDialog.Message", existingPath, incomingPath);
        }

        private void Skip_Click(object sender, RoutedEventArgs e)
        {
            Decision = ConflictResolution.Skip;
            DialogResult = true;
        }

        private void Overwrite_Click(object sender, RoutedEventArgs e)
        {
            Decision = ConflictResolution.Overwrite;
            DialogResult = true;
        }

        private void Rename_Click(object sender, RoutedEventArgs e)
        {
            Decision = ConflictResolution.Rename;
            DialogResult = true;
        }
    }
}
