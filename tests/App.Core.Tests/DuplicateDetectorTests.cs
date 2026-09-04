using System.Collections.Generic;
using App.Core.Evaluation;
using App.Core.Execution;
using App.Core.Model;
using FluentAssertions;
using Xunit;

namespace App.Core.Tests
{
    public class DuplicateDetectorTests
    {
        private class FakeHashOps : IFileOperations
        {
            public Dictionary<string, string> Hashes { get; } = new Dictionary<string, string>();
            public bool FileExists(string path) => false;
            public void Move(string sourcePath, string destinationPath) { }
            public void Copy(string sourcePath, string destinationPath) { }
            public void DeleteToRecycleBin(string path) { }
            public string ComputeSha256(string path) => Hashes[path];
            public void CreateDirectory(string path) { }
            public void AddToZip(string filePath, string zipPath) { }
            public void OpenFile(string path) { }
            public void OpenFileWith(string applicationPath, string filePath) { }
            public void ExecuteExternal(string executablePath, string arguments) { }
        }

        private static FileEntry MakeFile(string path, long size) => new FileEntry { FullPath = path, Name = path, SizeBytes = size };

        [Fact]
        public void MarkDuplicates_SameSizeAndHash_FlagsAllButFirst()
        {
            var a = MakeFile(@"C:\a.pdf", 100);
            var b = MakeFile(@"C:\b.pdf", 100);
            var c = MakeFile(@"C:\c.pdf", 100);
            var fileOps = new FakeHashOps();
            fileOps.Hashes[a.FullPath] = "HASH1";
            fileOps.Hashes[b.FullPath] = "HASH1";
            fileOps.Hashes[c.FullPath] = "HASH1";

            DuplicateDetector.MarkDuplicates(new[] { a, b, c }, fileOps);

            a.IsDuplicate.Should().BeFalse();
            b.IsDuplicate.Should().BeTrue();
            c.IsDuplicate.Should().BeTrue();
        }

        [Fact]
        public void MarkDuplicates_DifferentSize_NeverHashed()
        {
            var a = MakeFile(@"C:\a.pdf", 100);
            var b = MakeFile(@"C:\b.pdf", 200);
            var fileOps = new FakeHashOps(); // no hashes registered -> would throw if looked up

            DuplicateDetector.MarkDuplicates(new[] { a, b }, fileOps);

            a.IsDuplicate.Should().BeFalse();
            b.IsDuplicate.Should().BeFalse();
        }

        [Fact]
        public void MarkDuplicates_SameSizeDifferentHash_NotFlagged()
        {
            var a = MakeFile(@"C:\a.pdf", 100);
            var b = MakeFile(@"C:\b.pdf", 100);
            var fileOps = new FakeHashOps();
            fileOps.Hashes[a.FullPath] = "HASH1";
            fileOps.Hashes[b.FullPath] = "HASH2";

            DuplicateDetector.MarkDuplicates(new[] { a, b }, fileOps);

            a.IsDuplicate.Should().BeFalse();
            b.IsDuplicate.Should().BeFalse();
        }

        [Fact]
        public void RuleChecksDuplicates_FindsLeafAnywhereInTree()
        {
            var rule = new Rule { RootCondition = ConditionNode.NewGroup() };
            var nested = ConditionNode.NewGroup(GroupLogic.Any);
            nested.Children.Add(ConditionNode.NewLeaf(ConditionField.Duplicate, ConditionOperator.Equals, "true"));
            rule.RootCondition.Children.Add(nested);

            DuplicateDetector.RuleChecksDuplicates(rule.RootCondition).Should().BeTrue();
        }

        [Fact]
        public void RuleChecksDuplicates_WithoutDuplicateLeaf_ReturnsFalse()
        {
            var rule = new Rule { RootCondition = ConditionNode.NewGroup() };
            rule.RootCondition.Children.Add(ConditionNode.NewLeaf(ConditionField.Extension, ConditionOperator.Equals, "pdf"));

            DuplicateDetector.RuleChecksDuplicates(rule.RootCondition).Should().BeFalse();
        }
    }
}
