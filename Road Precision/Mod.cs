using Colossal.IO.AssetDatabase;
using Colossal.Logging;
using Game;
using Game.Modding;
using Game.SceneFlow;
using Road_Precision.Localization;
using Road_Precision.Systems;

namespace Road_Precision
{
    public class Mod : IMod
    {
        public static ILog log = LogManager.GetLogger($"{nameof(Road_Precision)}.{nameof(Mod)}")
            .SetShowsErrorsInUI(false);

        public static Mod Instance { get; private set; }
        public Setting Setting { get; private set; }

        public void OnLoad(UpdateSystem updateSystem)
        {
            log.Info(nameof(OnLoad));

            Instance = this;

            // Load mod settings
            Setting = new Setting(this);
            Setting.RegisterInOptionsUI();
            GameManager.instance.localizationManager.AddSource("en-US", new LocaleEN(Setting));
            AssetDatabase.global.LoadSettings(nameof(Road_Precision), Setting, new Setting(this));

            // Register precision tooltip systems that run alongside vanilla tooltips
            updateSystem.UpdateAt<PrecisionNetCourseTooltipSystem>(SystemUpdatePhase.UITooltip);
            log.Info("PrecisionNetCourseTooltipSystem registered (shows alongside vanilla with 300px offset)");

            updateSystem.UpdateAt<PrecisionGuideLineTooltipSystem>(SystemUpdatePhase.UITooltip);
            log.Info("PrecisionGuideLineTooltipSystem registered (shows alongside vanilla with small offset)");

            if (GameManager.instance.modManager.TryGetExecutableAsset(this, out var asset))
                log.Info($"Current mod asset at {asset.path}");
        }

        public void OnDispose()
        {
            log.Info(nameof(OnDispose));

            if (Setting != null)
            {
                Setting.UnregisterInOptionsUI();
                Setting = null;
            }

            Instance = null;
        }
    }
}