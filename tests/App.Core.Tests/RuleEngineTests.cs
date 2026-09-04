using System.Collections.Generic;
using System.Collections.ObjectModel;
using App.Core.Execution;
using App.Core.Model;
using FluentAssertions;
using Xunit;

namespace App.Core.Tests
{
    public class RuleEngineTests
    {
        private class FakeFileOperations : IFileOperations
        {
            public HashSet<string> ExistingFiles { get; } = new HashSet<string>();
            public List<(string From, string To)> Moved { get; } = new List<(string, string)>();
            public List<(string From, string To)> Copied { get; } = new List<(string, string)>();
            public List<string> DeletedToRecycleBin { get; } = new List<string>();
            public Dictionary<string, string> Hashes { get; } = new Dictionary<string, string>();
            public List<string> CreatedDirectories { get; } = new List<string>();
            public List<(string File, string Zip)> ZippedFiles { get; } = new List<(string, string)>();
            public List<string> OpenedFiles { get; } = new List<string>();
            public List<(string App, string File)> OpenedWith { get; } = new List<(string, string)>();
            public List<(string Exe, string Args)> ExecutedExternal { get; } = new List<(string, string)>();

            public bool FileExists(string path) => ExistingFiles.Contains(path);

            public void Move(string sourcePath, string destinationPath)
            {
                Moved.Add((sourcePath, destinationPath));
                ExistingFiles.Remove(sourcePath);
                ExistingFiles.Add(destinationPath);
            }

            public void Copy(string sourcePath, string destinationPath)
            {
                Copied.Add((sourcePath, destinationPath));
                ExistingFiles.Add(destinationPath);
            }

            public void DeleteToRecycleBin(string path)
            {
                DeletedToRecycleBin.Add(path);
                ExistingFiles.Remove(path);
            }

            public string ComputeSha256(string path) => Hashes.TryGetValue(path, out string hash) ? hash : path;
            public void CreateDirectory(string path) => CreatedDirectories.Add(path);
            public void AddToZip(string filePath, string zipPath) => ZippedFiles.Add((filePath, zipPath));
            public void OpenFile(string path) => OpenedFiles.Add(path);
            public void OpenFileWith(string applicationPath, string filePath) => OpenedWith.Add((applicationPath, filePath));
            public void ExecuteExternal(string executablePath, string arguments) => ExecutedExternal.Add((executablePath, arguments));
        }

        private class FixedConflictPrompt : IConflictPrompt
        {
            private readonly ConflictResolution _decision;
            public FixedConflictPrompt(ConflictResolution decision) => _decision = decision;
            public ConflictResolution Resolve(string existingPath, string incomingPath) => _decision;
        }

        private static FileEntry MakeFile(string directory, string name) => new FileEntry
        {
            FullPath = System.IO.Path.Combine(directory, name),
            Directory = directory,
            Name = name,
            Extension = System.IO.Path.GetExtension(name).TrimStart('.')
        };

        private static Rule MakeMatchAllRule(params RuleAction[] actions)
        {
            var rule = new Rule { RootCondition = ConditionNode.NewGroup() };
            rule.RootCondition.Children.Add(new ConditionNode
            {
                Field = ConditionField.Extension,
                Operator = ConditionOperator.Equals,
                Value = "pdf"
            });
            foreach (RuleAction action in actions)
            {
                rule.Actions.Add(action);
            }
            return rule;
        }

        [Fact]
        public void DryRun_NeverCallsMutatingOperations()
        {
            var fileOps = new FakeFileOperations();
            var engine = new RuleEngine(fileOps);
            var file = MakeFile(@"C:\Downloads", "invoice.pdf");
            var action = new RuleAction { Type = ActionType.Move, Destination = @"D:\Archive" };

            PlannedAction plan = engine.PlanAction(file, action, file.FullPath);
            ExecutionResult result = engine.Execute(plan, dryRun: true);

            result.Success.Should().BeTrue();
            plan.ResolvedDestinationPath.Should().Be(@"D:\Archive\invoice.pdf");
            fileOps.Moved.Should().BeEmpty();
        }

