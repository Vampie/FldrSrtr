using System;
using System.Collections.ObjectModel;

namespace App.Core.Model
{
    public class Rule
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");
        public string Name { get; set; } = "New rule";
        public bool Enabled { get; set; } = true;
        public int Priority { get; set; }
        public ConditionLogic Logic { get; set; } = ConditionLogic.All;
        public ObservableCollection<Condition> Conditions { get; set; } = new ObservableCollection<Condition>();
        public ObservableCollection<RuleAction> Actions { get; set; } = new ObservableCollection<RuleAction>();
    }
}
