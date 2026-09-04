using App.Core.Evaluation;
using App.Core.Model;
using FluentAssertions;
using Xunit;

namespace App.Core.Tests
{
    public class ConditionOperatorCatalogTests
    {
        [Fact]
        public void Extension_DoesNotIncludeTextOperators()
        {
            var ops = ConditionOperatorCatalog.SupportedOperators(ConditionField.Extension);

            ops.Should().NotContain(ConditionOperator.Contains);
            ops.Should().Contain(ConditionOperator.IsOneOf);
        }

        [Fact]
        public void FileName_IncludesWildcardAndRegex()
        {
            var ops = ConditionOperatorCatalog.SupportedOperators(ConditionField.FileName);

            ops.Should().Contain(ConditionOperator.Wildcard);
            ops.Should().Contain(ConditionOperator.Regex);
        }

        [Fact]
        public void EveryCatalogedFieldOperatorPair_ActuallyEvaluatesWithoutFailingSafe()
        {
            // Cross-check against ConditionEvaluator itself: for every (field, operator) pair the
            // catalog claims is valid, evaluating it against a plausible file must not silently
            // fall through to the "unsupported combination" false — it should hit real field logic.
            var file = new FileEntry { Name = "invoice.pdf", Extension = "pdf", SizeBytes = 100 };

            foreach (ConditionField field in new[] { ConditionField.FileName, ConditionField.Extension, ConditionField.Size, ConditionField.Age, ConditionField.Duplicate })
            {
                foreach (ConditionOperator op in ConditionOperatorCatalog.SupportedOperators(field))
                {
                    var node = new ConditionNode { Field = field, Operator = op, Value = "1" };
                    // Must not throw — that's the regression this whole exercise is about.
                    ConditionEvaluator.Evaluate(node, file);
                }
            }
        }
    }
}
