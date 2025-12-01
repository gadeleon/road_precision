using Colossal.UI.Binding;
using Game.UI.Tooltip;

namespace Road_Precision.UI
{
    /// <summary>
    /// Custom tooltip that displays float values with configurable decimal places
    /// instead of rounding to integers.
    /// Uses StringTooltip approach to bypass UI formatting.
    /// </summary>
    public class PrecisionFloatTooltip : StringTooltip
    {
        private float m_NumericValue;
        private string m_Unit = "length";
        private int m_DecimalPlaces = 2;

        public new float value
        {
            get => m_NumericValue;
            set
            {
                if (value != m_NumericValue)
                {
                    m_NumericValue = value;
                    UpdateLabel();
                }
            }
        }

        public string unit
        {
            get => m_Unit;
            set
            {
                if (value != m_Unit)
                {
                    m_Unit = value;
                    UpdateLabel();
                }
            }
        }

        public int decimalPlaces
        {
            get => m_DecimalPlaces;
            set
            {
                if (value != m_DecimalPlaces)
                {
                    m_DecimalPlaces = value;
                    UpdateLabel();
                }
            }
        }

        private void UpdateLabel()
        {
            // Format the value with decimals and append unit suffix
            string formattedValue = m_NumericValue.ToString($"F{m_DecimalPlaces}");

            // Add appropriate unit suffix
            string unitSuffix = m_Unit switch
            {
                "length" => "m",
                "percentageSingleFraction" => "%",
                _ => ""
            };

            // Set the base StringTooltip.value property
            base.value = $"{formattedValue}{unitSuffix}";
        }
    }
}