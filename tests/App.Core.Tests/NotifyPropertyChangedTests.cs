using System.Collections.Generic;
using System.ComponentModel;
using App.Core.Model;
using FluentAssertions;
using Xunit;

namespace App.Core.Tests
{
    /// <summary>
    /// Reported bug: editing a Condition or Action's details in the rule editor never updated the
    /// tree/list label until the editor was closed and reopened. Root cause: the label bindings
    /// target the whole node/action object (no property Path, so the formatted label can combine
    /// several fields) — a WPF binding like that only refreshes on a PropertyChanged whose name is
    /// null/"", never on a named property. These tests pin the fix at the model level (raising
    /// that empty-name notification alongside the named one) without needing a WPF binding.
    /// </summary>
    public class NotifyPropertyChangedTests
    {
        [Fact]
        public void ConditionNode_ChangingAnyProperty_AlsoRaisesAnEmptyNameNotification()
        {
            var node = ConditionNode.NewLeaf(ConditionField.FileName, ConditionOperator.Equals, "a.txt");
            var raisedNames = new List<string>();
            ((INotifyPropertyChanged)node).PropertyChanged += (s, e) => raisedNames.Add(e.PropertyName);

            node.Value = "b.txt";

            raisedNames.Should().Contain(nameof(ConditionNode.Value));
            raisedNames.Should().Contain(string.Empty);
        }

        [Fact]
        public void RuleAction_ChangingAnyProperty_AlsoRaisesAnEmptyNameNotification()
        {
            var action = new RuleAction { Type = ActionType.Move, Destination = @"D:\Archive" };
            var raisedNames = new List<string>();
            action.PropertyChanged += (s, e) => raisedNames.Add(e.PropertyName);

            action.Destination = @"D:\Other";

            raisedNames.Should().Contain(nameof(RuleAction.Destination));
            raisedNames.Should().Contain(string.Empty);
        }
    }
}
