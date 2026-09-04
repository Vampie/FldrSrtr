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
        public void Scan_IsNotRecursive()
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
    }
}
