using Game.UI.Tooltip;
using HarmonyLib;
using Unity.Entities;

namespace Road_Precision.Patches
{
    /// <summary>
    /// Harmony patch to disable the original GuideLineTooltipSystem
    /// so our custom PrecisionGuideLineTooltipSystem can take over.
    /// Uses multiple patch methods to ensure it's disabled even when ExtendedTooltip is present.
    /// </summary>
    [HarmonyPatch(typeof(GuideLineTooltipSystem))]
    public static class GuideLineTooltipSystemPatch
    {
        /// <summary>
        /// Prefix patch with highest priority that prevents the original OnUpdate from running
        /// </summary>
        [HarmonyPatch("OnUpdate")]
        [HarmonyPriority(Priority.First)]
        [HarmonyPrefix]
        static bool OnUpdatePrefix(GuideLineTooltipSystem __instance)
        {
            // Disable the system entirely by setting Enabled to false
            __instance.Enabled = false;

            // Return false to prevent the original method from executing
            return false;
        }

        /// <summary>
        /// Also patch OnCreate to prevent initialization
        /// </summary>
        [HarmonyPatch("OnCreate")]
        [HarmonyPriority(Priority.First)]
        [HarmonyPostfix]
        static void OnCreatePostfix(GuideLineTooltipSystem __instance)
        {
            // Disable immediately after creation
            __instance.Enabled = false;
        }
    }
}