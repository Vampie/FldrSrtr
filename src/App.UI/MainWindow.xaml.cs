using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO.Abstractions;
using System.Linq;
using System.Windows;
using App.Core.Configuration;
using App.Core.Execution;
using App.Core.Model;
using App.Infrastructure.Activity;
using App.Infrastructure.Configuration;
using App.Infrastructure.Execution;
using App.Infrastructure.Safety;
using App.Infrastructure.Scanning;

namespace FoldrSortr
{
    public partial class MainWindow : Window
    {
        private readonly ConfigService _configService = new ConfigService();
        private readonly IFileSystem _fileSystem = new FileSystem();
        private readonly ActivityLogger _activityLogger = new ActivityLogger();
        private readonly ObservableCollection<PreviewRow> _previewRows = new ObservableCollection<PreviewRow>();

        private AppConfig _config;
        private FolderScanner _scanner;

        public MainWindow()
        {
            InitializeComponent();
            ResultsGrid.ItemsSource = _previewRows;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            _config = _configService.LoadOrCreateDefault();
            _scanner = new FolderScanner(_fileSystem);
            FoldersList.ItemsSource = _config.Folders;
            RefreshActivity();
        }

        private WatchedFolder SelectedFolder => FoldersList.SelectedItem as WatchedFolder;
        private Rule SelectedRule => RulesList.SelectedItem as Rule;

        private void SaveConfig() => _configService.Save(_config);

        private void FoldersList_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            WatchedFolder folder = SelectedFolder;
            RulesList.ItemsSource = folder?.Rules;
            RulesHeaderText.Text = folder != null ? $"Rules for: {folder.Path}" : "Selecteer een folder";
            _previewRows.Clear();
            SummaryText.Text = string.Empty;
        }

        private void AddFolder_Click(object sender, RoutedEventArgs e)
        {
            using (var dialog = new System.Windows.Forms.FolderBrowserDialog())
            {
                if (dialog.ShowDialog() != System.Windows.Forms.DialogResult.OK)
                {
                    return;
                }

                _config.Folders.Add(new WatchedFolder { Path = dialog.SelectedPath });
                SaveConfig();
            }
        }

        private void RemoveFolder_Click(object sender, RoutedEventArgs e)
        {
            WatchedFolder folder = SelectedFolder;
            if (folder == null)
            {
                return;
            }

            MessageBoxResult confirm = MessageBox.Show(this, $"Folder '{folder.Path}' verwijderen uit FoldrSortr?\n(De map zelf blijft bestaan.)",
                "FoldrSortr", MessageBoxButton.YesNo, MessageBoxImage.Question);
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
                MessageBox.Show(this, "Deze map bestaat niet (meer).", "FoldrSortr", MessageBoxButton.OK, MessageBoxImage.Warning);
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

            var files = _scanner.Scan(folder.Path);
            MessageBox.Show(this, $"{files.Count} bestand(en) gevonden in {folder.Path}.", "FoldrSortr", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void AddRule_Click(object sender, RoutedEventArgs e)
        {
            WatchedFolder folder = SelectedFolder;
            if (folder == null)
            {
                MessageBox.Show(this, "Selecteer eerst een folder.", "FoldrSortr", MessageBoxButton.OK, MessageBoxImage.Warning);
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

        private void DryRun_Click(object sender, RoutedEventArgs e) => RunSelectedRule(dryRun: true);

        private void RunRule_Click(object sender, RoutedEventArgs e)
        {
            MessageBoxResult confirm = MessageBox.Show(this,
                "Deze regel echt uitvoeren? Dit wijzigt bestanden op schijf.",
                "FoldrSortr", MessageBoxButton.YesNo, MessageBoxImage.Question);
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
                MessageBox.Show(this, "Selecteer een folder en een regel.", "FoldrSortr", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var files = _scanner.Scan(folder.Path);
            var fileOps = new FileSystemFileOperations(_fileSystem, new ProtectedPathGuard());
            var engine = new RuleEngine(fileOps);
            var plans = engine.Plan(rule, files);

            _previewRows.Clear();
            int success = 0, failed = 0, skipped = 0;

            foreach (PlannedAction plan in plans)
            {
                ExecutionResult result = engine.Execute(plan, dryRun);
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
                        DestinationPath = plan.ResolvedDestinationPath
                    });
                }

                if (plan.Skipped) skipped++;
                else if (result.Success) success++;
                else failed++;
            }

            SummaryText.Text = dryRun
                ? $"{plans.Count} bestand(en) zouden worden verwerkt ({skipped} zouden worden overgeslagen)."
                : $"{plans.Count} matched, {success} success, {skipped} skipped, {failed} failed.";

            if (!dryRun)
            {
                RefreshActivity();
            }
        }

        private void RefreshActivity_Click(object sender, RoutedEventArgs e) => RefreshActivity();

        private void RefreshActivity()
        {
            ActivityGrid.ItemsSource = _activityLogger.ReadAll().OrderByDescending(entry => entry.TimestampUtc).ToList();
        }
    }
}