        [Fact]
        public void Execute_Move_CallsFileOperationsWithResolvedPath()
        {
            var fileOps = new FakeFileOperations();
            var engine = new RuleEngine(fileOps);
            var file = MakeFile(@"C:\Downloads", "invoice.pdf");
            var action = new RuleAction { Type = ActionType.Move, Destination = @"D:\Archive" };

            PlannedAction plan = engine.PlanAction(file, action, file.FullPath);
            ExecutionResult result = engine.Execute(plan, dryRun: false);

            result.Success.Should().BeTrue();
            fileOps.Moved.Should().ContainSingle(m => m.From == file.FullPath && m.To == @"D:\Archive\invoice.pdf");
        }

        [Fact]
        public void PlanAction_ConflictWithRename_AppendsNumericSuffix()
        {
            var fileOps = new FakeFileOperations();
            fileOps.ExistingFiles.Add(@"D:\Archive\invoice.pdf");
            var engine = new RuleEngine(fileOps);
            var file = MakeFile(@"C:\Downloads", "invoice.pdf");
            var action = new RuleAction { Type = ActionType.Move, Destination = @"D:\Archive", OnConflict = ConflictResolution.Rename };

            PlannedAction plan = engine.PlanAction(file, action, file.FullPath);

            plan.ResolvedDestinationPath.Should().Be(@"D:\Archive\invoice (1).pdf");
        }

        [Fact]
        public void PlanAction_ConflictWithSkip_MarksPlanAsSkipped()
        {
            var fileOps = new FakeFileOperations();
            fileOps.ExistingFiles.Add(@"D:\Archive\invoice.pdf");
            var engine = new RuleEngine(fileOps);
            var file = MakeFile(@"C:\Downloads", "invoice.pdf");
            var action = new RuleAction { Type = ActionType.Move, Destination = @"D:\Archive", OnConflict = ConflictResolution.Skip };

            PlannedAction plan = engine.PlanAction(file, action, file.FullPath);
            ExecutionResult result = engine.Execute(plan, dryRun: false);

            plan.Skipped.Should().BeTrue();
            result.Success.Should().BeTrue();
            fileOps.Moved.Should().BeEmpty();
        }

        [Fact]
        public void PlanAction_ConflictWithAsk_DelegatesToConflictPrompt()
        {
            var fileOps = new FakeFileOperations();
            fileOps.ExistingFiles.Add(@"D:\Archive\invoice.pdf");
            var engine = new RuleEngine(fileOps, new FixedConflictPrompt(ConflictResolution.Overwrite));
            var file = MakeFile(@"C:\Downloads", "invoice.pdf");
            var action = new RuleAction { Type = ActionType.Move, Destination = @"D:\Archive", OnConflict = ConflictResolution.Ask };

            PlannedAction plan = engine.PlanAction(file, action, file.FullPath);

            plan.ResolvedDestinationPath.Should().Be(@"D:\Archive\invoice.pdf");
            plan.Skipped.Should().BeFalse();
        }

        [Fact]
        public void Execute_DeleteToRecycleBin_DoesNotNeedDestination()
        {
            var fileOps = new FakeFileOperations();
            var engine = new RuleEngine(fileOps);
            var file = MakeFile(@"C:\Downloads", "old.tmp");
            var action = new RuleAction { Type = ActionType.DeleteToRecycleBin };

            PlannedAction plan = engine.PlanAction(file, action, file.FullPath);
            ExecutionResult result = engine.Execute(plan, dryRun: false);

            result.Success.Should().BeTrue();
            fileOps.DeletedToRecycleBin.Should().Contain(file.FullPath);
        }

