using System.Reflection;
using FluentAssertions;
using NetArchTest.Rules;
using Xunit;

namespace App.ArchTests
{
    /// <summary>
    /// Enforces the hard architecture rule from the project brief: Core and Infrastructure
    /// must never depend on UI. This runs at test time on every build, not as a code-review
    /// convention, so a stray "using App.UI" fails CI immediately instead of rotting in.
    /// </summary>
    public class LayeringTests
    {
        [Fact]
        public void Core_ShouldNotDependOn_UI()
        {
            Assembly coreAssembly = typeof(App.Core.Configuration.AppConfig).Assembly;

            TestResult result = Types.InAssembly(coreAssembly)
                .Should()
                .NotHaveDependencyOn("App.UI")
                .GetResult();

            result.IsSuccessful.Should().BeTrue(FailureMessage(result));
        }

        [Fact]
        public void Infrastructure_ShouldNotDependOn_UI()
        {
            Assembly infrastructureAssembly = typeof(App.Infrastructure.Configuration.ConfigService).Assembly;

            TestResult result = Types.InAssembly(infrastructureAssembly)
                .Should()
                .NotHaveDependencyOn("App.UI")
                .GetResult();

            result.IsSuccessful.Should().BeTrue(FailureMessage(result));
        }

        private static string FailureMessage(TestResult result)
        {
            if (result.FailingTypes == null)
            {
                return string.Empty;
            }

            return "Violating types: " + string.Join(", ", result.FailingTypeNames);
        }
    }
}
