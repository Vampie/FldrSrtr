using System;
using App.Core.Evaluation;
using App.Core.Model;
using FluentAssertions;
using Xunit;

namespace App.Core.Tests
{
    public class ConditionEvaluatorTests
    {
        private static FileEntry MakeFile(
            string name = "invoice.pdf",
            long sizeBytes = 1024,
            int daysOld = 40)
        {
            return new FileEntry
            {
                FullPath = @"C:\Downloads\" + name,
                Directory = @"C:\Downloads",
                Name = name,
                Extension = System.IO.Path.GetExtension(name).TrimStart('.'),
                SizeBytes = sizeBytes,
                CreatedUtc = DateTime.UtcNow.AddDays(-daysOld),
                ModifiedUtc = DateTime.UtcNow.AddDays(-daysOld),
                AccessedUtc = DateTime.UtcNow.AddDays(-daysOld)
            };
        }

        private static ConditionNode Leaf(ConditionField field, ConditionOperator op, string value, bool caseSensitive = false) =>
            new ConditionNode { NodeType = ConditionNodeType.Leaf, Field = field, Operator = op, Value = value, CaseSensitive = caseSensitive };

        [Theory]
        [InlineData(ConditionOperator.Equals, "invoice.pdf", true)]
        [InlineData(ConditionOperator.Contains, "voice", true)]
        [InlineData(ConditionOperator.StartsWith, "invoice", true)]
        [InlineData(ConditionOperator.EndsWith, ".pdf", true)]
        [InlineData(ConditionOperator.Equals, "other.pdf", false)]
        public void FileName_Operators(ConditionOperator op, string value, bool expected)
        {
            ConditionEvaluator.Evaluate(Leaf(ConditionField.FileName, op, value), MakeFile()).Should().Be(expected);
        }

        [Theory]
        [InlineData("invoice*.pdf", true)]
        [InlineData("*.txt", false)]
        public void FileName_Wildcard(string pattern, bool expected)
        {
            ConditionEvaluator.Evaluate(Leaf(ConditionField.FileName, ConditionOperator.Wildcard, pattern), MakeFile()).Should().Be(expected);
        }

        [Theory]
        [InlineData(@"^invoice\.pdf$", true)]
        [InlineData(@"^\d+$", false)]
        public void FileName_Regex(string pattern, bool expected)
        {
            ConditionEvaluator.Evaluate(Leaf(ConditionField.FileName, ConditionOperator.Regex, pattern), MakeFile()).Should().Be(expected);
        }

        [Fact]
        public void FileName_Regex_CatastrophicPattern_TimesOutInsteadOfHanging()
        {
            var node = Leaf(ConditionField.FileName, ConditionOperator.Regex, "^(a+)+$");
            var file = MakeFile(new string('a', 40) + "!");

            Action act = () => ConditionEvaluator.Evaluate(node, file);

            act.Should().NotThrow();
        }

        [Fact]
        public void Extension_IsOneOf_MatchesCaseInsensitive()
        {
            var node = Leaf(ConditionField.Extension, ConditionOperator.IsOneOf, "PDF, docx");

            ConditionEvaluator.Evaluate(node, MakeFile("report.pdf")).Should().BeTrue();
            ConditionEvaluator.Evaluate(node, MakeFile("report.txt")).Should().BeFalse();
        }

        [Fact]
        public void Size_GreaterThan_ComparesBytes()
        {
            var node = Leaf(ConditionField.Size, ConditionOperator.GreaterThan, "1000");

            ConditionEvaluator.Evaluate(node, MakeFile(sizeBytes: 2000)).Should().BeTrue();
            ConditionEvaluator.Evaluate(node, MakeFile(sizeBytes: 500)).Should().BeFalse();
        }

        [Fact]
        public void Age_GreaterThan_ComparesDaysSinceModified()
        {
            var node = Leaf(ConditionField.Age, ConditionOperator.GreaterThan, "30");

            ConditionEvaluator.Evaluate(node, MakeFile(daysOld: 40)).Should().BeTrue();
            ConditionEvaluator.Evaluate(node, MakeFile(daysOld: 5)).Should().BeFalse();
        }

