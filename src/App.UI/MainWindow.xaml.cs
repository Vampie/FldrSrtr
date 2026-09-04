using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO.Abstractions;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using App.Core.Configuration;
using App.Core.Execution;
using App.Core.Model;
using App.Infrastructure.Activity;
using App.Infrastructure.Configuration;
using App.Infrastructure.Execution;
using App.Infrastructure.Notifications;
using App.Infrastructure.Safety;
using App.Infrastructure.Scanning;

namespace FldrSrtr
{
    public partial class MainWindow : Window
    {
        private readonly ConfigService _configService = new ConfigService();
        private readonly IFileSystem _fileSystem = new FileSystem();
        private readonly ActivityLogger _activityLogger = new ActivityLogger();
        private readonly NotificationService _notificationService = new NotificationService();
        private readonly ImportExportService _importExportService = new ImportExportService();
        private readonly ObservableCollection<PreviewRow> _previewRows = new ObservableCollection<PreviewRow>();

        private AppConfig _config;
        private FolderScanner _scanner;
        private List<ActivityLogEntry> _allActivityEntries = new List<ActivityLogEntry>();

        public MainWindow()
        {
            InitializeComponent();
            ResultsGrid.ItemsSource = _previewRows;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            Title = $"FldrSrtr v{GetAppVersion()}";

            _config = _configService.LoadOrCreateDefault();
            _scanner = new FolderScanner(_fileSystem);
            FoldersList.ItemsSource = _config.Folders;

            PeriodComboBox.ItemsSource = Enum.GetValues(typeof(StatisticsPeriod));
            PeriodComboBox.SelectedItem = StatisticsPeriod.Last7Days;

            LoadSettingsIntoUi();
            RefreshActivity();
        }

        /// <summary>
        /// Reads the informational version (e.g. "1.2.11") that release.ps1 stamps onto the
        /// assembly via -p:Version. Falls back to the plain assembly version for local/dev
        /// builds that were never packaged through the release script.
        /// </summary>
        private static string GetAppVersion()
        {
            var assembly = System.Reflection.Assembly.GetExecutingAssembly();
            string informational = assembly
                .GetCustomAttributes(typeof(System.Reflection.AssemblyInformationalVersionAttribute), false)
                .OfType<System.Reflection.AssemblyInformationalVersionAttribute>()
                .FirstOrDefault()?.InformationalVersion;

            return !string.IsNullOrWhiteSpace(informational) ? informational : assembly.GetName().Version.ToString();
        }

        private WatchedFolder SelectedFolder => FoldersList.SelectedItem as WatchedFolder;
        private Rule SelectedRule => RulesList.SelectedItem as Rule;

        private void SaveConfig() => _configService.Save(_config);

        private ProtectedPathGuard BuildGuard() =>
            new ProtectedPathGuard(_config.Settings.ProtectedFolders, _config.Settings.ProtectedExtensions);

        // ===================== Folders =====================

        private void FoldersList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            WatchedFolder folder = SelectedFolder;
            RulesList.ItemsSource = folder?.Rules;
            RulesHeaderText.Text = folder != null ? $"Rules for: {folder.Path}" : "Selecteer een folder";
            _previewRows.Clear();
            SummaryText.Text = string.Empty;
        }

