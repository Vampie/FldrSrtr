namespace App.Core.Model
{
    public class Condition
    {
        public ConditionField Field { get; set; }
        public ConditionOperator Operator { get; set; }

        /// <summary>
        /// Raw value, interpreted per field: filename/extension = literal text (comma-separated
        /// for IsOneOf/IsNotOneOf), size = bytes, age = days.
        /// </summary>
        public string Value { get; set; }

        public bool CaseSensitive { get; set; } = false;
    }
}
