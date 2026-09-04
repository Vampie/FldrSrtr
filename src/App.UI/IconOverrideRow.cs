using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;

namespace FldrSrtr
{
    /// <summary>One row in IconOverridesWindow — an icon key (e.g. "icon-add.png") and the
    /// absolute path the user picked to replace it, or null to keep using the active pack.</summary>
    public class IconOverrideRow : INotifyPropertyChanged
    {
        private string _overridePath;

        public string Key { get; set; }

        public string OverridePath
        {
            get => _overridePath;
            set
            {
                _overridePath = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(PreviewPath));
                OnPropertyChanged(nameof(StatusText));
            }
        }

        /// <summary>What the icon actually resolves to right now — the override if set and still
        /// present on disk, otherwise the same active-pack/Default fallback IconPathConverter uses.</summary>
        public string PreviewPath
        {
            get
            {
                if (!string.IsNullOrEmpty(OverridePath) && File.Exists(OverridePath))
                {
                    return OverridePath;
                }

                string inActiveSet = Path.Combine(IconSetProvider.BasePath, IconSetProvider.ResolveFileName(IconSetProvider.BasePath, Key));
                if (File.Exists(inActiveSet))
                {
                    return inActiveSet;
                }

                return Path.Combine(IconSetProvider.DefaultSetFolder, IconSetProvider.ResolveFileName(IconSetProvider.DefaultSetFolder, Key));
            }
        }

        public string StatusText => string.IsNullOrEmpty(OverridePath) ? "(standaard)" : OverridePath;

        public event PropertyChangedEventHandler PropertyChanged;

        private void OnPropertyChanged([CallerMemberName] string propertyName = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
