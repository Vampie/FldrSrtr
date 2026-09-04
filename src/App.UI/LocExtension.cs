using System;
using System.Windows.Markup;

namespace FldrSrtr
{
    /// <summary>
    /// XAML markup extension for translated text: Text="{local:Loc SomeKey}" resolves via
    /// Localization.Get at load time. Not a live binding — see Localization's remarks on why a
    /// language switch needs a restart. Positional syntax ({local:Loc Foo.Bar}) and
    /// Key="Foo.Bar" both work.
    /// </summary>
    [MarkupExtensionReturnType(typeof(string))]
    public class LocExtension : MarkupExtension
    {
        public string Key { get; set; }

        public LocExtension()
        {
        }

        public LocExtension(string key)
        {
            Key = key;
        }

        public override object ProvideValue(IServiceProvider serviceProvider) => Localization.Get(Key);
    }
}