        [Fact]
        public void Matches_AllGroup_RequiresEveryChild()
        {
            var rule = new Rule { RootCondition = ConditionNode.NewGroup(GroupLogic.All) };
            rule.RootCondition.Children.Add(Leaf(ConditionField.Extension, ConditionOperator.Equals, "pdf"));
            rule.RootCondition.Children.Add(Leaf(ConditionField.Age, ConditionOperator.GreaterThan, "30"));

            ConditionEvaluator.Matches(rule, MakeFile("invoice.pdf", daysOld: 40)).Should().BeTrue();
            ConditionEvaluator.Matches(rule, MakeFile("invoice.pdf", daysOld: 5)).Should().BeFalse();
        }

        [Fact]
        public void Matches_AnyGroup_RequiresAtLeastOneChild()
        {
            var rule = new Rule { RootCondition = ConditionNode.NewGroup(GroupLogic.Any) };
            rule.RootCondition.Children.Add(Leaf(ConditionField.FileName, ConditionOperator.Contains, "invoice"));
            rule.RootCondition.Children.Add(Leaf(ConditionField.FileName, ConditionOperator.Contains, "factuur"));

            ConditionEvaluator.Matches(rule, MakeFile("factuur.pdf")).Should().BeTrue();
            ConditionEvaluator.Matches(rule, MakeFile("random.pdf")).Should().BeFalse();
        }

        [Fact]
        public void Matches_NotGroup_NegatesChild()
        {
            var rule = new Rule { RootCondition = ConditionNode.NewGroup(GroupLogic.Not) };
            rule.RootCondition.Children.Add(Leaf(ConditionField.Extension, ConditionOperator.Equals, "tmp"));

            ConditionEvaluator.Matches(rule, MakeFile("invoice.pdf")).Should().BeTrue();
            ConditionEvaluator.Matches(rule, MakeFile("cache.tmp")).Should().BeFalse();
        }

        [Fact]
        public void Matches_NestedGroups_EvaluateRecursively()
        {
            // ALL( Extension=pdf, ANY( contains "invoice", contains "factuur" ) )
            var rule = new Rule { RootCondition = ConditionNode.NewGroup(GroupLogic.All) };
            rule.RootCondition.Children.Add(Leaf(ConditionField.Extension, ConditionOperator.Equals, "pdf"));

            var nestedAny = ConditionNode.NewGroup(GroupLogic.Any);
            nestedAny.Children.Add(Leaf(ConditionField.FileName, ConditionOperator.Contains, "invoice"));
            nestedAny.Children.Add(Leaf(ConditionField.FileName, ConditionOperator.Contains, "factuur"));
            rule.RootCondition.Children.Add(nestedAny);

            ConditionEvaluator.Matches(rule, MakeFile("factuur.pdf")).Should().BeTrue();
            ConditionEvaluator.Matches(rule, MakeFile("factuur.txt")).Should().BeFalse();
            ConditionEvaluator.Matches(rule, MakeFile("random.pdf")).Should().BeFalse();
        }

        [Fact]
        public void Duplicate_EqualsTrue_MatchesFlaggedFile()
        {
            var node = Leaf(ConditionField.Duplicate, ConditionOperator.Equals, "true");
            var file = MakeFile();
            file.IsDuplicate = true;

            ConditionEvaluator.Evaluate(node, file).Should().BeTrue();
        }

        [Fact]
        public void Duplicate_EqualsFalse_MatchesUnflaggedFile()
        {
            var node = Leaf(ConditionField.Duplicate, ConditionOperator.Equals, "false");
            var file = MakeFile();
            file.IsDuplicate = false;

            ConditionEvaluator.Evaluate(node, file).Should().BeTrue();
        }

        [Fact]
        public void Matches_WithEmptyRootGroup_NeverMatches()
        {
            var rule = new Rule();

            ConditionEvaluator.Matches(rule, MakeFile()).Should().BeFalse();
        }
    }
}
