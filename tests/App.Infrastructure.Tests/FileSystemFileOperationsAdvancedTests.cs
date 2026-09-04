using System;
using System.IO;
using System.IO.Abstractions;
using System.IO.Abstractions.TestingHelpers;
using System.IO.Compression;
using App.Infrastructure.Execution;
using App.Infrastructure.Safety;
using FluentAssertions;
using Xunit;

namespace App.Infrastructure.Tests
{
    public class FileSystemFileOperationsAdvancedTests
    {
        [Fact]
        public void ComputeSha256_SameContent_ProducesSameHash()
        {
            var fileSystem = new MockFileSystem();
            fileSystem.AddFile(@"C:\a.txt", new MockFileData("hello world"));
            fileSystem.AddFile(@"C:\b.txt", new MockFileData("hello world"));
            fileSystem.AddFile(@"C:\c.txt", new MockFileData("different"));
            var ops = new FileSystemFileOperations(fileSystem, new ProtectedPathGuard());

            string hashA = ops.ComputeSha256(@"C:\a.txt");
            string hashB = ops.ComputeSha256(@"C:\b.txt");
            string hashC = ops.ComputeSha256(@"C:\c.txt");

            hashA.Should().Be(hashB);
            hashA.Should().NotBe(hashC);
        }

        [Fact]
        public void CreateDirectory_ToProtectedPath_ThrowsAndDoesNotCreate()
        {
            var fileSystem = new MockFileSystem();
            var ops = new FileSystemFileOperations(fileSystem, new ProtectedPathGuard());

            Action act = () => ops.CreateDirectory(@"C:\Windows\NewFolder");

            act.Should().Throw<InvalidOperationException>();
            fileSystem.Directory.Exists(@"C:\Windows\NewFolder").Should().BeFalse();
        }

        [Fact]
        public void CreateDirectory_OrdinaryPath_CreatesIt()
        {
            var fileSystem = new MockFileSystem();
            var ops = new FileSystemFileOperations(fileSystem, new ProtectedPathGuard());

            ops.CreateDirectory(@"C:\Archive\2026");

            fileSystem.Directory.Exists(@"C:\Archive\2026").Should().BeTrue();
        }

        [Fact]
        public void AddToZip_CreatesArchiveAndAddsFile()
        {
            string tempDir = Path.Combine(Path.GetTempPath(), "FldrSrtrTests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);
            try
            {
                string sourceFile = Path.Combine(tempDir, "invoice.pdf");
                File.WriteAllText(sourceFile, "dummy content");
                string zipPath = Path.Combine(tempDir, "archive.zip");

                var ops = new FileSystemFileOperations(new FileSystem(), new ProtectedPathGuard());
                ops.AddToZip(sourceFile, zipPath);

                File.Exists(zipPath).Should().BeTrue();
                using (var archive = ZipFile.OpenRead(zipPath))
                {
                    archive.Entries.Should().ContainSingle(e => e.Name == "invoice.pdf");
                }
            }
            finally
            {
                Directory.Delete(tempDir, recursive: true);
            }
        }
    }
}
