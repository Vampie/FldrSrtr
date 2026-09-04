using App.Infrastructure.Safety;
using FluentAssertions;
using Xunit;

namespace App.Infrastructure.Tests
{
    public class ProtectedPathGuardTests
    {
        [Theory]
        [InlineData(@"C:\Windows\System32\notepad.exe")]
        [InlineData(@"C:\Program Files\SomeApp\app.exe")]
        [InlineData(@"C:\Program Files (x86)\SomeApp\app.exe")]
        [InlineData(@"C:\ProgramData\SomeApp\data.dat")]
        public void IsProtected_ReturnsTrue_ForHardcodedRoots(string path)
        {
            new ProtectedPathGuard().IsProtected(path).Should().BeTrue();
        }

        [Fact]
        public void IsProtected_ReturnsFalse_ForOrdinaryPath()
        {
            new ProtectedPathGuard().IsProtected(@"C:\Users\Someone\Downloads\file.pdf").Should().BeFalse();
        }

        [Fact]
        public void IsProtected_HonorsExtraConfiguredRoots()
        {
            var guard = new ProtectedPathGuard(new[] { @"D:\Important" });

            guard.IsProtected(@"D:\Important\file.txt").Should().BeTrue();
            guard.IsProtected(@"D:\Other\file.txt").Should().BeFalse();
        }
    }
}
