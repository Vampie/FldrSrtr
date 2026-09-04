using System;
using App.Core.Evaluation;
using App.Core.Model;
using FluentAssertions;
using Xunit;

namespace App.Core.Tests
{
    public class ConditionOperatorCatalogTests
    {
        [Fact]
        public void Extension_MatchesFileNamesFullTextOperatorSetPlusIsOneOf()
        {
            var ops = ConditionOperatorCatalog.SupportedOperators(ConditionField.Extension);

            ops.Should().Contain(ConditionOperator.Contains);
            ops.Should().Contain(ConditionOperator.Wildcard);
            ops.Should().Contain(ConditionOperator.Regex);
            ops.Should().Contain(ConditionOperator.IsOneOf);
            ops.Should().Contain(ConditionOperator.IsNotOneOf);
        }

        [Fact]
        public void FileName_IncludesWildcardAndRegex()
        {
            var ops = ConditionOperatorCatalog.SupportedOperators(ConditionField.FileName);

            ops.Should().Contain(ConditionOperator.Wildcard);
            ops.Should().Contain(ConditionOperator.Regex);
        }

        [Theory]
        [InlineData(ConditionField.CreatedDate)]
        [InlineData(ConditionField.ModifiedDate)]
        [InlineData(ConditionField.AccessedDate)]
        public void DateFields_SupportBeforeAfterBetween(ConditionField field)
        {
            var ops = ConditionOperatorCatalog.SupportedOperators(field);

            ops.Should().Contain(ConditionOperator.Before);
            ops.Should().Contain(ConditionOperator.After);
            ops.Should().Contain(ConditionOperator.Between);
        }

        [Fact]
        public void EveryCatalogedFieldOperatorPair_NeverThrows()
        {
            // Cross-check against ConditionEvaluator itself: for every (field, operator) pair the
            // catalog claims is valid, evaluating it must never throw — that's the regression
            // this whole exercise is about, whatever the match result ends up being.
            var file = new FileEntry
            {
                Name = "invoice.pdf",
                Extension = "pdf",
                SizeBytes = 100,
                CreatedUtc = DateTime.UtcNow,
                ModifiedUtc = DateTime.UtcNow,
                AccessedUtc = DateTime.UtcNow
            };

            foreach (ConditionField field in (ConditionField[])Enum.GetValues(typeof(ConditionField)))
            {
                foreach (ConditionOperator op in ConditionOperatorCatalog.SupportedOperators(field))
                {
                    var node = new ConditionNode { Field = field, Operator = op, Value = "2026-01-01,2026-06-01" };
                    Action act = () => ConditionEvaluator.Evaluate(node, file);
                    act.Should().NotThrow();
                }
            }
        }
    }
}
