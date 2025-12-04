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

namespace Road_Precision.Systems
{
    /// <summary>
    /// Custom GuideLineTooltipSystem that displays precise values with decimal places.
    /// - LENGTH tooltips: Shows decimal precision (e.g., "12.34m" instead of "12m")
    /// - ANGLE tooltips: Creates additional precise angle tooltips by calculating angles
    ///   directly from NetToolSystem control points (e.g., "89.73°" instead of "90°")
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

            // First, calculate all precise angles and store them
            // We'll match by rounded angle value, not position (since vanilla offsets tooltip positions)
            List<float> preciseAngles = new List<float>();

            if (m_ToolSystem.activeTool == m_NetToolSystem)
            {
                JobHandle controlPointsHandle;
                NativeList<ControlPoint> controlPoints = m_NetToolSystem.GetControlPoints(out controlPointsHandle);
                controlPointsHandle.Complete();

                // Calculate angles between consecutive control points
                for (int i = 2; i < controlPoints.Length; i++)
                {
                    ControlPoint prevPrevPoint = controlPoints[i - 2];
                    ControlPoint prevPoint = controlPoints[i - 1];
                    ControlPoint currentPoint = controlPoints[i];

                    float3 segment1Start = prevPrevPoint.m_Position;
                    float3 segment1End = prevPoint.m_Position;
                    float segment1Length = math.distance(segment1Start.xz, segment1End.xz);

                    float3 segment2Start = prevPoint.m_Position;
                    float3 segment2End = currentPoint.m_Position;
                    float segment2Length = math.distance(segment2Start.xz, segment2End.xz);

                    if (segment1Length > 0.01f && segment2Length > 0.01f)
                    {
                        float2 dir1 = (segment1Start.xz - segment1End.xz) / segment1Length;
                        float2 dir2 = (segment2End.xz - segment2Start.xz) / segment2Length;

                        float dotProduct = math.clamp(math.dot(dir1, dir2), -1f, 1f);
                        float preciseAngle = math.degrees(math.acos(dotProduct));

                        if (preciseAngle > 0.1f && preciseAngle < 179.9f)
                        {
                            // Store the precise angle
                            preciseAngles.Add(preciseAngle);
                        }
                    }
                }

                // Calculate angles for connections to existing roads
                for (int i = 0; i < controlPoints.Length; i++)
                {
                    ControlPoint controlPoint = controlPoints[i];

                    if (controlPoint.m_OriginalEntity != Entity.Null && m_EdgeData.HasComponent(controlPoint.m_OriginalEntity))
                    {
                        if (m_CurveData.HasComponent(controlPoint.m_OriginalEntity))
                        {
                            Curve curve = m_CurveData[controlPoint.m_OriginalEntity];
                            float3 existingEdgeTangent;

                            float distToStart = math.distance(controlPoint.m_Position, curve.m_Bezier.a);
                            float distToEnd = math.distance(controlPoint.m_Position, curve.m_Bezier.d);

                            if (distToStart < distToEnd)
                            {
                                existingEdgeTangent = MathUtils.StartTangent(curve.m_Bezier);
                            }
                            else
                            {
                                existingEdgeTangent = -MathUtils.EndTangent(curve.m_Bezier);
                            }

                            float2 existingDir = math.normalizesafe(existingEdgeTangent.xz);
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

                            if (hasValidSegment && math.lengthsq(existingDir) > 0.01f)
                            {
                                float dotProduct = math.clamp(math.dot(existingDir, newSegmentDir), -1f, 1f);
                                float preciseConnectionAngle = math.degrees(math.acos(dotProduct));

                                // Calculate both angles - they are supplementary
                                float angle1 = preciseConnectionAngle;
                                float angle2 = 180f - preciseConnectionAngle;

                                // Store both angles
                                if (angle1 > 0f && angle1 <= 180f)
                                {
                                    preciseAngles.Add(angle1);
                                }
                                if (angle2 > 0f && angle2 <= 180f)
                                {
                                    preciseAngles.Add(angle2);
                                }
                            }
                        }
                    }
                    // If not an edge, check if it's a Node with multiple connected edges (corner/intersection)
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
                                                preciseAngles.Add(angle);

                                                // Also add supplementary angle
                                                float supplementary = 180f - angle;
                                                if (supplementary > 0f && supplementary <= 180f)
                                                {
                                                    preciseAngles.Add(supplementary);
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

            JobHandle jobHandle;
            NativeList<GuideLinesSystem.TooltipInfo> tooltips = m_GuideLinesSystem.GetTooltips(out jobHandle);
            jobHandle.Complete();

            for (int i = 0; i < tooltips.Length; i++)
            {
                GuideLinesSystem.TooltipInfo tooltipInfo = tooltips[i];
                GuideLinesSystem.TooltipType type = tooltipInfo.m_Type;

                // For angle tooltips, skip if we don't have precise angles to show
                if (type == GuideLinesSystem.TooltipType.Angle && preciseAngles.Count == 0)
                {
                    continue; // Don't show [P] angle tooltips when we can't calculate precise values
                }

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

                // Add offset to precision tooltip to separate from vanilla
                position.y += 55f;  // Vertical offset

                if (!tooltipGroup.position.Equals(position))
                {
                    tooltipGroup.position = position;
                    tooltipGroup.SetChildrenChanged();
                }

                StringTooltip stringTooltip = tooltipGroup.children[0] as StringTooltip;

                // Process both angle and length tooltips from vanilla system
                if (type == GuideLinesSystem.TooltipType.Angle)
                {
                    stringTooltip.icon = "Media/Glyphs/Angle.svg";

                    // Match by rounded angle value (vanilla tooltips are rounded to integers)
                    float angleValue = tooltipInfo.m_Value; // Default to vanilla value
                    int vanillaRounded = (int)math.round(tooltipInfo.m_Value);

                    // Find a precise angle that rounds to the vanilla value
                    float bestMatch = angleValue;
                    float smallestDiff = float.MaxValue;

                    foreach (float preciseAngle in preciseAngles)
                    {
                        int preciseRounded = (int)math.round(preciseAngle);

                        if (preciseRounded == vanillaRounded)
                        {
                            // This precise angle rounds to the same value as vanilla
                            float diff = math.abs(preciseAngle - tooltipInfo.m_Value);
                            if (diff < smallestDiff)
                            {
                                smallestDiff = diff;
                                bestMatch = preciseAngle;
                            }
                        }
                    }

                    angleValue = bestMatch;

                    string formattedAngle = angleValue.ToString($"F{m_AngleDecimalPlaces}", System.Globalization.CultureInfo.InvariantCulture);
                    stringTooltip.value = $"[P] {formattedAngle}°";
                }
                else if (type == GuideLinesSystem.TooltipType.Length)
                {
                    stringTooltip.icon = "Media/Glyphs/Length.svg";
                    string formattedLength = tooltipInfo.m_Value.ToString($"F{m_LengthDecimalPlaces}", System.Globalization.CultureInfo.InvariantCulture);
                    stringTooltip.value = $"[P] {formattedLength}m";
                }

                AddGroup(tooltipGroup);
            }

            // COMMENTED OUT: Create additional precise angle tooltips from NetToolSystem control points
            // These are now shown in the [P] tooltips with 55px offset instead
            /*
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

                // COMMENTED OUT: Calculate precise angles for connections to existing roads
                // These tooltips are now shown in the [P] tooltips with 55px offset instead
                /*
                for (int i = 0; i < controlPoints.Length; i++)
                {
                    ControlPoint controlPoint = controlPoints[i];

                    // Check if this control point is snapped to an existing road
                    if (controlPoint.m_OriginalEntity != Entity.Null)
                    {
                        // Edge connection tooltips (snapping to middle of existing roads)
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

                                    // Skip if angle is too close to 0 or 180 (nearly straight line)
                                    if (preciseConnectionAngle > 0.1f && preciseConnectionAngle < 179.9f)
                                    {
                                        // Create tooltip group for this precise connection angle
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

                                        TooltipGroup connectionAngleGroup = m_PreciseAngleGroups[preciseAngleCount];

                                        // Position tooltip at connection point
                                        float3 tooltipWorldPos = controlPoint.m_Position;
                                        float2 avgDir = math.normalize(existingDir + newSegmentDir);
                                        float2 offsetDir = new float2(-avgDir.y, avgDir.x);
                                        tooltipWorldPos.xz += offsetDir * 2f;
                                        tooltipWorldPos.y += 1f;

                                        bool visible;
                                        float2 tooltipScreenPos = WorldToTooltipPos(tooltipWorldPos, out visible);

                                        if (!connectionAngleGroup.position.Equals(tooltipScreenPos))
                                        {
                                            connectionAngleGroup.position = tooltipScreenPos;
                                            connectionAngleGroup.SetChildrenChanged();
                                        }

                                        StringTooltip connectionAngleTooltip = connectionAngleGroup.children[0] as StringTooltip;
                                        connectionAngleTooltip.icon = "Media/Glyphs/Angle.svg";
                                        string formattedConnectionAngle = preciseConnectionAngle.ToString($"F{m_AngleDecimalPlaces}", System.Globalization.CultureInfo.InvariantCulture);
                                        connectionAngleTooltip.value = $"[P] {formattedConnectionAngle}°";

                                        AddGroup(connectionAngleGroup);
                                        preciseAngleCount++;
                                    }
                                }
                            }
                        }
                        */
                        // COMMENTED OUT: Node/intersection connection tooltips
                        // Temporarily disabled to see how it looks without them
                        /*
                        // If not an edge, check if it's a Node with multiple connected edges (corner/intersection)
                        if (m_NodeData.HasComponent(controlPoint.m_OriginalEntity))
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
                                                    tooltipWorldPos.xz += offsetDir * 3f;
                                                    tooltipWorldPos.y += 1.5f;

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
            */
        }
    }
}