        private void FoldersList_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (SelectedFolder != null)
            {
                FolderSettings_Click(sender, e);
            }
        }

        private void AddFolder_Click(object sender, RoutedEventArgs e)
        {
            string path = ModernFolderPicker.PickFolder("Selecteer een map om te volgen");
            if (path == null)
            {
                return;
            }

            _config.Folders.Add(new WatchedFolder { Path = path });
            SaveConfig();
        }

        private void RemoveFolder_Click(object sender, RoutedEventArgs e)
        {
            WatchedFolder folder = SelectedFolder;
            if (folder == null)
            {
                return;
            }

            MessageBoxResult confirm = MessageBox.Show(this, $"Folder '{folder.Path}' verwijderen uit FldrSrtr?\n(De map zelf blijft bestaan.)",
                "FldrSrtr", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (confirm != MessageBoxResult.Yes)
            {
                return;
            }

            _config.Folders.Remove(folder);
            SaveConfig();
        }

        private void OpenInExplorer_Click(object sender, RoutedEventArgs e)
        {
            WatchedFolder folder = SelectedFolder;
            if (folder == null || !_fileSystem.Directory.Exists(folder.Path))
            {
                MessageBox.Show(this, "Deze map bestaat niet (meer).", "FldrSrtr", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            Process.Start("explorer.exe", $"\"{folder.Path}\"");
        }

        private void ScanNow_Click(object sender, RoutedEventArgs e)
        {
            WatchedFolder folder = SelectedFolder;
            if (folder == null)
            {
                return;
            }

            var files = _scanner.Scan(folder);
            MessageBox.Show(this, $"{files.Count} bestand(en) gevonden in {folder.Path}" +
                (folder.Recursive ? " (recursief)." : "."), "FldrSrtr", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void FolderSettings_Click(object sender, RoutedEventArgs e)
        {
            WatchedFolder folder = SelectedFolder;
            if (folder == null)
            {
                return;
            }

            var settings = new FolderSettingsWindow(folder) { Owner = this };
            if (settings.ShowDialog() == true)
            {
                SaveConfig();
            }
        }

        private void ExportFolder_Click(object sender, RoutedEventArgs e)
        {
            WatchedFolder folder = SelectedFolder;
            if (folder == null)
            {
                MessageBox.Show(this, "Selecteer eerst een folder.", "FldrSrtr", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            using (var dialog = new System.Windows.Forms.SaveFileDialog { Filter = "JSON (*.json)|*.json", FileName = "folder-export.json" })
            {
                if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    _importExportService.ExportFolder(folder, dialog.FileName);
                }
            }
        }

        private void ImportFolder_Click(object sender, RoutedEventArgs e)
        {
            using (var dialog = new System.Windows.Forms.OpenFileDialog { Filter = "JSON (*.json)|*.json" })
            {
                if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    try
                    {
                        WatchedFolder imported = _importExportService.ImportFolder(dialog.FileName);
                        _config.Folders.Add(imported);
                        SaveConfig();
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(this, $"Import mislukt: {ex.Message}", "FldrSrtr", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
        }

        // ===================== Rules =====================

        private void RulesList_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (SelectedRule != null)
            {
                EditRule_Click(sender, e);
            }
        }

        private void AddRule_Click(object sender, RoutedEventArgs e)
        {
            WatchedFolder folder = SelectedFolder;
            if (folder == null)
            {
                MessageBox.Show(this, "Selecteer eerst een folder.", "FldrSrtr", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var rule = new Rule();
            var editor = new RuleEditorWindow(rule) { Owner = this };
            if (editor.ShowDialog() == true)
            {
                folder.Rules.Add(rule);
                SaveConfig();
            }
        }

        private void EditRule_Click(object sender, RoutedEventArgs e)
        {
            Rule rule = SelectedRule;
            if (rule == null)
            {
                return;
            }

            var editor = new RuleEditorWindow(rule) { Owner = this };
            if (editor.ShowDialog() == true)
            {
                SaveConfig();
                RulesList.Items.Refresh();
            }
        }

        private void DeleteRule_Click(object sender, RoutedEventArgs e)
        {
            WatchedFolder folder = SelectedFolder;
            Rule rule = SelectedRule;
            if (folder == null || rule == null)
            {
                return;
            }

            folder.Rules.Remove(rule);
            SaveConfig();
        }

        private void ExportRule_Click(object sender, RoutedEventArgs e)
        {
            Rule rule = SelectedRule;
            if (rule == null)
            {
                MessageBox.Show(this, "Selecteer eerst een regel.", "FldrSrtr", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            using (var dialog = new System.Windows.Forms.SaveFileDialog { Filter = "JSON (*.json)|*.json", FileName = "rule-export.json" })
            {
                if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    _importExportService.ExportRule(rule, dialog.FileName);
                }
            }
        }

        private void ImportRule_Click(object sender, RoutedEventArgs e)
        {
            WatchedFolder folder = SelectedFolder;
            if (folder == null)
            {
                MessageBox.Show(this, "Selecteer eerst een folder.", "FldrSrtr", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            using (var dialog = new System.Windows.Forms.OpenFileDialog { Filter = "JSON (*.json)|*.json" })
            {
                if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    try
                    {
                        Rule imported = _importExportService.ImportRule(dialog.FileName);
                        folder.Rules.Add(imported);
                        SaveConfig();
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(this, $"Import mislukt: {ex.Message}", "FldrSrtr", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
        }

        // ===================== Dry run / run =====================

        private void DryRun_Click(object sender, RoutedEventArgs e) => RunSelectedRule(dryRun: true);

        private void RunRule_Click(object sender, RoutedEventArgs e)
        {
            WatchedFolder folder = SelectedFolder;
            Rule rule = SelectedRule;
            if (folder == null || rule == null)
            {
                MessageBox.Show(this, "Selecteer een folder en een regel.", "FldrSrtr", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var files = _scanner.Scan(folder);
            var engine = new RuleEngine(new FileSystemFileOperations(_fileSystem, BuildGuard()));
            int matchCount = engine.GetMatches(rule, files).Count();

            if (matchCount > _config.Settings.MaxFilesPerRun)
            {
                MessageBox.Show(this,
                    $"Deze regel matcht {matchCount} bestanden — dat is meer dan de ingestelde limiet van {_config.Settings.MaxFilesPerRun}.\n" +
                    "Verhoog de limiet in Settings als dit verwacht is, of verfijn de regel.",
                    "FldrSrtr", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string question =
                $"Regel '{rule.Name}' uitvoeren op:\n{folder.Path}\n\n" +
                $"{matchCount} bestand(en) komen in aanmerking. Dit wijzigt bestanden op schijf.\n\n" +
                (matchCount > _config.Settings.ConfirmationThreshold ? "Dit is best veel bestanden — doorgaan?" : "Doorgaan?");

            MessageBoxResult confirm = MessageBox.Show(this, question, "FldrSrtr", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (confirm == MessageBoxResult.Yes)
            {
                RunSelectedRule(dryRun: false);
            }
        }

        private void RunSelectedRule(bool dryRun)
        {
            WatchedFolder folder = SelectedFolder;
            Rule rule = SelectedRule;
            if (folder == null || rule == null)
            {
                MessageBox.Show(this, "Selecteer een folder en een regel.", "FldrSrtr", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var files = _scanner.Scan(folder);
            var fileOps = new FileSystemFileOperations(_fileSystem, BuildGuard());
            var engine = new RuleEngine(fileOps, new WpfConflictPrompt(this));
            List<ExecutionResult> executionResults = engine.ExecuteRule(rule, files, dryRun);

            _previewRows.Clear();
            int success = 0, failed = 0, skipped = 0;

            foreach (ExecutionResult result in executionResults)
            {
                PlannedAction plan = result.Plan;
                string status = plan.Skipped ? "SKIPPED" : (result.Success ? (dryRun ? "PREVIEW" : "SUCCESS") : "ERROR");
                string message = result.ErrorMessage ?? plan.SkipReason;

                _previewRows.Add(new PreviewRow
                {
                    FileName = plan.File.Name,
                    Action = plan.Action.Type.ToString(),
                    FromPath = plan.OriginalPath,
                    ToPath = plan.ResolvedDestinationPath,
                    Status = status,
                    Message = message
                });

                if (!dryRun)
                {
                    _activityLogger.Append(new ActivityLogEntry
                    {
                        FolderPath = folder.Path,
                        RuleName = rule.Name,
                        FileName = plan.File.Name,
                        Action = plan.Action.Type.ToString(),
                        Status = plan.Skipped ? "WARNING" : (result.Success ? "SUCCESS" : "ERROR"),
                        Message = message,
                        OriginalPath = plan.OriginalPath,
                        DestinationPath = plan.ResolvedDestinationPath,
                        FileSizeBytes = plan.File.SizeBytes
                    });
                }

                if (plan.Skipped) skipped++;
                else if (result.Success) success++;
                else failed++;
            }

            SummaryText.Text = dryRun
                ? $"{executionResults.Count} actie(s) zouden worden uitgevoerd ({skipped} zouden worden overgeslagen)."
                : $"{executionResults.Count} matched, {success} success, {skipped} skipped, {failed} failed.";

            if (!dryRun)
            {
                RefreshActivity();
                RefreshDashboard();
                _notificationService.ShowBalloonTip("FldrSrtr", $"'{rule.Name}': {SummaryText.Text}");
            }
        }

        private void ResultsGrid_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (ResultsGrid.SelectedItem is PreviewRow row)
            {
                MessageBox.Show(this,
                    $"Bestand: {row.FileName}\nActie: {row.Action}\nStatus: {row.Status}\n\nVan:\n{row.FromPath}\n\nNaar:\n{row.ToPath}\n\nMelding:\n{row.Message}",
                    "FldrSrtr — details", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        // ===================== Activity =====================

        private void RefreshActivity_Click(object sender, RoutedEventArgs e) => RefreshActivity();

        private void ActivityGrid_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (ActivityGrid.SelectedItem is ActivityLogEntry entry)
            {
                MessageBox.Show(this,
                    $"Tijd (UTC): {entry.TimestampUtc}\nFolder: {entry.FolderPath}\nRegel: {entry.RuleName}\nBestand: {entry.FileName}\n" +
                    $"Actie: {entry.Action}\nStatus: {entry.Status}\n\nVan:\n{entry.OriginalPath}\n\nNaar:\n{entry.DestinationPath}\n\nMelding:\n{entry.Message}",
                    "FldrSrtr — details", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void RefreshActivity()
        {
            _allActivityEntries = _activityLogger.ReadAll().OrderByDescending(entry => entry.TimestampUtc).ToList();

            string previousStatus = ActivityStatusFilter.SelectedItem as string;
            string previousRule = ActivityRuleFilter.SelectedItem as string;
            string previousFolder = ActivityFolderFilter.SelectedItem as string;

            ActivityStatusFilter.ItemsSource = new[] { "(alle)" }.Concat(_allActivityEntries.Select(e => e.Status).Distinct().OrderBy(s => s)).ToList();
            ActivityRuleFilter.ItemsSource = new[] { "(alle)" }.Concat(_allActivityEntries.Select(e => e.RuleName).Where(r => r != null).Distinct().OrderBy(r => r)).ToList();
            ActivityFolderFilter.ItemsSource = new[] { "(alle)" }.Concat(_allActivityEntries.Select(e => e.FolderPath).Where(f => f != null).Distinct().OrderBy(f => f)).ToList();

            ActivityStatusFilter.SelectedItem = previousStatus ?? "(alle)";
            ActivityRuleFilter.SelectedItem = previousRule ?? "(alle)";
            ActivityFolderFilter.SelectedItem = previousFolder ?? "(alle)";

            ApplyActivityFilter();
            RefreshDashboard();
        }

        private void ActivityFilter_Changed(object sender, EventArgs e) => ApplyActivityFilter();

        private void ApplyActivityFilter()
        {
            if (ActivityGrid == null || _allActivityEntries == null)
            {
                return;
            }

            IEnumerable<ActivityLogEntry> filtered = _allActivityEntries;

            string search = ActivitySearchBox?.Text?.Trim();
            if (!string.IsNullOrEmpty(search))
            {
                filtered = filtered.Where(e =>
                    (e.FileName?.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0) ||
                    (e.RuleName?.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0) ||
                    (e.FolderPath?.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0));
            }

            if (ActivityStatusFilter.SelectedItem is string status && status != "(alle)")
            {
                filtered = filtered.Where(e => e.Status == status);
            }
            if (ActivityRuleFilter.SelectedItem is string ruleName && ruleName != "(alle)")
            {
                filtered = filtered.Where(e => e.RuleName == ruleName);
            }
            if (ActivityFolderFilter.SelectedItem is string folderPath && folderPath != "(alle)")
            {
                filtered = filtered.Where(e => e.FolderPath == folderPath);
            }

            ActivityGrid.ItemsSource = filtered.ToList();
        }

        private void UndoLastAction_Click(object sender, RoutedEventArgs e)
        {
            var undoneIds = new HashSet<string>(_allActivityEntries.Where(x => x.UndoOfId != null).Select(x => x.UndoOfId));

            ActivityLogEntry target = _allActivityEntries.FirstOrDefault(entry =>
                entry.Status == "SUCCESS" &&
                UndoService.SupportsUndo(ParseActionType(entry.Action)) &&
                !undoneIds.Contains(entry.Id));

            if (target == null)
            {
                MessageBox.Show(this, "Geen ongedaan te maken actie gevonden.", "FldrSrtr", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            MessageBoxResult confirm = MessageBox.Show(this,
                $"Laatste actie ongedaan maken?\n\n{target.Action}: {target.OriginalPath} -> {target.DestinationPath}",
                "FldrSrtr", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (confirm != MessageBoxResult.Yes)
            {
                return;
            }

            var undoService = new UndoService(new FileSystemFileOperations(_fileSystem, BuildGuard()));
            UndoResult result = undoService.Undo(new UndoableAction
            {
                ActionType = ParseActionType(target.Action),
                OriginalPath = target.OriginalPath,
                NewPath = target.DestinationPath
            });

            _activityLogger.Append(new ActivityLogEntry
            {
                FolderPath = target.FolderPath,
                RuleName = target.RuleName,
                FileName = target.FileName,
                Action = "Undo",
                Status = result.Success ? "SUCCESS" : "ERROR",
                Message = result.Success ? $"Undo of {target.Action}" : result.ErrorMessage,
                OriginalPath = target.DestinationPath,
                DestinationPath = target.OriginalPath,
                UndoOfId = target.Id
            });

            RefreshActivity();

            if (!result.Success)
            {
                MessageBox.Show(this, $"Ongedaan maken mislukt: {result.ErrorMessage}", "FldrSrtr", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private static ActionType ParseActionType(string action) =>
            Enum.TryParse(action, out ActionType type) ? type : (ActionType)(-1);

        // ===================== Dashboard =====================

        private void PeriodComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e) => RefreshDashboard();

        private void RefreshDashboard()
        {
            if (PeriodComboBox?.SelectedItem == null)
            {
                return;
            }

            var period = (StatisticsPeriod)PeriodComboBox.SelectedItem;
            ActivityStatistics stats = StatisticsAggregator.Aggregate(_allActivityEntries, period);

            StatFilesProcessed.Text = stats.FilesProcessed.ToString();
            StatMoved.Text = stats.Moved.ToString();
            StatCopied.Text = stats.Copied.ToString();
            StatRenamed.Text = stats.Renamed.ToString();
            StatDeleted.Text = stats.Deleted.ToString();
            StatErrors.Text = stats.Errors.ToString();
            StatDataMoved.Text = FormatBytes(stats.DataMovedBytes);

            StatsPerRuleList.ItemsSource = stats.ActionsPerRule.OrderByDescending(kv => kv.Value)
                .Select(kv => $"{kv.Key}: {kv.Value}").ToList();
            StatsPerFolderList.ItemsSource = stats.ActionsPerFolder.OrderByDescending(kv => kv.Value)
                .Select(kv => $"{kv.Key}: {kv.Value}").ToList();
        }

        private static string FormatBytes(long bytes)
        {
            string[] units = { "B", "KB", "MB", "GB", "TB" };
            double value = bytes;
            int unitIndex = 0;
            while (value >= 1024 && unitIndex < units.Length - 1)
            {
                value /= 1024;
                unitIndex++;
            }
            return $"{value:0.#} {units[unitIndex]}";
        }

        // ===================== Settings =====================

        private void LoadSettingsIntoUi()
        {
            ConfirmationThresholdTextBox.Text = _config.Settings.ConfirmationThreshold.ToString();
            MaxFilesPerRunTextBox.Text = _config.Settings.MaxFilesPerRun.ToString();
            ProtectedFoldersListBox.ItemsSource = _config.Settings.ProtectedFolders;
            ProtectedExtensionsListBox.ItemsSource = _config.Settings.ProtectedExtensions;
        }

        private void SaveSettings_Click(object sender, RoutedEventArgs e)
        {
            if (!int.TryParse(ConfirmationThresholdTextBox.Text, out int threshold) || threshold < 0)
            {
                MessageBox.Show(this, "Confirmation threshold moet een positief getal zijn.", "FldrSrtr", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (!int.TryParse(MaxFilesPerRunTextBox.Text, out int maxFiles) || maxFiles < 1)
            {
                MessageBox.Show(this, "Max files per run moet minstens 1 zijn.", "FldrSrtr", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            _config.Settings.ConfirmationThreshold = threshold;
            _config.Settings.MaxFilesPerRun = maxFiles;
            SaveConfig();
            MessageBox.Show(this, "Instellingen opgeslagen.", "FldrSrtr", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void BrowseProtectedFolder_Click(object sender, RoutedEventArgs e)
        {
            string path = ModernFolderPicker.PickFolder("Selecteer een beschermde map");
            if (path != null)
            {
                NewProtectedFolderTextBox.Text = path;
            }
        }

        private void AddProtectedFolder_Click(object sender, RoutedEventArgs e)
        {
            string path = NewProtectedFolderTextBox.Text.Trim();
            if (string.IsNullOrEmpty(path))
            {
                return;
            }
            _config.Settings.ProtectedFolders.Add(path);
            NewProtectedFolderTextBox.Clear();
            SaveConfig();
        }

        private void RemoveProtectedFolder_Click(object sender, RoutedEventArgs e)
        {
            if (((FrameworkElement)sender).DataContext is string path)
            {
                _config.Settings.ProtectedFolders.Remove(path);
                SaveConfig();
            }
        }

        private void AddProtectedExtension_Click(object sender, RoutedEventArgs e)
        {
            string ext = NewProtectedExtensionTextBox.Text.Trim().TrimStart('.');
            if (string.IsNullOrEmpty(ext))
            {
                return;
            }
            _config.Settings.ProtectedExtensions.Add(ext);
            NewProtectedExtensionTextBox.Clear();
            SaveConfig();
        }

        private void RemoveProtectedExtension_Click(object sender, RoutedEventArgs e)
        {
            if (((FrameworkElement)sender).DataContext is string ext)
            {
                _config.Settings.ProtectedExtensions.Remove(ext);
                SaveConfig();
            }
        }

        private void ExportConfig_Click(object sender, RoutedEventArgs e)
        {
            using (var dialog = new System.Windows.Forms.SaveFileDialog { Filter = "JSON (*.json)|*.json", FileName = "fldrsrtr-config-export.json" })
            {
                if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    _importExportService.ExportConfig(_config, dialog.FileName);
                }
            }
        }

        private void ImportConfig_Click(object sender, RoutedEventArgs e)
        {
            MessageBoxResult confirm = MessageBox.Show(this,
                "Dit vervangt de volledige huidige configuratie (folders, regels en instellingen). Doorgaan?",
                "FldrSrtr", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (confirm != MessageBoxResult.Yes)
            {
                return;
            }

            using (var dialog = new System.Windows.Forms.OpenFileDialog { Filter = "JSON (*.json)|*.json" })
            {
                if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    try
                    {
                        _config = _importExportService.ImportConfig(dialog.FileName);
                        SaveConfig();
                        FoldersList.ItemsSource = _config.Folders;
                        LoadSettingsIntoUi();
                        RefreshActivity();
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(this, $"Import mislukt: {ex.Message}", "FldrSrtr", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
        }
    }
}
