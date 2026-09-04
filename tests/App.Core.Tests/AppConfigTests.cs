using App.Core.Configuration;
using FluentAssertions;
using Xunit;

namespace App.Core.Tests
{
    public class AppConfigTests
    {
        [Fact]
        public void CreateDefault_SetsCurrentSchemaVersion()
        {
            AppConfig config = AppConfig.CreateDefault();

            config.SchemaVersion.Should().Be(SchemaVersions.Current);
        }
    }
}
