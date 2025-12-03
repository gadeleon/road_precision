using Colossal.IO.AssetDatabase;
using Colossal.Logging;
using Game;
using Game.Modding;
using Game.SceneFlow;
using HarmonyLib;
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

        private Harmony m_Harmony;

        public void OnLoad(UpdateSystem updateSystem)
        {
            log.Info(nameof(OnLoad));

            Instance = this;

            // Load mod settings
            Setting = new Setting(this);
            Setting.RegisterInOptionsUI();
            GameManager.instance.localizationManager.AddSource("en-US", new LocaleEN(Setting));
            AssetDatabase.global.LoadSettings(nameof(Road_Precision), Setting, new Setting(this));

            // Apply Harmony patches to disable vanilla tooltip systems
            m_Harmony = new Harmony($"{nameof(Road_Precision)}.{nameof(Mod)}");
            m_Harmony.PatchAll(typeof(Mod).Assembly);
            log.Info("Harmony patches applied (NetCourseTooltipSystem and GuideLineTooltipSystem disabled)");

            // Register custom tooltip systems that replace vanilla tooltips with precision versions
            updateSystem.UpdateAt<PrecisionNetCourseTooltipSystem>(SystemUpdatePhase.UITooltip);
            log.Info("PrecisionNetCourseTooltipSystem registered (replaces vanilla NetCourse tooltips)");

            updateSystem.UpdateAt<PrecisionGuideLineTooltipSystem>(SystemUpdatePhase.UITooltip);
            log.Info("PrecisionGuideLineTooltipSystem registered (replaces vanilla GuideLine tooltips)");

            log.Info("NOTE: Tooltip patches are incompatible with ExtendedTooltip mod. Please disable one or the other.");

            if (GameManager.instance.modManager.TryGetExecutableAsset(this, out var asset))
                log.Info($"Current mod asset at {asset.path}");
        }

        public void OnDispose()
        {
            log.Info(nameof(OnDispose));

            // Unpatch Harmony
            m_Harmony?.UnpatchSelf();

            if (Setting != null)
            {
                Setting.UnregisterInOptionsUI();
                Setting = null;
            }

            Instance = null;
        }
    }
}