        [Fact]
        public void Execute_DeleteTargetIfExists_WhenTargetExists_DeletesItToRecycleBin()
        {
            var fileOps = new FakeFileOperations();
            fileOps.ExistingFiles.Add(@"D:\Archive\invoice.pdf");
            var engine = new RuleEngine(fileOps);
            var file = MakeFile(@"C:\Downloads", "invoice.pdf");
            var action = new RuleAction { Type = ActionType.DeleteTargetIfExists, Destination = @"D:\Archive\invoice.pdf" };

            PlannedAction plan = engine.PlanAction(file, action, file.FullPath);
            ExecutionResult result = engine.Execute(plan, dryRun: false);

            result.Success.Should().BeTrue();
            fileOps.DeletedToRecycleBin.Should().Contain(@"D:\Archive\invoice.pdf");
        }

        [Fact]
        public void Execute_DeleteTargetIfExists_WhenTargetMissing_IsANoOp()
        {
            var fileOps = new FakeFileOperations();
            var engine = new RuleEngine(fileOps);
            var file = MakeFile(@"C:\Downloads", "invoice.pdf");
            var action = new RuleAction { Type = ActionType.DeleteTargetIfExists, Destination = @"D:\Archive\invoice.pdf" };

            PlannedAction plan = engine.PlanAction(file, action, file.FullPath);
            ExecutionResult result = engine.Execute(plan, dryRun: false);

            result.Success.Should().BeTrue();
            fileOps.DeletedToRecycleBin.Should().BeEmpty();
        }

        [Fact]
        public void ExecuteRule_DeleteTargetIfExistsThenMove_ClearsTheWayForTheMove()
        {
            var fileOps = new FakeFileOperations();
            fileOps.ExistingFiles.Add(@"C:\Downloads\invoice.pdf");
            fileOps.ExistingFiles.Add(@"D:\Archive\invoice.pdf"); // stale file blocking the move
            var engine = new RuleEngine(fileOps);
            var file = MakeFile(@"C:\Downloads", "invoice.pdf");
            var rule = MakeMatchAllRule(
                new RuleAction { Type = ActionType.DeleteTargetIfExists, Destination = @"D:\Archive\invoice.pdf" },
                new RuleAction { Type = ActionType.Move, Destination = @"D:\Archive", OnConflict = ConflictResolution.Overwrite });

            List<ExecutionResult> results = engine.ExecuteRule(rule, new[] { file }, dryRun: false);

            results.Should().OnlyContain(r => r.Success);
            fileOps.DeletedToRecycleBin.Should().Contain(@"D:\Archive\invoice.pdf");
            fileOps.Moved.Should().Contain(m => m.From == @"C:\Downloads\invoice.pdf" && m.To == @"D:\Archive\invoice.pdf");
        }

        [Fact]
        public void Execute_WhenFileOperationThrows_ReturnsFailureWithoutThrowing()
        {
            var engine = new RuleEngine(new ThrowingFileOperations());
            var file = MakeFile(@"C:\Downloads", "locked.txt");
            var action = new RuleAction { Type = ActionType.Move, Destination = @"D:\Archive" };

            PlannedAction plan = engine.PlanAction(file, action, file.FullPath);
            ExecutionResult result = engine.Execute(plan, dryRun: false);

            result.Success.Should().BeFalse();
            result.ErrorMessage.Should().NotBeNullOrEmpty();
        }

        [Fact]
        public void ExecuteRule_ChainsMultipleActions_SecondActionSeesResultOfFirst()
        {
            var fileOps = new FakeFileOperations();
            var engine = new RuleEngine(fileOps);
            var file = MakeFile(@"C:\Downloads", "invoice.pdf");
            var rule = MakeMatchAllRule(
                new RuleAction { Type = ActionType.Rename, Destination = "renamed.pdf" },
                new RuleAction { Type = ActionType.Move, Destination = @"D:\Archive" });

            List<ExecutionResult> results = engine.ExecuteRule(rule, new[] { file }, dryRun: false);

            results.Should().HaveCount(2);
            results.Should().OnlyContain(r => r.Success);
            fileOps.Moved.Should().Contain(m => m.From == @"C:\Downloads\invoice.pdf" && m.To == @"C:\Downloads\renamed.pdf");
            fileOps.Moved.Should().Contain(m => m.From == @"C:\Downloads\renamed.pdf" && m.To == @"D:\Archive\renamed.pdf");
        }

