using System.Windows;
using System.Windows.Input;

namespace FldrSrtr
{
    /// <summary>Minimal single-line text prompt — WPF has no built-in InputBox.</summary>
    public partial class TextPromptWindow : Window
    {
        public string Value { get; private set; }

        public TextPromptWindow(string prompt, string title = "FldrSrtr", string initialValue = "")
        {
            InitializeComponent();
            Title = title;
            PromptText.Text = prompt;
            ValueTextBox.Text = initialValue;
            Loaded += (s, e) => ValueTextBox.Focus();
        }

        private void Ok_Click(object sender, RoutedEventArgs e)
        {
            Value = ValueTextBox.Text.Trim();
            DialogResult = true;
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }

        private void ValueTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                Ok_Click(sender, e);
            }
        }
    }
}
