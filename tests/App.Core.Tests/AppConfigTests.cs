using App.Core.Configuration;
using FluentAssertions;
using Xunit;

namespace App.Core.Tests
{
    public class AppConfigTests
    {
        [Fact]
        public void CreateDefault_SetsSchemaVersion1()
        {
            AppConfig config = AppConfig.CreateDefault();

            config.SchemaVersion.Should().Be(1);
        }
    }
}
