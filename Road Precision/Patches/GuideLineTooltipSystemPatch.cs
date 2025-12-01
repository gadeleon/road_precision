using Game.UI.Tooltip;
using HarmonyLib;

namespace Road_Precision.Patches
{
    /// <summary>
    /// Harmony patch to disable the original GuideLineTooltipSystem
    /// so our custom PrecisionGuideLineTooltipSystem can take over.
    /// NOTE: This makes the mod incompatible with ExtendedTooltip.
    /// </summary>
    [HarmonyPatch(typeof(GuideLineTooltipSystem), "OnUpdate")]
    public static class GuideLineTooltipSystemPatch
    {
        /// <summary>
        /// Prefix patch that prevents the original OnUpdate from running
        /// </summary>
        /// <returns>False to skip the original method</returns>
        static bool Prefix()
        {
            // Return false to prevent the original method from executing
            // Our custom PrecisionGuideLineTooltipSystem will handle tooltips instead
            return false;
        }
    }
}