using System.Windows.Controls;

namespace FldrSrtr
{
    /// <summary>
    /// Shared "Insert variable ▾" dropdown — used by both RuleEditorWindow (Destination/Arguments)
    /// and MainWindow's Dashboard quick-action Destination, so the exact same token list and
    /// insert-at-cursor behavior stays in one place instead of drifting between the two.
    /// Grouped into submenus so the list stays manageable. A null entry renders as a separator
    /// (used in "File" to split general/Created/Modified properties).
    /// </summary>
    public static class VariableMenuHelper
    {
        private static readonly (string Group, string[] Tokens)[] VariableGroups =
        {
            ("Algemeen", new[] { "{Counter:1:1}", "{Guid}", "{Random:0000}", "{RandomString:8}" }),
            ("File", new[]
            {
                "{FileName}", "{OriginalName}", "{Extension}", "{OriginalExtension}", "{FullPath}", "{Directory}", "{FileSize}",
                null,
                "{CreatedYear}", "{CreatedMonth}", "{CreatedDay}", "{CreatedHour}", "{CreatedMinute}", "{CreatedSecond}", "{CreatedDate}", "{CreatedTime}",
                null,
                "{ModifiedYear}", "{ModifiedMonth}", "{ModifiedDay}", "{ModifiedHour}", "{ModifiedMinute}", "{ModifiedSecond}", "{ModifiedDate}", "{ModifiedTime}"
            }),
            ("Datum (huidige datum)", new[] { "{Year}", "{Month}", "{Day}", "{Hour}", "{Minute}", "{Second}", "{Date}", "{Time}", "{UnixTimestamp}", "{UnixTimestampMicro}" })
        };

        public static void ShowVariableMenu(Button anchor, TextBox target)
        {
            var menu = new ContextMenu();
            foreach ((string group, string[] tokens) in VariableGroups)
            {
                var groupItem = new MenuItem { Header = group };
                foreach (string token in tokens)
                {
                    if (token == null)
                    {
                        groupItem.Items.Add(new Separator());
                        continue;
                    }

                    var item = new MenuItem { Header = token };
                    item.Click += (s, e) => InsertAtCursor(target, token);
                    groupItem.Items.Add(item);
                }
                menu.Items.Add(groupItem);
            }
            anchor.ContextMenu = menu;
            menu.PlacementTarget = anchor;
            menu.IsOpen = true;
        }

        private static void InsertAtCursor(TextBox textBox, string token)
        {
            int caret = textBox.CaretIndex;
            textBox.Text = (textBox.Text ?? string.Empty).Insert(caret, token);
            textBox.CaretIndex = caret + token.Length;
            textBox.Focus();
        }
    }
}
