using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace App.Core.Model
{
    /// <summary>Raises PropertyChanged so a bound Actions list label updates live as the user edits an action.</summary>
    public class RuleAction : INotifyPropertyChanged
    {
        private ActionType _type;
        private string _destination;
        private string _arguments;
        private ConflictResolution _onConflict = ConflictResolution.Rename;

        public ActionType Type
        {
            get => _type;
            set => SetField(ref _type, value);
        }

        /// <summary>
        /// Move/Copy: destination folder. Rename: new file name (no directory). CreateFolder:
        /// folder to create. AddExtension: extension to append. OpenWith/ExecuteExternal: the
        /// application/script path. Zip: path to the archive. Unused for Delete/Open/RemoveExtension.
        /// Supports dynamic variables ({Year}, {FileName}, ...) — see VariableResolver.
        /// </summary>
        public string Destination
        {
            get => _destination;
            set => SetField(ref _destination, value);
        }

        /// <summary>ExecuteExternal only: command-line arguments. Supports the same variables.</summary>
        public string Arguments
        {
            get => _arguments;
            set => SetField(ref _arguments, value);
        }

        public ConflictResolution OnConflict
        {
            get => _onConflict;
            set => SetField(ref _onConflict, value);
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private void SetField<T>(ref T field, T value, [CallerMemberName] string propertyName = null)
        {
            field = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

            // The Actions list's label binds to the whole action (Text="{Binding Converter=...}", no
            // Path) so it can show one formatted string built from several fields — a WPF binding
            // with an empty path only refreshes on a PropertyChanged whose name is null/"", not on a
            // named property, so without this the label never reflected an edit until the editor was
            // closed and reopened.
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(string.Empty));
        }
    }
}