        [Fact]
        public void ExecuteRule_StopsChain_WhenDeleteSucceeds()
        {
            var fileOps = new FakeFileOperations();
            var engine = new RuleEngine(fileOps);
            var file = MakeFile(@"C:\Downloads", "invoice.pdf");
            var rule = MakeMatchAllRule(
                new RuleAction { Type = ActionType.DeleteToRecycleBin },
                new RuleAction { Type = ActionType.Move, Destination = @"D:\Archive" });

            List<ExecutionResult> results = engine.ExecuteRule(rule, new[] { file }, dryRun: false);

            results.Should().HaveCount(1);
            fileOps.DeletedToRecycleBin.Should().Contain(file.FullPath);
            fileOps.Moved.Should().BeEmpty();
        }

        [Fact]
        public void ExecuteRule_SameFileTwiceInScanResults_ProcessedOnce()
        {
            var fileOps = new FakeFileOperations();
            var engine = new RuleEngine(fileOps);
            var file = MakeFile(@"C:\Downloads", "invoice.pdf");
            var duplicate = MakeFile(@"C:\Downloads", "invoice.pdf");
            var rule = MakeMatchAllRule(new RuleAction { Type = ActionType.Move, Destination = @"D:\Archive" });

            List<ExecutionResult> results = engine.ExecuteRule(rule, new[] { file, duplicate }, dryRun: false);

            results.Should().HaveCount(1);
        }

        [Fact]
        public void PlanAction_Move_RelativeDestination_AnchorsToFilesOwnDirectory_NotProcessWorkingDirectory()
        {
            // Reported bug: a Destination like "{Year}_{Month}\{Day}\{FileName}_1.{Extension}"
            // (no drive letter, no {Directory} token) used to resolve as a bare relative path,
            // which .NET treats as relative to the process's working directory — for a portable
            // exe, that's wherever it's run from, so files ended up in subfolders next to the exe
            // instead of next to the source file.
            var fileOps = new FakeFileOperations();
            var engine = new RuleEngine(fileOps);
            var file = MakeFile(@"C:\Downloads", "invoice.pdf");
            var action = new RuleAction { Type = ActionType.Move, Destination = @"Archive\{Year}" };
            var now = new System.DateTime(2026, 3, 5);

            PlannedAction plan = engine.PlanAction(file, action, file.FullPath, now);

            plan.ResolvedDestinationPath.Should().Be(@"C:\Downloads\Archive\2026\invoice.pdf");
        }

        [Fact]
        public void PlanAction_Move_AbsoluteDestination_IsUsedAsIs()
        {
            var fileOps = new FakeFileOperations();
            var engine = new RuleEngine(fileOps);
            var file = MakeFile(@"C:\Downloads", "invoice.pdf");
            var action = new RuleAction { Type = ActionType.Move, Destination = @"D:\Archive" };

            PlannedAction plan = engine.PlanAction(file, action, file.FullPath);

            plan.ResolvedDestinationPath.Should().Be(@"D:\Archive\invoice.pdf");
        }

        [Fact]
        public void PlanAction_Move_DestinationWithParentDirectoryToken_ResolvesDosStyle()
        {
            // "..\" should behave like it does in a DOS/Windows shell: go up one directory from
            // the file's own folder, not end up as a literal ".." segment in the final path.
            var fileOps = new FakeFileOperations();
            var engine = new RuleEngine(fileOps);
            var file = MakeFile(@"D:\folder1\begin", "invoice.pdf");
            var action = new RuleAction { Type = ActionType.Move, Destination = @"..\doel" };

            PlannedAction plan = engine.PlanAction(file, action, file.FullPath);

            plan.ResolvedDestinationPath.Should().Be(@"D:\folder1\doel\invoice.pdf");
        }

