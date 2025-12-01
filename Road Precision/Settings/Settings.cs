using Colossal;
using Colossal.IO.AssetDatabase;
using Game.Modding;
using Game.Settings;
using Game.UI;
using System.Collections.Generic;

namespace Road_Precision
{
    [FileLocation(nameof(Road_Precision))]
    [SettingsUIGroupOrder(kDisplayGroup)]
    [SettingsUIShowGroupName(kDisplayGroup)]
    public class Setting : ModSetting
    {
        public const string kSection = "Main";
        public const string kDisplayGroup = "Display";

        public Setting(IMod mod) : base(mod)
        {
        }

        [SettingsUISection(kSection, kDisplayGroup)]
        [SettingsUISlider(min = 0, max = 4, step = 1, scalarMultiplier = 1, unit = Unit.kInteger)]
        public int DistanceDecimalPlaces { get; set; }

        [SettingsUISection(kSection, kDisplayGroup)]
        [SettingsUISlider(min = 0, max = 4, step = 1, scalarMultiplier = 1, unit = Unit.kInteger)]
        public int AngleDecimalPlaces { get; set; }

        [SettingsUISection(kSection, kDisplayGroup)]
        public bool EnableFloatDistance { get; set; }

        [SettingsUISection(kSection, kDisplayGroup)]
        public bool EnableFloatAngle { get; set; }

        public override void SetDefaults()
        {
            DistanceDecimalPlaces = 2;
            AngleDecimalPlaces = 2;
            EnableFloatDistance = true;
            EnableFloatAngle = true;
        }
    }
}
