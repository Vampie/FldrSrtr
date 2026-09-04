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

        [Theory]
        [InlineData(ConditionOperator.Equals, "invoice.pdf", true)]
        [InlineData(ConditionOperator.Contains, "voice", true)]
        [InlineData(ConditionOperator.StartsWith, "invoice", true)]
        [InlineData(ConditionOperator.EndsWith, ".pdf", true)]
        [InlineData(ConditionOperator.Equals, "other.pdf", false)]
        public void FileName_Operators(ConditionOperator op, string value, bool expected)
        {
            var condition = new Condition { Field = ConditionField.FileName, Operator = op, Value = value };

            ConditionEvaluator.Evaluate(condition, MakeFile()).Should().Be(expected);
        }

        [Fact]
        public void Extension_IsOneOf_MatchesCaseInsensitive()
        {
            var condition = new Condition
            {
                Field = ConditionField.Extension,
                Operator = ConditionOperator.IsOneOf,
                Value = "PDF, docx"
            };

            ConditionEvaluator.Evaluate(condition, MakeFile("report.pdf")).Should().BeTrue();
            ConditionEvaluator.Evaluate(condition, MakeFile("report.txt")).Should().BeFalse();
        }

        [Fact]
        public void Size_GreaterThan_ComparesBytes()
        {
            var condition = new Condition { Field = ConditionField.Size, Operator = ConditionOperator.GreaterThan, Value = "1000" };

            ConditionEvaluator.Evaluate(condition, MakeFile(sizeBytes: 2000)).Should().BeTrue();
            ConditionEvaluator.Evaluate(condition, MakeFile(sizeBytes: 500)).Should().BeFalse();
        }

        [Fact]
        public void Age_GreaterThan_ComparesDaysSinceModified()
        {
            var condition = new Condition { Field = ConditionField.Age, Operator = ConditionOperator.GreaterThan, Value = "30" };

            ConditionEvaluator.Evaluate(condition, MakeFile(daysOld: 40)).Should().BeTrue();
            ConditionEvaluator.Evaluate(condition, MakeFile(daysOld: 5)).Should().BeFalse();
        }

        [Fact]
        public void Matches_WithAllLogic_RequiresEveryCondition()
        {
            var rule = new Rule
            {
                Logic = ConditionLogic.All,
                Conditions =
                {
                    new Condition { Field = ConditionField.Extension, Operator = ConditionOperator.Equals, Value = "pdf" },
                    new Condition { Field = ConditionField.Age, Operator = ConditionOperator.GreaterThan, Value = "30" }
                }
            };

            ConditionEvaluator.Matches(rule, MakeFile("invoice.pdf", daysOld: 40)).Should().BeTrue();
            ConditionEvaluator.Matches(rule, MakeFile("invoice.pdf", daysOld: 5)).Should().BeFalse();
        }

        [Fact]
        public void Matches_WithAnyLogic_RequiresAtLeastOneCondition()
        {
            var rule = new Rule
            {
                Logic = ConditionLogic.Any,
                Conditions =
                {
                    new Condition { Field = ConditionField.FileName, Operator = ConditionOperator.Contains, Value = "invoice" },
                    new Condition { Field = ConditionField.FileName, Operator = ConditionOperator.Contains, Value = "factuur" }
                }
            };

            ConditionEvaluator.Matches(rule, MakeFile("factuur.pdf")).Should().BeTrue();
            ConditionEvaluator.Matches(rule, MakeFile("random.pdf")).Should().BeFalse();
        }

        [Fact]
        public void Matches_WithNoConditions_NeverMatches()
        {
            var rule = new Rule();

            ConditionEvaluator.Matches(rule, MakeFile()).Should().BeFalse();
        }
    }
}
