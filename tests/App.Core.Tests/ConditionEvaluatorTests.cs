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

        [Fact]
        public void All_AlwaysMatches_RegardlessOfOperatorOrValue()
        {
            ConditionEvaluator.Evaluate(Leaf(ConditionField.All, ConditionOperator.Equals, null), MakeFile()).Should().BeTrue();
        }

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

        [Theory]
        [InlineData(ConditionOperator.Contains, "tm")]
        [InlineData(ConditionOperator.StartsWith, "ht")]
        [InlineData(ConditionOperator.EndsWith, "ml")]
        [InlineData(ConditionOperator.Wildcard, "h*l")]
        [InlineData(ConditionOperator.Regex, "^html$")]
        public void Extension_SharesFileNamesTextOperators(ConditionOperator op, string value)
        {
            var node = Leaf(ConditionField.Extension, op, value);

            ConditionEvaluator.Evaluate(node, MakeFile("page.html")).Should().BeTrue();
            ConditionEvaluator.Evaluate(node, MakeFile("page.pdf")).Should().BeFalse();
        }

        [Fact]
        public void Extension_IsOneOf_MatchesCaseInsensitive()
        {
            var node = Leaf(ConditionField.Extension, ConditionOperator.IsOneOf, "PDF, docx");

            ConditionEvaluator.Evaluate(node, MakeFile("report.pdf")).Should().BeTrue();
            ConditionEvaluator.Evaluate(node, MakeFile("report.txt")).Should().BeFalse();
        }

        [Theory]
        [InlineData("pdf,docx,txt")]
        [InlineData("pdf, docx, txt")]
        [InlineData("pdf docx txt")]
        [InlineData("pdf  docx   txt")]
        [InlineData(" pdf , docx  txt ")]
        public void Extension_IsOneOf_AcceptsCommaOrWhitespaceOrBothAsSeparator(string listValue)
        {
            var node = Leaf(ConditionField.Extension, ConditionOperator.IsOneOf, listValue);

            ConditionEvaluator.Evaluate(node, MakeFile("report.pdf")).Should().BeTrue();
            ConditionEvaluator.Evaluate(node, MakeFile("report.docx")).Should().BeTrue();
            ConditionEvaluator.Evaluate(node, MakeFile("report.txt")).Should().BeTrue();
            ConditionEvaluator.Evaluate(node, MakeFile("report.csv")).Should().BeFalse();
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

        [Theory]
        [InlineData(ConditionField.Extension, ConditionOperator.GreaterThan)]
        [InlineData(ConditionField.FileName, ConditionOperator.GreaterThan)]
        [InlineData(ConditionField.Age, ConditionOperator.Contains)]
        [InlineData(ConditionField.Size, ConditionOperator.StartsWith)]
        public void Evaluate_MismatchedFieldAndOperator_FailsSafeInsteadOfThrowing(ConditionField field, ConditionOperator op)
        {
            // Regression: a hand-edited or imported rule.json can pair any operator with any
            // field. This used to throw NotSupportedException from inside RuleEngine.GetMatches'
            // LINQ Where clause, crashing the whole app on one bad rule — exactly the kind of
            // single-file failure §4.2 says must never take down a run.
            var node = Leaf(field, op, "x");

            Action act = () => ConditionEvaluator.Evaluate(node, MakeFile());

            act.Should().NotThrow();
            ConditionEvaluator.Evaluate(node, MakeFile()).Should().BeFalse();
        }

        [Fact]
        public void ModifiedDate_Before_ComparesCalendarDate()
        {
            var file = MakeFile(daysOld: 40); // modified ~40 days ago
            string futureDate = DateTime.Now.AddDays(1).ToString("yyyy-MM-dd");
            string pastDate = DateTime.Now.AddDays(-1000).ToString("yyyy-MM-dd");

            ConditionEvaluator.Evaluate(Leaf(ConditionField.ModifiedDate, ConditionOperator.Before, futureDate), file).Should().BeTrue();
            ConditionEvaluator.Evaluate(Leaf(ConditionField.ModifiedDate, ConditionOperator.Before, pastDate), file).Should().BeFalse();
        }

        [Fact]
        public void ModifiedDate_After_ComparesCalendarDate()
        {
            var file = MakeFile(daysOld: 5);
            string pastDate = DateTime.Now.AddDays(-30).ToString("yyyy-MM-dd");

            ConditionEvaluator.Evaluate(Leaf(ConditionField.ModifiedDate, ConditionOperator.After, pastDate), file).Should().BeTrue();
        }

        [Fact]
        public void ModifiedDate_Between_MatchesInclusiveRange()
        {
            var file = MakeFile(daysOld: 10);
            string from = DateTime.Now.AddDays(-30).ToString("yyyy-MM-dd");
            string to = DateTime.Now.AddDays(1).ToString("yyyy-MM-dd");
            string outsideFrom = DateTime.Now.AddDays(-9).ToString("yyyy-MM-dd");
            string outsideTo = DateTime.Now.AddDays(-8).ToString("yyyy-MM-dd");

            ConditionEvaluator.Evaluate(Leaf(ConditionField.ModifiedDate, ConditionOperator.Between, $"{from},{to}"), file).Should().BeTrue();
            ConditionEvaluator.Evaluate(Leaf(ConditionField.ModifiedDate, ConditionOperator.Between, $"{outsideFrom},{outsideTo}"), file).Should().BeFalse();
        }

        [Fact]
        public void CreatedDate_And_AccessedDate_UseTheirOwnTimestamps()
        {
            var file = MakeFile(daysOld: 40);
            file.CreatedUtc = DateTime.UtcNow.AddDays(-5);
            file.AccessedUtc = DateTime.UtcNow.AddDays(-1);

            string recentCutoff = DateTime.Now.AddDays(-10).ToString("yyyy-MM-dd");

            ConditionEvaluator.Evaluate(Leaf(ConditionField.CreatedDate, ConditionOperator.After, recentCutoff), file).Should().BeTrue();
            ConditionEvaluator.Evaluate(Leaf(ConditionField.AccessedDate, ConditionOperator.After, recentCutoff), file).Should().BeTrue();
            ConditionEvaluator.Evaluate(Leaf(ConditionField.ModifiedDate, ConditionOperator.After, recentCutoff), file).Should().BeFalse();
        }

        [Fact]
        public void DateField_UnparsableValue_FailsSafeInsteadOfThrowing()
        {
            var node = Leaf(ConditionField.ModifiedDate, ConditionOperator.Before, "not-a-date");

            Action act = () => ConditionEvaluator.Evaluate(node, MakeFile());

            act.Should().NotThrow();
            ConditionEvaluator.Evaluate(node, MakeFile()).Should().BeFalse();
        }

        [Fact]
        public void Matches_ReportedCrashRule_ExtensionContainsIsNowASupportedCombination()
        {
            // The exact shape from the original user-reported crash: ANY(Age LessThan 20,
            // Extension Contains "htm"). Extension+Contains used to be unsupported and crashed
            // the app; Extension now shares FileName's full operator set, so this is a legitimate
            // match rather than a fail-safe false — and, either way, it must never throw.
            var rule = new Rule { RootCondition = ConditionNode.NewGroup(GroupLogic.Any) };
            rule.RootCondition.Children.Add(Leaf(ConditionField.Age, ConditionOperator.LessThan, "20"));
            rule.RootCondition.Children.Add(Leaf(ConditionField.Extension, ConditionOperator.Contains, "htm"));

            var oldFile = MakeFile("archive.html", daysOld: 40); // Age branch false -> falls through to the Extension branch

            Action act = () => ConditionEvaluator.Matches(rule, oldFile);

            act.Should().NotThrow();
            ConditionEvaluator.Matches(rule, oldFile).Should().BeTrue();
        }

        [Fact]
        public void Matches_WithEmptyRootGroup_NeverMatches()
        {
            var rule = new Rule();

            ConditionEvaluator.Matches(rule, MakeFile()).Should().BeFalse();
        }
    }
}