        [Fact]
        public void PlanAction_Move_DateBasedDestination_IsRecomputedPerFile_NotCachedAcrossFiles()
        {
            // User's concern: with a Destination like "..\{CreatedYear}_{CreatedMonth}\..." they
            // worried the destination folder is computed once and reused for every file, so files
            // created in different months would wrongly all land in the same folder. It isn't —
            // PlanAction resolves variables fresh per file (per FileEntry's own CreatedUtc) — but
            // there was no test pinning that guarantee down explicitly, so add one using their
            // exact template shape.
            var fileOps = new FakeFileOperations();
            var engine = new RuleEngine(fileOps);
            var action = new RuleAction { Type = ActionType.Move, Destination = @"..\{CreatedYear}_{CreatedMonth}\{OriginalName}.{OriginalExtension}" };

            var januaryFile = MakeFile(@"D:\folder1\begin", "invoice.pdf");
            januaryFile.CreatedUtc = new System.DateTime(2026, 1, 15);
            var marchFile = MakeFile(@"D:\folder1\begin", "receipt.pdf");
            marchFile.CreatedUtc = new System.DateTime(2026, 3, 20);

            PlannedAction planJanuary = engine.PlanAction(januaryFile, action, januaryFile.FullPath);
            PlannedAction planMarch = engine.PlanAction(marchFile, action, marchFile.FullPath);

            planJanuary.ResolvedDestinationPath.Should().Be(@"D:\folder1\2026_01\invoice.pdf");
            planMarch.ResolvedDestinationPath.Should().Be(@"D:\folder1\2026_03\receipt.pdf");
        }

        [Fact]
        public void PlanAction_CreateFolder_RelativeDestination_AnchorsToFilesOwnDirectory()
        {
            var fileOps = new FakeFileOperations();
            var engine = new RuleEngine(fileOps);
            var file = MakeFile(@"C:\Downloads", "invoice.pdf");
            var action = new RuleAction { Type = ActionType.CreateFolder, Destination = @"Archive\{Year}" };
            var now = new System.DateTime(2026, 3, 5);

            PlannedAction plan = engine.PlanAction(file, action, file.FullPath, now);

            plan.ResolvedDestinationPath.Should().Be(@"C:\Downloads\Archive\2026");
        }

        [Fact]
        public void PlanAction_Move_ResolvesVariablesInDestination()
        {
            var fileOps = new FakeFileOperations();
            var engine = new RuleEngine(fileOps);
            var file = MakeFile(@"C:\Downloads", "invoice.pdf");
            var action = new RuleAction { Type = ActionType.Move, Destination = @"D:\Archive\{Year}\{Month}" };
            var now = new System.DateTime(2026, 3, 5);

            PlannedAction plan = engine.PlanAction(file, action, file.FullPath, now);

            plan.ResolvedDestinationPath.Should().Be(@"D:\Archive\2026\03\invoice.pdf");
        }

        [Fact]
        public void PlanAction_Move_DestinationWithFileNameToken_UsedAsFullPath()
        {
            var fileOps = new FakeFileOperations();
            var engine = new RuleEngine(fileOps);
            var file = MakeFile(@"C:\Downloads", "invoice.pdf");
            var action = new RuleAction { Type = ActionType.Move, Destination = @"D:\Archive\{Year}\{FileName}.{Extension}" };
            var now = new System.DateTime(2026, 3, 5);

            PlannedAction plan = engine.PlanAction(file, action, file.FullPath, now);

            plan.ResolvedDestinationPath.Should().Be(@"D:\Archive\2026\invoice.pdf");
        }

