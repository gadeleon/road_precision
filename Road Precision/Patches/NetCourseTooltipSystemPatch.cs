using Game.UI.Tooltip;
using HarmonyLib;

namespace Road_Precision.Patches
{
    /// <summary>
    /// Harmony patch to disable the original NetCourseTooltipSystem
    /// so our custom PrecisionNetCourseTooltipSystem can take over
    /// </summary>
    [HarmonyPatch(typeof(NetCourseTooltipSystem), "OnUpdate")]
    public static class NetCourseTooltipSystemPatch
    {
        /// <summary>
        /// Prefix patch that prevents the original OnUpdate from running
        /// </summary>
        /// <returns>False to skip the original method</returns>
        static bool Prefix()
        {
            // Return false to prevent the original method from executing
            // Our custom PrecisionNetCourseTooltipSystem will handle tooltips instead
            return false;
        }
    }
}