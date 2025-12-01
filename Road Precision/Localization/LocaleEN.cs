using Colossal;
using System.Collections.Generic;

namespace Road_Precision.Localization
{
    public class LocaleEN : IDictionarySource
    {
        private readonly Setting m_Setting;

        public LocaleEN(Setting setting)
        {
            m_Setting = setting;
        }

        public IEnumerable<KeyValuePair<string, string>> ReadEntries(
            IList<IDictionaryEntryError> errors, 
            Dictionary<string, int> indexCounts)
        {
            return new Dictionary<string, string>
            {
            { m_Setting.GetSettingsLocaleID(), "Road Precision" },
                { m_Setting.GetOptionTabLocaleID(Setting.kSection), "Main Settings" },
                
                { m_Setting.GetOptionGroupLocaleID(Setting.kDisplayGroup), "Display Options" },
                
                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.DistanceDecimalPlaces)), 
                  "Distance Decimal Places" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.DistanceDecimalPlaces)), 
                  "Number of decimal places to show for distance (0-4)" },
                
                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.AngleDecimalPlaces)), 
                  "Angle Decimal Places" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.AngleDecimalPlaces)), 
                  "Number of decimal places to show for angles (0-4)" },
                
                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.EnableFloatDistance)), 
                  "Enable Float Distance" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.EnableFloatDistance)), 
                  "Show distance as floating point instead of integer" },
                
                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.EnableFloatAngle)), 
                  "Enable Float Angle" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.EnableFloatAngle)), 
                  "Show angle as floating point instead of integer" }
            };
        }

        public void Unload()
        {
        }
    }
}