        [Fact]
        public void PlanAction_Copy_DestinationBuiltFromNonFileNameTokens_StillTreatedAsFullPath()
        {
            // Reported bug: a Destination built entirely from tokens other than {FileName}/
            // {OriginalName} (here {Extension} + {Counter}) still ends in a real file name once
            // resolved, but the old heuristic only recognized {FileName}/{OriginalName} as "this
            // is a full path" — so it treated "tst\html_9.html" as a *folder* and appended the
            // original filename underneath it, producing ".../tst/html_9.html/original.html".
            var fileOps = new FakeFileOperations();
            var engine = new RuleEngine(fileOps);
            var file = MakeFile(@"C:\Downloads", "2024_ARCHIVE_landing___backup_026.html");
            var action = new RuleAction { Type = ActionType.Copy, Destination = @"tst\{Extension}_9.{Extension}" };

            PlannedAction plan = engine.PlanAction(file, action, file.FullPath);

            plan.ResolvedDestinationPath.Should().Be(@"C:\Downloads\tst\html_9.html");
        }

        [Fact]
        public void ExecuteRule_Copy_DestinationBuiltFromCounterAndExtension_ReproducesExactReportedRule()
        {
            // The exact rule.json shape from the bug report, including the real {Counter} plumbing.
            var fileOps = new FakeFileOperations();
            var engine = new RuleEngine(fileOps);
            var file = MakeFile(@"C:\Testfiles", "2024_ARCHIVE_landing___backup_026.html");
            var rule = MakeMatchAllRule(new RuleAction { Type = ActionType.Copy, Destination = @"tst\{Extension}_{Counter:1:1}.{Extension}", OnConflict = ConflictResolution.Rename });
            rule.RootCondition.Children.Clear();
            rule.RootCondition.Children.Add(new ConditionNode { Field = ConditionField.Extension, Operator = ConditionOperator.Contains, Value = "ml" });

            List<ExecutionResult> results = engine.ExecuteRule(rule, new[] { file }, dryRun: false);

            results.Should().ContainSingle();
            results[0].Plan.ResolvedDestinationPath.Should().Be(@"C:\Testfiles\tst\html_1.html");
            fileOps.Copied.Should().ContainSingle(c => c.To == @"C:\Testfiles\tst\html_1.html");
        }

