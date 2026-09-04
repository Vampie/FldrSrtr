using App.Core.Evaluation;
using App.Core.Model;
using FluentAssertions;
using Xunit;

namespace App.Core.Tests
{
    public class RuleValidatorTests
    {
        private static Rule ValidRule()
        {
            var rule = new Rule { Name = "Archive old PDFs", RootCondition = ConditionNode.NewGroup() };
            rule.RootCondition.Children.Add(ConditionNode.NewLeaf(ConditionField.Extension, ConditionOperator.Equals, "pdf"));
            rule.Actions.Add(new RuleAction { Type = ActionType.Move, Destination = @"D:\Archive" });
            return rule;
        }

        [Fact]
        public void ValidRule_HasNoIssues()
        {
            RuleValidator.Validate(ValidRule()).Should().BeEmpty();
        }

        [Fact]
        public void MissingName_IsFlagged()
        {
            var rule = ValidRule();
            rule.Name = "  ";

            RuleValidator.Validate(rule).Should().Contain(i => i.Contains("naam"));
        }

        [Fact]
        public void EmptyRootGroup_IsFlagged()
        {
            var rule = ValidRule();
            rule.RootCondition.Children.Clear();

            RuleValidator.Validate(rule).Should().Contain(i => i.Contains("groep"));
        }

        [Fact]
        public void MismatchedFieldAndOperator_IsFlagged()
        {
            var rule = ValidRule();
            rule.RootCondition.Children[0].Operator = ConditionOperator.GreaterThan; // invalid for Extension after... wait Extension now supports text ops but not GreaterThan
            rule.RootCondition.Children[0].Field = ConditionField.Duplicate;

            RuleValidator.Validate(rule).Should().Contain(i => i.Contains("niet geldig"));
        }

        [Fact]
        public void AllField_NeedsNoValue_IsNotFlagged()
        {
            var rule = ValidRule();
            rule.RootCondition.Children[0] = ConditionNode.NewLeaf(ConditionField.All, ConditionOperator.Equals, null);

            RuleValidator.Validate(rule).Should().BeEmpty();
        }

        [Fact]
        public void MissingConditionValue_IsFlagged()
        {
            var rule = ValidRule();
            rule.RootCondition.Children[0].Value = "";

            RuleValidator.Validate(rule).Should().Contain(i => i.Contains("waarde"));
        }

        [Fact]
        public void InvalidRegex_IsFlagged()
        {
            var rule = ValidRule();
            rule.RootCondition.Children[0].Operator = ConditionOperator.Regex;
            rule.RootCondition.Children[0].Value = "["; // unterminated character class

            RuleValidator.Validate(rule).Should().Contain(i => i.Contains("reguliere expressie"));
        }

        [Theory]
        [InlineData("not-a-number")]
        [InlineData("")]
        public void InvalidSizeValue_IsFlagged(string value)
        {
            var rule = ValidRule();
            rule.RootCondition.Children[0].Field = ConditionField.Size;
            rule.RootCondition.Children[0].Operator = ConditionOperator.GreaterThan;
            rule.RootCondition.Children[0].Value = value;

            RuleValidator.Validate(rule).Should().NotBeEmpty();
        }

        [Fact]
        public void DateField_InvalidSingleDate_IsFlagged()
        {
            var rule = ValidRule();
            rule.RootCondition.Children[0].Field = ConditionField.ModifiedDate;
            rule.RootCondition.Children[0].Operator = ConditionOperator.Before;
            rule.RootCondition.Children[0].Value = "not-a-date";

            RuleValidator.Validate(rule).Should().Contain(i => i.Contains("datum"));
        }

        [Fact]
        public void DateField_Between_RequiresTwoValidDates()
        {
            var rule = ValidRule();
            rule.RootCondition.Children[0].Field = ConditionField.ModifiedDate;
            rule.RootCondition.Children[0].Operator = ConditionOperator.Between;
            rule.RootCondition.Children[0].Value = "2026-01-01"; // only one date

            RuleValidator.Validate(rule).Should().Contain(i => i.Contains("Between"));
        }

        [Fact]
        public void DateField_ValidBetween_HasNoIssues()
        {
            var rule = ValidRule();
            rule.RootCondition.Children[0].Field = ConditionField.ModifiedDate;
            rule.RootCondition.Children[0].Operator = ConditionOperator.Between;
            rule.RootCondition.Children[0].Value = "2026-01-01,2026-03-01";

            RuleValidator.Validate(rule).Should().BeEmpty();
        }

        [Fact]
        public void NoActions_IsFlagged()
        {
            var rule = ValidRule();
            rule.Actions.Clear();

            RuleValidator.Validate(rule).Should().Contain(i => i.Contains("acties"));
        }

        [Theory]
        [InlineData(ActionType.Move)]
        [InlineData(ActionType.Copy)]
        [InlineData(ActionType.Zip)]
        [InlineData(ActionType.CreateFolder)]
        [InlineData(ActionType.OpenWith)]
        [InlineData(ActionType.ExecuteExternal)]
        [InlineData(ActionType.DeleteTargetIfExists)]
        public void ActionMissingDestination_IsFlagged(ActionType type)
        {
            var rule = ValidRule();
            rule.Actions[0] = new RuleAction { Type = type, Destination = "" };

            RuleValidator.Validate(rule).Should().Contain(i => i.Contains("Destination"));
        }

        [Theory]
        [InlineData(ActionType.Open)]
        [InlineData(ActionType.RemoveExtension)]
        [InlineData(ActionType.DeleteToRecycleBin)]
        public void ActionsNotNeedingDestination_AreNotFlagged(ActionType type)
        {
            var rule = ValidRule();
            rule.Actions[0] = new RuleAction { Type = type };

            RuleValidator.Validate(rule).Should().BeEmpty();
        }
    }
}
