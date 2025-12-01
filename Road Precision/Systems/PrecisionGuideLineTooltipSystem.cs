using Colossal.Mathematics;
using Game.Net;
using Game.Rendering;
using Game.Tools;
using Game.UI.Tooltip;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace Road_Precision.Systems
{
    /// <summary>
    /// Custom GuideLineTooltipSystem that displays precise values with decimal places.
    /// - LENGTH tooltips: Shows decimal precision (e.g., "12.34m" instead of "12m")
    /// - ANGLE tooltips: Creates additional precise angle tooltips by calculating angles
    ///   directly from NetToolSystem control points (e.g., "89.73°" instead of "90°")
    /// NOTE: Incompatible with ExtendedTooltip mod - please disable one or the other.
    /// </summary>
    public partial class PrecisionGuideLineTooltipSystem : TooltipSystemBase
    {
        private GuideLinesSystem m_GuideLinesSystem;
        private NetToolSystem m_NetToolSystem;
        private ToolSystem m_ToolSystem;
        private List<TooltipGroup> m_Groups;
        private List<TooltipGroup> m_PreciseAngleGroups;
        private ComponentLookup<Edge> m_EdgeData;
        private ComponentLookup<Curve> m_CurveData;
        private ComponentLookup<Game.Net.Node> m_NodeData;
        private BufferLookup<ConnectedEdge> m_ConnectedEdges;
        private int m_AngleDecimalPlaces = 2;
        private int m_LengthDecimalPlaces = 2;
        private int m_FrameCounter = 0;
        private const int SETTINGS_CHECK_INTERVAL = 60; // Check settings every 60 frames (~1 second at 60fps)

        protected override void OnCreate()
        {
            base.OnCreate();
            m_GuideLinesSystem = World.GetOrCreateSystemManaged<GuideLinesSystem>();
            m_NetToolSystem = World.GetOrCreateSystemManaged<NetToolSystem>();
            m_ToolSystem = World.GetOrCreateSystemManaged<ToolSystem>();
            m_Groups = new List<TooltipGroup>();
            m_PreciseAngleGroups = new List<TooltipGroup>();

            // Get mod settings
            var setting = Mod.Instance?.Setting;
            m_AngleDecimalPlaces = setting?.AngleDecimalPlaces ?? 2;
            m_LengthDecimalPlaces = setting?.DistanceDecimalPlaces ?? 2;
            bool enableFloatAngle = setting?.EnableFloatAngle ?? true;
            bool enableFloatDistance = setting?.EnableFloatDistance ?? true;

            if (!enableFloatAngle)
                m_AngleDecimalPlaces = 0;

            if (!enableFloatDistance)
                m_LengthDecimalPlaces = 0;

            Mod.log.Info($"GuideLineTooltip settings: AngleDecimals={m_AngleDecimalPlaces}, LengthDecimals={m_LengthDecimalPlaces}");
        }

        protected override void OnUpdate()
        {
            // Update component lookups
            m_EdgeData = SystemAPI.GetComponentLookup<Edge>(true);
            m_CurveData = SystemAPI.GetComponentLookup<Curve>(true);
            m_NodeData = SystemAPI.GetComponentLookup<Game.Net.Node>(true);
            m_ConnectedEdges = SystemAPI.GetBufferLookup<ConnectedEdge>(true);

            // Periodically check for settings changes
            if (++m_FrameCounter >= SETTINGS_CHECK_INTERVAL)
            {
                m_FrameCounter = 0;
                var setting = Mod.Instance?.Setting;
                if (setting != null)
                {
                    int newAngleDecimals = setting.AngleDecimalPlaces;
                    int newLengthDecimals = setting.DistanceDecimalPlaces;
                    bool enableFloatAngle = setting.EnableFloatAngle;
                    bool enableFloatDistance = setting.EnableFloatDistance;

                    if (!enableFloatAngle)
                        newAngleDecimals = 0;

                    if (!enableFloatDistance)
                        newLengthDecimals = 0;

                    if (newAngleDecimals != m_AngleDecimalPlaces || newLengthDecimals != m_LengthDecimalPlaces)
                    {
                        Mod.log.Info($"GuideLineTooltip settings updated: AngleDecimals={newAngleDecimals}, LengthDecimals={newLengthDecimals}");
                        m_AngleDecimalPlaces = newAngleDecimals;
                        m_LengthDecimalPlaces = newLengthDecimals;
                    }
                }
            }

            JobHandle jobHandle;
            NativeList<GuideLinesSystem.TooltipInfo> tooltips = m_GuideLinesSystem.GetTooltips(out jobHandle);
            jobHandle.Complete();

            for (int i = 0; i < tooltips.Length; i++)
            {
                GuideLinesSystem.TooltipInfo tooltipInfo = tooltips[i];

                if (m_Groups.Count <= i)
                {
                    m_Groups.Add(new TooltipGroup
                    {
                        path = string.Format("precisionGuideLineTooltip{0}", i),
                        horizontalAlignment = TooltipGroup.Alignment.Center,
                        verticalAlignment = TooltipGroup.Alignment.Center,
                        category = TooltipGroup.Category.Network,
                        children =
                        {
                            new StringTooltip()
                        }
                    });
                }

                TooltipGroup tooltipGroup = m_Groups[i];
                bool visible;
                float2 position = WorldToTooltipPos(tooltipInfo.m_Position, out visible);

                if (!tooltipGroup.position.Equals(position))
                {
                    tooltipGroup.position = position;
                    tooltipGroup.SetChildrenChanged();
                }

                StringTooltip stringTooltip = tooltipGroup.children[0] as StringTooltip;
                GuideLinesSystem.TooltipType type = tooltipInfo.m_Type;

                if (type == GuideLinesSystem.TooltipType.Angle)
                {
                    stringTooltip.icon = "Media/Glyphs/Angle.svg";
                    // Note: Angle values are whole numbers due to Burst compilation preventing patching
                    string formattedAngle = tooltipInfo.m_Value.ToString($"F{m_AngleDecimalPlaces}", System.Globalization.CultureInfo.InvariantCulture);
                    stringTooltip.value = $"{formattedAngle}°";
                }
                else if (type == GuideLinesSystem.TooltipType.Length)
                {
                    stringTooltip.icon = "Media/Glyphs/Length.svg";
                    string formattedLength = tooltipInfo.m_Value.ToString($"F{m_LengthDecimalPlaces}", System.Globalization.CultureInfo.InvariantCulture);
                    stringTooltip.value = $"[P] {formattedLength}m";
                }

                AddGroup(tooltipGroup);
            }

            // Create additional precise angle tooltips from NetToolSystem control points
            if (m_ToolSystem.activeTool == m_NetToolSystem)
            {
                JobHandle controlPointsHandle;
                NativeList<ControlPoint> controlPoints = m_NetToolSystem.GetControlPoints(out controlPointsHandle);
                controlPointsHandle.Complete();

                // We need at least 2 control points to calculate angles between segments
                // Each angle requires 2 line segments (3 points total)
                int preciseAngleCount = 0;
                if (controlPoints.Length >= 2)
                {
                    for (int i = 1; i < controlPoints.Length; i++)
                    {
                        ControlPoint prevPoint = controlPoints[i - 1];
                        ControlPoint currentPoint = controlPoints[i];

                        // Calculate line segment between consecutive points
                        float3 segmentStart = prevPoint.m_Position;
                        float3 segmentEnd = currentPoint.m_Position;
                        float segmentLength = math.distance(segmentStart.xz, segmentEnd.xz);

                        // Only calculate angles for segments long enough to be visible
                        if (segmentLength > 0.01f && i >= 1)
                        {
                            // For angle calculation, we need the previous segment too
                            if (i >= 2)
                            {
                                ControlPoint prevPrevPoint = controlPoints[i - 2];

                                // Calculate direction vectors for both segments
                                float3 segment1Start = prevPrevPoint.m_Position;
                                float3 segment1End = prevPoint.m_Position;
                                float segment1Length = math.distance(segment1Start.xz, segment1End.xz);

                                if (segment1Length > 0.01f)
                                {
                                    // Calculate direction vectors (normalized)
                                    // dir1 points backward along first segment (from junction toward start)
                                    // dir2 points forward along second segment (from junction toward end)
                                    // This gives us the interior angle between the two segments
                                    float2 dir1 = (segment1Start.xz - segment1End.xz) / segment1Length;
                                    float2 dir2 = (segmentEnd.xz - segmentStart.xz) / segmentLength;

                                    // Calculate precise angle using the same formula as the game
                                    float dotProduct = math.clamp(math.dot(dir1, dir2), -1f, 1f);
                                    float preciseAngle = math.degrees(math.acos(dotProduct));

                                    // Skip if angle is too close to 0 or 180 (nearly straight line)
                                    if (preciseAngle > 0.1f && preciseAngle < 179.9f)
                                    {
                                        // Create tooltip group for this precise angle
                                        if (m_PreciseAngleGroups.Count <= preciseAngleCount)
                                        {
                                            m_PreciseAngleGroups.Add(new TooltipGroup
                                            {
                                                path = string.Format("preciseAngleTooltip{0}", preciseAngleCount),
                                                horizontalAlignment = TooltipGroup.Alignment.Center,
                                                verticalAlignment = TooltipGroup.Alignment.Center,
                                                category = TooltipGroup.Category.Network,
                                                children =
                                                {
                                                    new StringTooltip()
                                                }
                                            });
                                        }

                                        TooltipGroup angleGroup = m_PreciseAngleGroups[preciseAngleCount];

                                        // Position tooltip at the connection point with slight offset
                                        float3 tooltipWorldPos = prevPoint.m_Position;
                                        // Calculate offset direction (perpendicular to angle bisector)
                                        float2 avgDir = math.normalize(dir1 + dir2);
                                        float2 offsetDir = new float2(-avgDir.y, avgDir.x); // Perpendicular
                                        tooltipWorldPos.xz += offsetDir * 2f; // Offset by 2 units
                                        tooltipWorldPos.y += 1f; // Slight vertical offset

                                        bool visible;
                                        float2 tooltipScreenPos = WorldToTooltipPos(tooltipWorldPos, out visible);

                                        if (!angleGroup.position.Equals(tooltipScreenPos))
                                        {
                                            angleGroup.position = tooltipScreenPos;
                                            angleGroup.SetChildrenChanged();
                                        }

                                        StringTooltip angleTooltip = angleGroup.children[0] as StringTooltip;
                                        angleTooltip.icon = "Media/Glyphs/Angle.svg";
                                        string formattedAngle = preciseAngle.ToString($"F{m_AngleDecimalPlaces}", System.Globalization.CultureInfo.InvariantCulture);
                                        angleTooltip.value = $"[P] {formattedAngle}°";

                                        AddGroup(angleGroup);
                                        preciseAngleCount++;
                                    }
                                }
                            }
                        }
                    }
                }

                // Calculate precise angles for connections to existing roads
                for (int i = 0; i < controlPoints.Length; i++)
                {
                    ControlPoint controlPoint = controlPoints[i];

                    // Check if this control point is snapped to an existing road
                    if (controlPoint.m_OriginalEntity != Entity.Null)
                    {
                        // Try to get the Edge component (if snapped to an edge)
                        if (m_EdgeData.HasComponent(controlPoint.m_OriginalEntity))
                        {
                            Edge edge = m_EdgeData[controlPoint.m_OriginalEntity];

                            // Get the curve of the existing edge
                            if (m_CurveData.HasComponent(controlPoint.m_OriginalEntity))
                            {
                                Curve curve = m_CurveData[controlPoint.m_OriginalEntity];
                                // Determine which end of the edge we're connecting to
                                // and get the appropriate tangent direction
                                float3 existingEdgeTangent;

                                // Check if we're closer to start or end of the edge
                                float distToStart = math.distance(controlPoint.m_Position, curve.m_Bezier.a);
                                float distToEnd = math.distance(controlPoint.m_Position, curve.m_Bezier.d);

                                if (distToStart < distToEnd)
                                {
                                    // Connecting to start of edge - use start tangent (pointing away from edge)
                                    existingEdgeTangent = MathUtils.StartTangent(curve.m_Bezier);
                                }
                                else
                                {
                                    // Connecting to end of edge - use negative end tangent (pointing away from edge)
                                    existingEdgeTangent = -MathUtils.EndTangent(curve.m_Bezier);
                                }

                                // Get the direction of the new road segment
                                float2 existingDir = math.normalizesafe(existingEdgeTangent.xz);

                                // Determine new segment direction based on control point index
                                float2 newSegmentDir = float2.zero;
                                bool hasValidSegment = false;

                                if (i == 0 && controlPoints.Length > 1)
                                {
                                    // First control point - direction to next point
                                    ControlPoint nextPoint = controlPoints[1];
                                    float segLength = math.distance(controlPoint.m_Position.xz, nextPoint.m_Position.xz);
                                    if (segLength > 0.01f)
                                    {
                                        newSegmentDir = (nextPoint.m_Position.xz - controlPoint.m_Position.xz) / segLength;
                                        hasValidSegment = true;
                                    }
                                }
                                else if (i == controlPoints.Length - 1 && i > 0)
                                {
                                    // Last control point - direction from previous point
                                    ControlPoint prevPoint = controlPoints[i - 1];
                                    float segLength = math.distance(prevPoint.m_Position.xz, controlPoint.m_Position.xz);
                                    if (segLength > 0.01f)
                                    {
                                        newSegmentDir = (controlPoint.m_Position.xz - prevPoint.m_Position.xz) / segLength;
                                        hasValidSegment = true;
                                    }
                                }

                                if (hasValidSegment && math.lengthsq(existingDir) > 0.01f)
                                {
                                    // Calculate precise angle
                                    float dotProduct = math.clamp(math.dot(existingDir, newSegmentDir), -1f, 1f);
                                    float preciseConnectionAngle = math.degrees(math.acos(dotProduct));

                                    // Calculate both angles - they are supplementary (add up to 180°)
                                    float angle1 = preciseConnectionAngle;
                                    float angle2 = 180f - preciseConnectionAngle; // Supplementary angle

                                    // Create tooltips for both angles (match vanilla behavior - show all angles except exactly 0)
                                    if (angle1 > 0f && angle1 <= 180f)
                                    {
                                        // First angle tooltip
                                        if (m_PreciseAngleGroups.Count <= preciseAngleCount)
                                        {
                                            m_PreciseAngleGroups.Add(new TooltipGroup
                                            {
                                                path = string.Format("preciseAngleTooltip{0}", preciseAngleCount),
                                                horizontalAlignment = TooltipGroup.Alignment.Center,
                                                verticalAlignment = TooltipGroup.Alignment.Center,
                                                category = TooltipGroup.Category.Network,
                                                children =
                                                {
                                                    new StringTooltip()
                                                }
                                            });
                                        }

                                        TooltipGroup connectionAngleGroup1 = m_PreciseAngleGroups[preciseAngleCount];

                                        // Position first tooltip on the appropriate side (larger offset)
                                        float3 tooltipWorldPos1 = controlPoint.m_Position;
                                        float2 avgDir1 = math.normalize(existingDir + newSegmentDir);
                                        float2 offsetDir1 = new float2(-avgDir1.y, avgDir1.x);
                                        tooltipWorldPos1.xz += offsetDir1 * 10f; // Increased to 10f for better separation
                                        tooltipWorldPos1.y += 2.5f; // Increased vertical offset

                                        bool visible1;
                                        float2 tooltipScreenPos1 = WorldToTooltipPos(tooltipWorldPos1, out visible1);

                                        if (!connectionAngleGroup1.position.Equals(tooltipScreenPos1))
                                        {
                                            connectionAngleGroup1.position = tooltipScreenPos1;
                                            connectionAngleGroup1.SetChildrenChanged();
                                        }

                                        StringTooltip connectionAngleTooltip1 = connectionAngleGroup1.children[0] as StringTooltip;
                                        connectionAngleTooltip1.icon = "Media/Glyphs/Angle.svg";
                                        string formattedConnectionAngle1 = angle1.ToString($"F{m_AngleDecimalPlaces}", System.Globalization.CultureInfo.InvariantCulture);
                                        connectionAngleTooltip1.value = $"[P] {formattedConnectionAngle1}°";

                                        AddGroup(connectionAngleGroup1);
                                        preciseAngleCount++;
                                    }

                                    if (angle2 > 0f && angle2 <= 180f)
                                    {
                                        // Second angle tooltip (on the other side)
                                        if (m_PreciseAngleGroups.Count <= preciseAngleCount)
                                        {
                                            m_PreciseAngleGroups.Add(new TooltipGroup
                                            {
                                                path = string.Format("preciseAngleTooltip{0}", preciseAngleCount),
                                                horizontalAlignment = TooltipGroup.Alignment.Center,
                                                verticalAlignment = TooltipGroup.Alignment.Center,
                                                category = TooltipGroup.Category.Network,
                                                children =
                                                {
                                                    new StringTooltip()
                                                }
                                            });
                                        }

                                        TooltipGroup connectionAngleGroup2 = m_PreciseAngleGroups[preciseAngleCount];

                                        // Position second tooltip on the opposite side (larger offset)
                                        float3 tooltipWorldPos2 = controlPoint.m_Position;
                                        float2 avgDir2 = math.normalize(existingDir - newSegmentDir); // Note: minus for opposite side
                                        float2 offsetDir2 = new float2(-avgDir2.y, avgDir2.x);
                                        tooltipWorldPos2.xz += offsetDir2 * 10f; // Increased to 10f for better separation
                                        tooltipWorldPos2.y += 3.5f; // Larger vertical offset difference

                                        bool visible2;
                                        float2 tooltipScreenPos2 = WorldToTooltipPos(tooltipWorldPos2, out visible2);

                                        if (!connectionAngleGroup2.position.Equals(tooltipScreenPos2))
                                        {
                                            connectionAngleGroup2.position = tooltipScreenPos2;
                                            connectionAngleGroup2.SetChildrenChanged();
                                        }

                                        StringTooltip connectionAngleTooltip2 = connectionAngleGroup2.children[0] as StringTooltip;
                                        connectionAngleTooltip2.icon = "Media/Glyphs/Angle.svg";
                                        string formattedConnectionAngle2 = angle2.ToString($"F{m_AngleDecimalPlaces}", System.Globalization.CultureInfo.InvariantCulture);
                                        connectionAngleTooltip2.value = $"[P] {formattedConnectionAngle2}°";

                                        AddGroup(connectionAngleGroup2);
                                        preciseAngleCount++;
                                    }
                                }
                            }
                        }
                        // If not an edge, check if it's a Node with multiple connected edges
                        else if (m_NodeData.HasComponent(controlPoint.m_OriginalEntity))
                        {
                            // Get all edges connected to this node
                            if (m_ConnectedEdges.HasBuffer(controlPoint.m_OriginalEntity))
                            {
                                DynamicBuffer<ConnectedEdge> connectedEdges = m_ConnectedEdges[controlPoint.m_OriginalEntity];

                                // Determine new segment direction
                                float2 newSegmentDir = float2.zero;
                                bool hasValidSegment = false;

                                if (i == 0 && controlPoints.Length > 1)
                                {
                                    ControlPoint nextPoint = controlPoints[1];
                                    float segLength = math.distance(controlPoint.m_Position.xz, nextPoint.m_Position.xz);
                                    if (segLength > 0.01f)
                                    {
                                        newSegmentDir = (nextPoint.m_Position.xz - controlPoint.m_Position.xz) / segLength;
                                        hasValidSegment = true;
                                    }
                                }
                                else if (i == controlPoints.Length - 1 && i > 0)
                                {
                                    ControlPoint prevPoint = controlPoints[i - 1];
                                    float segLength = math.distance(prevPoint.m_Position.xz, controlPoint.m_Position.xz);
                                    if (segLength > 0.01f)
                                    {
                                        newSegmentDir = (controlPoint.m_Position.xz - prevPoint.m_Position.xz) / segLength;
                                        hasValidSegment = true;
                                    }
                                }

                                if (hasValidSegment)
                                {
                                    // Calculate angles to each connected edge
                                    for (int edgeIdx = 0; edgeIdx < connectedEdges.Length; edgeIdx++)
                                    {
                                        Entity edgeEntity = connectedEdges[edgeIdx].m_Edge;

                                        if (m_EdgeData.HasComponent(edgeEntity) && m_CurveData.HasComponent(edgeEntity))
                                        {
                                            Edge edge = m_EdgeData[edgeEntity];
                                            Curve curve = m_CurveData[edgeEntity];

                                            // Determine direction of existing edge at this node
                                            float3 existingEdgeTangent;
                                            if (edge.m_Start == controlPoint.m_OriginalEntity)
                                            {
                                                // Node is at start of edge - use start tangent
                                                existingEdgeTangent = MathUtils.StartTangent(curve.m_Bezier);
                                            }
                                            else
                                            {
                                                // Node is at end of edge - use negative end tangent
                                                existingEdgeTangent = -MathUtils.EndTangent(curve.m_Bezier);
                                            }

                                            float2 existingDir = math.normalizesafe(existingEdgeTangent.xz);

                                            if (math.lengthsq(existingDir) > 0.01f)
                                            {
                                                // Calculate angle
                                                float dotProduct = math.clamp(math.dot(existingDir, newSegmentDir), -1f, 1f);
                                                float angle = math.degrees(math.acos(dotProduct));

                                                if (angle > 0f && angle <= 180f)
                                                {
                                                    if (m_PreciseAngleGroups.Count <= preciseAngleCount)
                                                    {
                                                        m_PreciseAngleGroups.Add(new TooltipGroup
                                                        {
                                                            path = string.Format("preciseAngleTooltip{0}", preciseAngleCount),
                                                            horizontalAlignment = TooltipGroup.Alignment.Center,
                                                            verticalAlignment = TooltipGroup.Alignment.Center,
                                                            category = TooltipGroup.Category.Network,
                                                            children =
                                                            {
                                                                new StringTooltip()
                                                            }
                                                        });
                                                    }

                                                    TooltipGroup angleGroup = m_PreciseAngleGroups[preciseAngleCount];

                                                    // Position tooltip
                                                    float3 tooltipWorldPos = controlPoint.m_Position;
                                                    float2 avgDir = math.normalize(existingDir + newSegmentDir);
                                                    float2 offsetDir = new float2(-avgDir.y, avgDir.x);
                                                    tooltipWorldPos.xz += offsetDir * (10f + edgeIdx * 3f); // Offset each tooltip
                                                    tooltipWorldPos.y += 2.5f + edgeIdx * 0.5f;

                                                    bool visible;
                                                    float2 tooltipScreenPos = WorldToTooltipPos(tooltipWorldPos, out visible);

                                                    if (!angleGroup.position.Equals(tooltipScreenPos))
                                                    {
                                                        angleGroup.position = tooltipScreenPos;
                                                        angleGroup.SetChildrenChanged();
                                                    }

                                                    StringTooltip angleTooltip = angleGroup.children[0] as StringTooltip;
                                                    angleTooltip.icon = "Media/Glyphs/Angle.svg";
                                                    string formattedAngle = angle.ToString($"F{m_AngleDecimalPlaces}", System.Globalization.CultureInfo.InvariantCulture);
                                                    angleTooltip.value = $"[P] {formattedAngle}°";

                                                    AddGroup(angleGroup);
                                                    preciseAngleCount++;
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }
    }
}