        [Fact]
        public void PlanAction_Move_DestinationFolderNameContainingDot_NeedsTrailingSlashToStayAFolder()
        {
            // The extension-sniffing heuristic's known edge case: a genuine destination *folder*
            // whose name contains a dot looks like it has a file extension. A trailing slash is
            // the escape hatch that forces the folder interpretation.
            var fileOps = new FakeFileOperations();
            var engine = new RuleEngine(fileOps);
            var file = MakeFile(@"C:\Downloads", "invoice.pdf");
            var action = new RuleAction { Type = ActionType.Move, Destination = @"D:\Archive\v1.2\" };

            PlannedAction plan = engine.PlanAction(file, action, file.FullPath);

            plan.ResolvedDestinationPath.Should().Be(@"D:\Archive\v1.2\invoice.pdf");
        }

        [Fact]
        public void Execute_AddExtension_AppendsToCurrentName()
        {
            var fileOps = new FakeFileOperations();
            var engine = new RuleEngine(fileOps);
            var file = MakeFile(@"C:\Downloads", "invoice.pdf");
            var action = new RuleAction { Type = ActionType.AddExtension, Destination = "bak" };

            PlannedAction plan = engine.PlanAction(file, action, file.FullPath);
            engine.Execute(plan, dryRun: false);

            plan.ResolvedDestinationPath.Should().Be(@"C:\Downloads\invoice.pdf.bak");
            fileOps.Moved.Should().Contain(m => m.To == @"C:\Downloads\invoice.pdf.bak");
        }

        [Fact]
        public void Execute_RemoveExtension_StripsCurrentExtension()
        {
            var fileOps = new FakeFileOperations();
            var engine = new RuleEngine(fileOps);
            var file = MakeFile(@"C:\Downloads", "invoice.pdf");
            var action = new RuleAction { Type = ActionType.RemoveExtension };

            PlannedAction plan = engine.PlanAction(file, action, file.FullPath);
            engine.Execute(plan, dryRun: false);

            plan.ResolvedDestinationPath.Should().Be(@"C:\Downloads\invoice");
        }

        [Fact]
        public void Execute_CreateFolder_ResolvesVariablesAndCallsCreateDirectory()
        {
            var fileOps = new FakeFileOperations();
            var engine = new RuleEngine(fileOps);
            var file = MakeFile(@"C:\Downloads", "invoice.pdf");
            var action = new RuleAction { Type = ActionType.CreateFolder, Destination = @"D:\Archive\{Year}" };
            var now = new System.DateTime(2026, 3, 5);

            PlannedAction plan = engine.PlanAction(file, action, file.FullPath, now);
            engine.Execute(plan, dryRun: false);

            fileOps.CreatedDirectories.Should().Contain(@"D:\Archive\2026");
        }

        [Fact]
        public void Execute_Zip_AddsFileToArchivePath()
        {
            var fileOps = new FakeFileOperations();
            var engine = new RuleEngine(fileOps);
            var file = MakeFile(@"C:\Downloads", "invoice.pdf");
            var action = new RuleAction { Type = ActionType.Zip, Destination = @"D:\Archive\{Year}.zip" };
            var now = new System.DateTime(2026, 3, 5);

            PlannedAction plan = engine.PlanAction(file, action, file.FullPath, now);
            engine.Execute(plan, dryRun: false);

            fileOps.ZippedFiles.Should().ContainSingle(z => z.File == file.FullPath && z.Zip == @"D:\Archive\2026.zip");
        }

        [Fact]
        public void Execute_OpenWith_PassesApplicationAndFile()
        {
            var fileOps = new FakeFileOperations();
            var engine = new RuleEngine(fileOps);
            var file = MakeFile(@"C:\Downloads", "invoice.pdf");
            var action = new RuleAction { Type = ActionType.OpenWith, Destination = @"C:\Apps\Reader.exe" };

            PlannedAction plan = engine.PlanAction(file, action, file.FullPath);
            engine.Execute(plan, dryRun: false);

            fileOps.OpenedWith.Should().ContainSingle(o => o.App == @"C:\Apps\Reader.exe" && o.File == file.FullPath);
        }

        [Fact]
        public void Execute_ExecuteExternal_ResolvesArgumentsVariables()
        {
            var fileOps = new FakeFileOperations();
            var engine = new RuleEngine(fileOps);
            var file = MakeFile(@"C:\Downloads", "invoice.pdf");
            var action = new RuleAction { Type = ActionType.ExecuteExternal, Destination = @"C:\Tools\process.exe", Arguments = "\"{FullPath}\"" };

            PlannedAction plan = engine.PlanAction(file, action, file.FullPath);
            engine.Execute(plan, dryRun: false);

            fileOps.ExecutedExternal.Should().ContainSingle(x => x.Exe == @"C:\Tools\process.exe" && x.Args == $"\"{file.FullPath}\"");
        }

        [Fact]
        public void ExecuteRule_DuplicateCondition_OnlyFlaggedCopyGetsMoved()
        {
            var fileOps = new FakeFileOperations();
            fileOps.Hashes[@"C:\Downloads\a.pdf"] = "SAME";
            fileOps.Hashes[@"C:\Downloads\b.pdf"] = "SAME";
            var engine = new RuleEngine(fileOps);
            var a = MakeFile(@"C:\Downloads", "a.pdf");
            var b = MakeFile(@"C:\Downloads", "b.pdf");

            var rule = new Rule { RootCondition = ConditionNode.NewGroup() };
            rule.RootCondition.Children.Add(ConditionNode.NewLeaf(ConditionField.Duplicate, ConditionOperator.Equals, "true"));
            rule.Actions.Add(new RuleAction { Type = ActionType.Move, Destination = @"D:\Duplicates" });

            List<ExecutionResult> results = engine.ExecuteRule(rule, new[] { a, b }, dryRun: false);

            results.Should().HaveCount(1);
            fileOps.Moved.Should().ContainSingle(m => m.From == @"C:\Downloads\b.pdf");
        }

        [Fact]
        public void ExecuteRule_Counter_IncrementsOncePerFile_AcrossMatches()
        {
            var fileOps = new FakeFileOperations();
            var engine = new RuleEngine(fileOps);
            var a = MakeFile(@"C:\Downloads", "a.pdf");
            var b = MakeFile(@"C:\Downloads", "b.pdf");
            var c = MakeFile(@"C:\Downloads", "c.pdf");
            var rule = MakeMatchAllRule(new RuleAction { Type = ActionType.Move, Destination = @"D:\Archive\{Counter:100:5}" });

            engine.ExecuteRule(rule, new[] { a, b, c }, dryRun: false);

            fileOps.Moved.Should().Contain(m => m.To == @"D:\Archive\100\a.pdf");
            fileOps.Moved.Should().Contain(m => m.To == @"D:\Archive\105\b.pdf");
            fileOps.Moved.Should().Contain(m => m.To == @"D:\Archive\110\c.pdf");
        }

        [Fact]
        public void ExecuteRule_Counter_SameValueAcrossChainedActionsForOneFile()
        {
            var fileOps = new FakeFileOperations();
            var engine = new RuleEngine(fileOps);
            var file = MakeFile(@"C:\Downloads", "invoice.pdf");
            var rule = MakeMatchAllRule(
                new RuleAction { Type = ActionType.Rename, Destination = "renamed_{Counter}.pdf" },
                new RuleAction { Type = ActionType.CreateFolder, Destination = @"D:\Archive\{Counter}" });

            List<ExecutionResult> results = engine.ExecuteRule(rule, new[] { file }, dryRun: false);

            fileOps.Moved.Should().ContainSingle(m => m.To == @"C:\Downloads\renamed_1.pdf");
            fileOps.CreatedDirectories.Should().ContainSingle(d => d == @"D:\Archive\1");
        }

        [Fact]
        public void ExecuteRule_Counter_DoesNotPersistAcrossSeparateExecuteRuleCalls()
        {
            var fileOps = new FakeFileOperations();
            var engine = new RuleEngine(fileOps);
            var a = MakeFile(@"C:\Downloads", "a.pdf");
            var rule = MakeMatchAllRule(new RuleAction { Type = ActionType.Move, Destination = @"D:\Archive\{Counter}" });

            engine.ExecuteRule(rule, new[] { a }, dryRun: false);
            fileOps.ExistingFiles.Clear(); // reset as if files were freshly rescanned
            var aAgain = MakeFile(@"C:\Downloads", "a.pdf");
            engine.ExecuteRule(rule, new[] { aAgain }, dryRun: false);

            // Both separate runs start the counter fresh at 1 — it must never be remembered on the engine instance.
            fileOps.Moved.Should().OnlyContain(m => m.To == @"D:\Archive\1\a.pdf");
        }

        private class ThrowingFileOperations : IFileOperations
        {
            public bool FileExists(string path) => false;
            public void Move(string sourcePath, string destinationPath) => throw new System.IO.IOException("bestand is in gebruik");
            public void Copy(string sourcePath, string destinationPath) { }
            public void DeleteToRecycleBin(string path) { }
            public string ComputeSha256(string path) => path;
            public void CreateDirectory(string path) { }
            public void AddToZip(string filePath, string zipPath) { }
            public void OpenFile(string path) { }
            public void OpenFileWith(string applicationPath, string filePath) { }
            public void ExecuteExternal(string executablePath, string arguments) { }
        }
    }
}
