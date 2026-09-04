using System.Windows;
using App.Core.Execution;
using App.Core.Model;

namespace FldrSrtr
{
    public class WpfConflictPrompt : IConflictPrompt
    {
        private readonly Window _owner;

        public WpfConflictPrompt(Window owner)
        {
            _owner = owner;
        }

        public ConflictResolution Resolve(string existingPath, string incomingPath)
        {
            var dialog = new ConflictDialog(existingPath, incomingPath) { Owner = _owner };
            dialog.ShowDialog();
            return dialog.Decision;
        }
    }
}
