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

        private class ThrowingFileOperations : IFileOperations
        {
            public bool FileExists(string path) => false;
            public void Move(string sourcePath, string destinationPath) => throw new System.IO.IOException("bestand is in gebruik");
            public void Copy(string sourcePath, string destinationPath) { }
            public void DeleteToRecycleBin(string path) { }
        }
    }
}
