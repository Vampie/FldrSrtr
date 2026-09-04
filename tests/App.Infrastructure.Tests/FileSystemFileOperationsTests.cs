using System;
using System.IO.Abstractions.TestingHelpers;
using App.Infrastructure.Execution;
using App.Infrastructure.Safety;
using FluentAssertions;
using Xunit;

namespace App.Infrastructure.Tests
{
    public class FileSystemFileOperationsTests
    {
        [Fact]
        public void Move_ToProtectedDestination_ThrowsAndDoesNotMove()
        {
            var fileSystem = new MockFileSystem();
            fileSystem.AddFile(@"C:\Downloads\app.exe", new MockFileData("x"));
            var ops = new FileSystemFileOperations(fileSystem, new ProtectedPathGuard());

            Action act = () => ops.Move(@"C:\Downloads\app.exe", @"C:\Windows\app.exe");

            act.Should().Throw<InvalidOperationException>();
            fileSystem.FileExists(@"C:\Downloads\app.exe").Should().BeTrue();
        }

        [Fact]
        public void Move_ToExistingDestination_OverwritesIt()
        {
            var fileSystem = new MockFileSystem();
            fileSystem.AddFile(@"C:\Downloads\invoice.pdf", new MockFileData("new"));
            fileSystem.AddFile(@"D:\Archive\invoice.pdf", new MockFileData("old"));
            var ops = new FileSystemFileOperations(fileSystem, new ProtectedPathGuard());

            ops.Move(@"C:\Downloads\invoice.pdf", @"D:\Archive\invoice.pdf");

            fileSystem.FileExists(@"C:\Downloads\invoice.pdf").Should().BeFalse();
            fileSystem.File.ReadAllText(@"D:\Archive\invoice.pdf").Should().Be("new");
        }
    }
}
