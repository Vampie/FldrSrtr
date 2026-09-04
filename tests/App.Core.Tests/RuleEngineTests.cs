using System.Collections.Generic;
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

        private static FileEntry MakeFile(string directory, string name) => new FileEntry
        {
            FullPath = System.IO.Path.Combine(directory, name),
            Directory = directory,
            Name = name,
            Extension = System.IO.Path.GetExtension(name).TrimStart('.')
        };

        [Fact]
        public void DryRun_NeverCallsMutatingOperations()
        {
            var fileOps = new FakeFileOperations();
            var engine = new RuleEngine(fileOps);
            var file = MakeFile(@"C:\Downloads", "invoice.pdf");
            var action = new RuleAction { Type = ActionType.Move, Destination = @"D:\Archive" };

            PlannedAction plan = engine.PlanAction(file, action);
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

            PlannedAction plan = engine.PlanAction(file, action);
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

            PlannedAction plan = engine.PlanAction(file, action);

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

            PlannedAction plan = engine.PlanAction(file, action);
            ExecutionResult result = engine.Execute(plan, dryRun: false);

            plan.Skipped.Should().BeTrue();
            result.Success.Should().BeTrue();
            fileOps.Moved.Should().BeEmpty();
        }

        [Fact]
        public void Execute_DeleteToRecycleBin_DoesNotNeedDestination()
        {
            var fileOps = new FakeFileOperations();
            var engine = new RuleEngine(fileOps);
            var file = MakeFile(@"C:\Downloads", "old.tmp");
            var action = new RuleAction { Type = ActionType.DeleteToRecycleBin };

            PlannedAction plan = engine.PlanAction(file, action);
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

            PlannedAction plan = engine.PlanAction(file, action);
            ExecutionResult result = engine.Execute(plan, dryRun: false);

            result.Success.Should().BeFalse();
            result.ErrorMessage.Should().NotBeNullOrEmpty();
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
