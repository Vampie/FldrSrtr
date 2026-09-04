using System;
using App.Core.Execution;
using App.Core.Model;
using FluentAssertions;
using Xunit;

namespace App.Core.Tests
{
    public class VariableResolverTests
    {
        private static FileEntry MakeFile(string name = "invoice.pdf", long sizeBytes = 2048) => new FileEntry
        {
            FullPath = @"C:\Downloads\" + name,
            Directory = @"C:\Downloads",
            Name = name,
            Extension = System.IO.Path.GetExtension(name).TrimStart('.'),
            SizeBytes = sizeBytes
        };

        [Fact]
        public void Resolve_FileNameAndExtension_ReflectCurrentPath()
        {
            var file = MakeFile("invoice.pdf");
            string result = VariableResolver.Resolve("{FileName}.{Extension}", file, @"C:\Downloads\renamed.txt", DateTime.Now);

            result.Should().Be("renamed.txt");
        }

        [Fact]
        public void Resolve_OriginalNameAndExtension_ReflectScanTimeFile()
        {
            var file = MakeFile("invoice.pdf");
            string result = VariableResolver.Resolve("{OriginalName}.{OriginalExtension}", file, @"C:\Downloads\renamed.txt", DateTime.Now);

            result.Should().Be("invoice.pdf");
        }

        [Fact]
        public void Resolve_DateTokens_UseSuppliedMoment()
        {
            var file = MakeFile();
            var now = new DateTime(2026, 3, 5, 14, 7, 9);

            string result = VariableResolver.Resolve(@"D:\Archive\{Year}\{Month}\{Day}", file, file.FullPath, now);

            result.Should().Be(@"D:\Archive\2026\03\05");
        }

        [Fact]
        public void Resolve_FileSize_UsesOriginalFileSnapshot()
        {
            var file = MakeFile(sizeBytes: 4096);
            string result = VariableResolver.Resolve("size-{FileSize}", file, file.FullPath, DateTime.Now);

            result.Should().Be("size-4096");
        }

        [Fact]
        public void Resolve_NullOrEmptyTemplate_ReturnsUnchanged()
        {
            var file = MakeFile();
            VariableResolver.Resolve(null, file, file.FullPath, DateTime.Now).Should().BeNull();
            VariableResolver.Resolve(string.Empty, file, file.FullPath, DateTime.Now).Should().BeEmpty();
        }
    }
}
