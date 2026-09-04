using App.Core.Model;
using System.IO.Abstractions.TestingHelpers;
using App.Infrastructure.Scanning;
using FluentAssertions;
using Xunit;

namespace App.Infrastructure.Tests
{
    public class FolderScannerTests
    {
        [Fact]
        public void Scan_ReturnsFileEntriesForEachFileInFolder()
        {
            var fileSystem = new MockFileSystem();
            fileSystem.AddFile(@"C:\Downloads\invoice.pdf", new MockFileData("dummy content"));
            fileSystem.AddFile(@"C:\Downloads\notes.txt", new MockFileData("hi"));
            fileSystem.AddDirectory(@"C:\Downloads\Subfolder");

            var scanner = new FolderScanner(fileSystem);

            var entries = scanner.Scan(@"C:\Downloads");

            entries.Should().HaveCount(2);
            entries.Should().Contain(e => e.Name == "invoice.pdf" && e.Extension == "pdf");
        }

        [Fact]
        public void Scan_NonRecursiveByDefault_IgnoresSubfolders()
        {
            var fileSystem = new MockFileSystem();
            fileSystem.AddFile(@"C:\Downloads\top.txt", new MockFileData("x"));
            fileSystem.AddFile(@"C:\Downloads\Sub\nested.txt", new MockFileData("x"));

            var scanner = new FolderScanner(fileSystem);

            var entries = scanner.Scan(@"C:\Downloads");

            entries.Should().ContainSingle(e => e.Name == "top.txt");
        }

        [Fact]
        public void Scan_WhenFolderMissing_ReturnsEmpty()
        {
            var scanner = new FolderScanner(new MockFileSystem());

            scanner.Scan(@"C:\DoesNotExist").Should().BeEmpty();
        }

        [Fact]
        public void Scan_Recursive_FindsFilesInSubfolders()
        {
            var fileSystem = new MockFileSystem();
            fileSystem.AddFile(@"C:\Downloads\top.txt", new MockFileData("x"));
            fileSystem.AddFile(@"C:\Downloads\Sub\nested.txt", new MockFileData("x"));
            fileSystem.AddFile(@"C:\Downloads\Sub\Deeper\deep.txt", new MockFileData("x"));

            var scanner = new FolderScanner(fileSystem);
            var folder = new WatchedFolder { Path = @"C:\Downloads", Recursive = true, MaxRecursionDepth = 10 };

            var entries = scanner.Scan(folder);

            entries.Should().HaveCount(3);
        }

        [Fact]
        public void Scan_Recursive_RespectsMaxDepth()
        {
            var fileSystem = new MockFileSystem();
            fileSystem.AddFile(@"C:\Downloads\top.txt", new MockFileData("x"));
            fileSystem.AddFile(@"C:\Downloads\Sub\nested.txt", new MockFileData("x"));
            fileSystem.AddFile(@"C:\Downloads\Sub\Deeper\deep.txt", new MockFileData("x"));

            var scanner = new FolderScanner(fileSystem);
            var folder = new WatchedFolder { Path = @"C:\Downloads", Recursive = true, MaxRecursionDepth = 1 };

            var entries = scanner.Scan(folder);

            entries.Should().HaveCount(2);
            entries.Should().NotContain(e => e.Name == "deep.txt");
        }

        [Fact]
        public void Scan_Recursive_SkipsExcludedSubfolders()
        {
            var fileSystem = new MockFileSystem();
            fileSystem.AddFile(@"C:\Downloads\top.txt", new MockFileData("x"));
            fileSystem.AddFile(@"C:\Downloads\node_modules\lib.txt", new MockFileData("x"));

            var scanner = new FolderScanner(fileSystem);
            var folder = new WatchedFolder { Path = @"C:\Downloads", Recursive = true };
            folder.ExcludedSubfolders.Add("node_modules");

            var entries = scanner.Scan(folder);

            entries.Should().ContainSingle(e => e.Name == "top.txt");
        }

        [Fact]
        public void Scan_SkipsFilesMatchingExcludedPattern()
        {
            var fileSystem = new MockFileSystem();
            fileSystem.AddFile(@"C:\Downloads\invoice.pdf", new MockFileData("x"));
            fileSystem.AddFile(@"C:\Downloads\partial.tmp", new MockFileData("x"));

            var scanner = new FolderScanner(fileSystem);
            var folder = new WatchedFolder { Path = @"C:\Downloads" };
            folder.ExcludedFilePatterns.Add("*.tmp");

            var entries = scanner.Scan(folder);

            entries.Should().ContainSingle(e => e.Name == "invoice.pdf");
        }
    }
}
