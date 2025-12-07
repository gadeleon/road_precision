using Colossal.Mathematics;
using Game.Tools;
using Game.UI.Tooltip;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace Road_Precision.Systems
{
    /// <summary>
    /// Custom tooltip system for road networks that displays precise float values
    /// alongside vanilla tooltips (which show rounded integers).
    /// Creates separate tooltips with unique path to coexist with vanilla system.
    /// </summary>
    public partial class PrecisionNetCourseTooltipSystem : TooltipSystemBase
    {
        private ToolSystem m_ToolSystem;
        private NetToolSystem m_NetTool;
        private EntityQuery m_NetCourseQuery;
        private TooltipGroup m_Group;
        private StringTooltip m_Length;
        private StringTooltip m_Slope;
        private int m_LengthDecimalPlaces = 2;
        private int m_SlopeDecimalPlaces = 2;
        private int m_FrameCounter = 0;
        private const int SETTINGS_CHECK_INTERVAL = 60; // Check settings every 60 frames (~1 second at 60fps)

        protected override void OnCreate()
        {
            base.OnCreate();

            m_ToolSystem = World.GetOrCreateSystemManaged<ToolSystem>();
            m_NetTool = World.GetOrCreateSystemManaged<NetToolSystem>();

            m_NetCourseQuery = GetEntityQuery(new EntityQueryDesc
            {
                All = new[]
                {
                    ComponentType.ReadOnly<CreationDefinition>(),
                    ComponentType.ReadOnly<NetCourse>()
                }
            });

            RequireForUpdate(m_NetCourseQuery);

            // Get mod settings
            var setting = Mod.Instance?.Setting;
            m_LengthDecimalPlaces = setting?.DistanceDecimalPlaces ?? 2;
            m_SlopeDecimalPlaces = setting?.AngleDecimalPlaces ?? 2;
            bool enableFloatDistance = setting?.EnableFloatDistance ?? true;
            bool enableFloatAngle = setting?.EnableFloatAngle ?? true;

            if (!enableFloatDistance)
                m_LengthDecimalPlaces = 0;

            if (!enableFloatAngle)
                m_SlopeDecimalPlaces = 0;

            // Debug log settings
            Mod.log.Info($"Tooltip settings: LengthDecimals={m_LengthDecimalPlaces}, SlopeDecimals={m_SlopeDecimalPlaces}, EnableFloatDistance={enableFloatDistance}, EnableFloatAngle={enableFloatAngle}");

            // Create standard string tooltips (UI recognizes these)
            m_Length = new StringTooltip
            {
                path = "precisionNetCourse/length",
                icon = "Media/Glyphs/Length.svg",
                value = "0m"
            };

            m_Slope = new StringTooltip
            {
                path = "precisionNetCourse/slope",
                icon = "Media/Glyphs/Slope.svg",
                value = "0%"
            };

            m_Group = new TooltipGroup
            {
                path = "precisionNetCourse",
                horizontalAlignment = TooltipGroup.Alignment.Center,
                verticalAlignment = TooltipGroup.Alignment.Center,
                category = TooltipGroup.Category.Network
            };

            m_Group.children.Add(m_Length);
            m_Group.children.Add(m_Slope);
        }

        protected override void OnUpdate()
        {
            if (m_ToolSystem.activeTool != m_NetTool ||
                m_NetTool.mode == NetToolSystem.Mode.Replace ||
                Camera.main == null)
            {
                return;
            }

            // Periodically check for settings changes
            if (++m_FrameCounter >= SETTINGS_CHECK_INTERVAL)
            {
                m_FrameCounter = 0;
                var setting = Mod.Instance?.Setting;
                if (setting != null)
                {
                    int newLengthDecimals = setting.DistanceDecimalPlaces;
                    int newSlopeDecimals = setting.AngleDecimalPlaces;
                    bool enableFloatDistance = setting.EnableFloatDistance;
                    bool enableFloatAngle = setting.EnableFloatAngle;

                    if (!enableFloatDistance)
                        newLengthDecimals = 0;

                    if (!enableFloatAngle)
                        newSlopeDecimals = 0;

                    // Log if settings changed
                    if (newLengthDecimals != m_LengthDecimalPlaces || newSlopeDecimals != m_SlopeDecimalPlaces)
                    {
                        Mod.log.Info($"Settings updated: LengthDecimals={newLengthDecimals}, SlopeDecimals={newSlopeDecimals}, EnableFloatDistance={enableFloatDistance}, EnableFloatAngle={enableFloatAngle}");
                        m_LengthDecimalPlaces = newLengthDecimals;
                        m_SlopeDecimalPlaces = newSlopeDecimals;
                    }
                }
            }

            CompleteDependency();

            using var courses = new NativeList<NetCourse>(m_NetCourseQuery.CalculateEntityCount(), Allocator.Temp);
            using var chunks = m_NetCourseQuery.ToArchetypeChunkArray(Allocator.Temp);

            var netCourseHandle = GetComponentTypeHandle<NetCourse>(true);
            var creationDefHandle = GetComponentTypeHandle<CreationDefinition>(true);

            float totalLength = 0f;
            float curveLength = 0f;

            foreach (var chunk in chunks)
            {
                var netCourses = chunk.GetNativeArray(ref netCourseHandle);
                var creationDefs = chunk.GetNativeArray(ref creationDefHandle);

                for (int i = 0; i < netCourses.Length; i++)
                {
                    var netCourse = netCourses[i];
                    var creationDef = creationDefs[i];

                    // Filter out certain course types (same logic as original)
                    if (creationDef.m_Original != Entity.Null ||
                        (creationDef.m_Flags & (CreationFlags.Permanent | CreationFlags.Delete |
                         CreationFlags.Upgrade | CreationFlags.Invert | CreationFlags.Align)) != 0 ||
                        (netCourse.m_StartPosition.m_Flags & CoursePosFlags.IsParallel) != 0)
                    {
                        continue;
                    }

                    totalLength += netCourse.m_Length;

                    float2 t = new float2(netCourse.m_StartPosition.m_CourseDelta,
                                         netCourse.m_EndPosition.m_CourseDelta);
                    Bezier4x2 xz = MathUtils.Cut(netCourse.m_Curve, t).xz;
                    curveLength += MathUtils.Length(xz);

                    courses.Add(netCourse);
                }
            }

            // Format length with decimal places
            string formattedLength = curveLength.ToString($"F{m_LengthDecimalPlaces}", System.Globalization.CultureInfo.InvariantCulture);
            m_Length.value = $"[P] {formattedLength}m";

            if (courses.Length > 0 && curveLength >= 12f)
            {
                // Calculate slope
                float startY = courses[0].m_StartPosition.m_Position.y;
                float endY = courses[courses.Length - 1].m_EndPosition.m_Position.y;
                float slopePercent = 100f * (endY - startY) / curveLength;
                float finalSlope = math.select(slopePercent, 0f, math.abs(slopePercent) < 0.05f);

                // Format slope with decimal places
                string sign = finalSlope >= 0 ? "" : "";
                m_Slope.value = $"[P] {sign}{finalSlope.ToString($"F{m_SlopeDecimalPlaces}")}%";

                // Sort courses and find midpoint
                SortCourses(courses);
                float3 worldPos = GetWorldPosition(courses, totalLength / 2f);

                float2 tooltipPos = WorldToTooltipPos(worldPos, out bool visible);

                // Offset precision tooltip to avoid overlap with vanilla tooltip
                // Offset to the right by 300 pixels in screen space
                float2 offsetTooltipPos = tooltipPos + new float2(300f, 0f);

                if (!m_Group.position.Equals(offsetTooltipPos))
                {
                    m_Group.position = offsetTooltipPos;
                    m_Group.SetChildrenChanged();
                }

                if (visible)
                {
                    AddGroup(m_Group);
                }
                else
                {
                    AddMouseTooltip(m_Length);
                    AddMouseTooltip(m_Slope);
                }
            }
        }

        private static float3 GetWorldPosition(NativeList<NetCourse> courses, float length)
        {
            float accumulated = -length;

            foreach (var course in courses)
            {
                accumulated += course.m_Length;
                if (accumulated >= 0f && course.m_Length != 0f)
                {
                    float t = math.lerp(course.m_StartPosition.m_CourseDelta,
                                       course.m_EndPosition.m_CourseDelta,
                                       1f - accumulated / course.m_Length);
                    return MathUtils.Position(course.m_Curve, t);
                }
            }

            return courses[courses.Length - 1].m_EndPosition.m_Position;
        }

        private static void SortCourses(NativeList<NetCourse> courses)
        {
            // Find the first course
            for (int i = 0; i < courses.Length; i++)
            {
                var course = courses[i];
                if ((course.m_StartPosition.m_Flags & CoursePosFlags.IsFirst) != 0)
                {
                    courses[i] = courses[0];
                    courses[0] = course;
                    break;
                }
            }

            // Sort remaining courses by connectivity
            for (int i = 0; i < courses.Length - 1; i++)
            {
                var currentCourse = courses[i];
                for (int j = i + 1; j < courses.Length; j++)
                {
                    var nextCourse = courses[j];
                    if (currentCourse.m_EndPosition.m_Position.Equals(nextCourse.m_StartPosition.m_Position))
                    {
                        courses[j] = courses[i + 1];
                        courses[i + 1] = nextCourse;
                        break;
                    }
                }
            }
        }
    }
}