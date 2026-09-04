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

        [Theory]
        [InlineData("{filename}")]
        [InlineData("{FILENAME}")]
        [InlineData("{FileName}")]
        public void Resolve_TokensAreCaseInsensitive(string token)
        {
            var file = MakeFile("invoice.pdf");
            string result = VariableResolver.Resolve(token, file, file.FullPath, DateTime.Now);

            result.Should().Be("invoice");
        }

        [Fact]
        public void Resolve_CreatedAndModifiedDateTokens_UseTheFilesOwnTimestamps_NotNow()
        {
            var file = MakeFile();
            file.CreatedUtc = new DateTime(2020, 1, 2, 0, 0, 0, DateTimeKind.Utc);
            file.ModifiedUtc = new DateTime(2021, 6, 15, 0, 0, 0, DateTimeKind.Utc);
            var now = new DateTime(2026, 3, 5);

            string result = VariableResolver.Resolve(
                "{CreatedYear}-{CreatedMonth}-{CreatedDay} / {ModifiedYear}-{ModifiedMonth}-{ModifiedDay} / {Year}",
                file, file.FullPath, now);

            result.Should().Be($"{file.CreatedUtc.ToLocalTime():yyyy-MM-dd} / {file.ModifiedUtc.ToLocalTime():yyyy-MM-dd} / 2026");
        }

        [Fact]
        public void Resolve_UnknownToken_IsLeftUnchanged()
        {
            var file = MakeFile();
            string result = VariableResolver.Resolve("{NotARealToken}", file, file.FullPath, DateTime.Now);

            result.Should().Be("{NotARealToken}");
        }

        [Fact]
        public void Resolve_CounterToken_WithoutResolver_IsLeftUnchanged()
        {
            var file = MakeFile();
            string result = VariableResolver.Resolve("{Counter}", file, file.FullPath, DateTime.Now);

            result.Should().Be("{Counter}");
        }

        [Fact]
        public void Resolve_CounterToken_DelegatesToResolver()
        {
            var file = MakeFile();
            string result = VariableResolver.Resolve("file_{Counter}", file, file.FullPath, DateTime.Now, spec => 42);

            result.Should().Be("file_42");
        }

        [Theory]
        [InlineData("Counter", 1, 1)]
        [InlineData("Counter:100", 100, 1)]
        [InlineData("Counter:100:5", 100, 5)]
        [InlineData("Counter:-3:2", -3, 2)]
        public void ParseCounterSpec_ParsesStartAndStep(string spec, int expectedStart, int expectedStep)
        {
            VariableResolver.ParseCounterSpec(spec, out int start, out int step);

            start.Should().Be(expectedStart);
            step.Should().Be(expectedStep);
        }

        [Fact]
        public void Resolve_GuidToken_ProducesDistinctValuesEachTime()
        {
            var file = MakeFile();

            string first = VariableResolver.Resolve("{Guid}", file, file.FullPath, DateTime.Now);
            string second = VariableResolver.Resolve("{Guid}", file, file.FullPath, DateTime.Now);

            first.Should().NotBe(second);
            first.Should().MatchRegex("^[0-9a-f]{32}$");
        }

        [Fact]
        public void Resolve_UnixTimestamp_IsCloseToNow()
        {
            var file = MakeFile();
            long expected = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

            string result = VariableResolver.Resolve("{UnixTimestamp}", file, file.FullPath, DateTime.Now);

            long.Parse(result).Should().BeInRange(expected - 5, expected + 5);
        }

        [Fact]
        public void Resolve_UnixTimestampMicro_IsFinerGrainedThanSeconds()
        {
            var file = MakeFile();

            string first = VariableResolver.Resolve("{UnixTimestampMicro}", file, file.FullPath, DateTime.Now);
            string second = VariableResolver.Resolve("{UnixTimestampMicro}", file, file.FullPath, DateTime.Now);

            long.Parse(first).Should().BeGreaterThan(1_600_000_000_000_000); // sanity: looks like microseconds since epoch, not seconds
            (long.Parse(second) - long.Parse(first)).Should().BeGreaterThanOrEqualTo(0);
        }

        [Fact]
        public void Resolve_RandomDefault_Produces6ZeroPaddedDigits()
        {
            var file = MakeFile();

            string result = VariableResolver.Resolve("{Random}", file, file.FullPath, DateTime.Now);

            result.Should().MatchRegex("^[0-9]{6}$");
        }

        [Theory]
        [InlineData("{Random:0}", "^[0-9]$")]
        [InlineData("{Random:0000}", "^[0-9]{4}$")]
        public void Resolve_RandomWithPattern_RespectsDigitCountAndPadding(string template, string expectedRegex)
        {
            var file = MakeFile();

            string result = VariableResolver.Resolve(template, file, file.FullPath, DateTime.Now);

            result.Should().MatchRegex(expectedRegex);
        }

        [Fact]
        public void Resolve_RandomWithHashPattern_DoesNotZeroPad()
        {
            var file = MakeFile();
            bool sawUnpadded = false;

            for (int i = 0; i < 200 && !sawUnpadded; i++)
            {
                string result = VariableResolver.Resolve("{Random:###0}", file, file.FullPath, DateTime.Now);
                if (result.Length < 4)
                {
                    sawUnpadded = true;
                }
            }

            sawUnpadded.Should().BeTrue("a '#'-based pattern should not force leading zeros across many samples");
        }

        [Fact]
        public void Resolve_RandomStringDefault_Produces8CharactersAlphanumeric()
        {
            var file = MakeFile();

            string result = VariableResolver.Resolve("{RandomString}", file, file.FullPath, DateTime.Now);

            result.Should().MatchRegex("^[A-Za-z0-9]{8}$");
        }

        [Fact]
        public void Resolve_RandomStringWithLength_UsesGivenLength()
        {
            var file = MakeFile();

            string result = VariableResolver.Resolve("{RandomString:16}", file, file.FullPath, DateTime.Now);

            result.Should().MatchRegex("^[A-Za-z0-9]{16}$");
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
