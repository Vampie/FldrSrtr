using System.Windows;
using App.Infrastructure.Configuration;

namespace FoldrSortr
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            try
            {
                PortablePaths.EnsureBaseDirectoryIsWritable();
            }
            catch (System.InvalidOperationException ex)
            {
                MessageBox.Show(ex.Message, "FoldrSortr", MessageBoxButton.OK, MessageBoxImage.Error);
                Shutdown(1);
                return;
            }

            new ConfigService().LoadOrCreateDefault();
        }
    }
}
