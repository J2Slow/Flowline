using System;
using System.Collections.Generic;
using System.Numerics;
using Dalamud.Interface.Textures;
using Dalamud.Interface.Textures.TextureWraps;
using Dalamud.Plugin.Services;
using Flowline.Configuration;
using Flowline.Data;
using Dalamud.Bindings.ImGui;

namespace Flowline.Rendering;

/// <summary>
/// Renders timeline as a vertical scrolling bar (actions flow top to bottom).
/// </summary>
public class VerticalScrollRenderer : ITimelineRenderer
{
    private readonly ITextureProvider textureProvider;
    private readonly Dictionary<uint, ISharedImmediateTexture?> iconCache = new();

    public VerticalScrollRenderer(ITextureProvider textureProvider)
    {
        this.textureProvider = textureProvider;
    }

    public void Render(
        List<ActionMarker> markers,
        float currentTime,
        float lookAheadSeconds,
        Vector2 position,
        Vector2 size,
        FlowlineConfiguration config,
        ActionDataService actionDataService)
    {
        var drawList = ImGui.GetWindowDrawList();

        // Draw background
        drawList.AddRectFilled(position, position + size, ImGui.GetColorU32(new Vector4(0, 0, 0, 0.7f)));

        // Draw timeline track (vertical line)
        var trackX = position.X + size.X / 2;
        drawList.AddLine(
            new Vector2(trackX, position.Y),
            new Vector2(trackX, position.Y + size.Y),
            ImGui.GetColorU32(new Vector4(1, 1, 1, 0.3f)),
            2.0f
        );

        // Draw hit indicator (where actions "hit")
        var indicatorY = position.Y + 50;
        drawList.AddLine(
            new Vector2(position.X, indicatorY),
            new Vector2(position.X + size.X, indicatorY),
            ImGui.GetColorU32(new Vector4(1, 0, 0, 0.8f)),
            3.0f
        );

        // Calculate pixels per second
        var pixelsPerSecond = (size.Y - 100) / lookAheadSeconds;

        // Draw time markers (white every 15s, blue every 60s, green every 120s)
        DrawTimeMarkers(drawList, currentTime, lookAheadSeconds, indicatorY, pixelsPerSecond, position, size);

        // Group markers by similar timestamps (within 0.5s of each other)
        var markerGroups = GroupMarkersByTime(markers, currentTime, 0.5f);

        foreach (var group in markerGroups)
        {
            var timeUntil = group.Key;
            if (timeUntil < 0)
                continue;

            // Calculate position (moving from bottom to top)
            var markerY = indicatorY + (timeUntil * pixelsPerSecond);

            if (markerY > position.Y + size.Y)
                continue;

            // Draw each marker in the group, offset horizontally
            var groupMarkers = group.Value;
            var stackCount = groupMarkers.Count;

            for (int i = 0; i < stackCount; i++)
            {
                var marker = groupMarkers[i];
                // Offset each marker horizontally to avoid overlap
                var horizontalOffset = (i - (stackCount - 1) / 2f) * 60f;
                DrawMarker(
                    drawList,
                    marker,
                    new Vector2(trackX + horizontalOffset, markerY),
                    config,
                    actionDataService
                );
            }
        }

        // Draw time display
        if (config.ShowCountdownTimer)
        {
            var timeText = $"{currentTime:F1}s";
            drawList.AddText(
                new Vector2(position.X + 5, position.Y + 5),
                ImGui.GetColorU32(new Vector4(1, 1, 1, 1)),
                timeText
            );
        }
    }

    private void DrawMarker(
        ImDrawListPtr drawList,
        ActionMarker marker,
        Vector2 centerPosition,
        FlowlineConfiguration config,
        ActionDataService actionDataService)
    {
        const float iconSize = 48f;

        // Handle text-only markers differently
        if (marker.IsTextMarker)
        {
            DrawTextMarker(drawList, marker, centerPosition);
            return;
        }

        // Draw icon only for action markers
        if (config.ShowActionIcons)
        {
            var iconId = marker.IconId;
            if (iconId == 0)
            {
                iconId = actionDataService.GetActionIconId(marker.ActionId);
            }

            if (iconId != 0)
            {
                var texture = GetOrLoadIcon(iconId);
                if (texture != null)
                {
                    var iconPos = centerPosition - new Vector2(iconSize / 2, iconSize / 2);
                    drawList.AddImage(
                        texture.Handle,
                        iconPos,
                        iconPos + new Vector2(iconSize, iconSize)
                    );
                }
            }
        }
    }

    private void DrawTextMarker(
        ImDrawListPtr drawList,
        ActionMarker marker,
        Vector2 position)
    {
        var text = marker.CustomLabel;
        if (string.IsNullOrEmpty(text)) return;

        var textSize = ImGui.CalcTextSize(text);
        const float padding = 6f;
        const float indicatorWidth = 20f;

        // Position text left-aligned from the marker position
        var boxStart = new Vector2(position.X + 10, position.Y - textSize.Y / 2 - padding);
        var boxEnd = new Vector2(boxStart.X + textSize.X + padding * 2, boxStart.Y + textSize.Y + padding * 2);

        // Draw border box around text
        var borderColor = ImGui.GetColorU32(new Vector4(1f, 1f, 0.5f, 0.8f));
        var bgColor = ImGui.GetColorU32(new Vector4(0.1f, 0.1f, 0.1f, 0.9f));
        drawList.AddRectFilled(boxStart, boxEnd, bgColor);
        drawList.AddRect(boxStart, boxEnd, borderColor, 0f, ImDrawFlags.None, 2f);

        // Draw timeline position indicator (small line from marker to text box)
        drawList.AddLine(
            position,
            new Vector2(boxStart.X, position.Y),
            borderColor,
            2f
        );

        // Draw small horizontal indicator at exact position
        drawList.AddLine(
            new Vector2(position.X - indicatorWidth / 2, position.Y),
            new Vector2(position.X + indicatorWidth / 2, position.Y),
            borderColor,
            3f
        );

        // Draw text left-aligned inside the box
        var textPos = new Vector2(boxStart.X + padding, boxStart.Y + padding);
        drawList.AddText(textPos, ImGui.GetColorU32(new Vector4(1f, 1f, 0.5f, 1f)), text);
    }

    private void DrawTimeMarkers(
        ImDrawListPtr drawList,
        float currentTime,
        float lookAheadSeconds,
        float indicatorY,
        float pixelsPerSecond,
        Vector2 position,
        Vector2 size)
    {
        var endTime = currentTime + lookAheadSeconds;

        // Find the first marker timestamp (round up to nearest 15s)
        var firstMarkerTime = (float)(Math.Ceiling(currentTime / 15.0f) * 15.0f);

        // Draw markers from first marker to end of lookahead
        for (var time = firstMarkerTime; time <= endTime; time += 15f)
        {
            var timeUntil = time - currentTime;
            if (timeUntil < 0)
                continue;

            var markerY = indicatorY + (timeUntil * pixelsPerSecond);

            if (markerY > position.Y + size.Y)
                break;

            // Determine color based on time interval
            Vector4 color;
            float thickness;
            if (time % 120 == 0) // Green every 2 minutes
            {
                color = new Vector4(0, 1, 0, 0.6f);
                thickness = 2.5f;
            }
            else if (time % 60 == 0) // Blue every minute
            {
                color = new Vector4(0.3f, 0.6f, 1, 0.5f);
                thickness = 2.0f;
            }
            else // White every 15 seconds
            {
                color = new Vector4(1, 1, 1, 0.3f);
                thickness = 1.5f;
            }

            // Draw horizontal line
            drawList.AddLine(
                new Vector2(position.X, markerY),
                new Vector2(position.X + size.X, markerY),
                ImGui.GetColorU32(color),
                thickness
            );

            // Draw time label for all markers
            var minutes = (int)(time / 60);
            var seconds = (int)(time % 60);
            var timeLabel = minutes > 0 ? $"{minutes}:{seconds:D2}" : $"0:{seconds:D2}";
            var textSize = ImGui.CalcTextSize(timeLabel);
            var textPos = new Vector2(position.X + 5, markerY - textSize.Y / 2);
            drawList.AddText(textPos, ImGui.GetColorU32(new Vector4(1, 1, 1, 0.8f)), timeLabel);
        }
    }

    private Dictionary<float, List<ActionMarker>> GroupMarkersByTime(
        List<ActionMarker> markers,
        float currentTime,
        float groupThreshold)
    {
        var groups = new Dictionary<float, List<ActionMarker>>();

        foreach (var marker in markers)
        {
            var timeUntil = marker.TimestampSeconds - currentTime;

            // Find an existing group within threshold
            float? matchingGroup = null;
            foreach (var existingTime in groups.Keys)
            {
                if (Math.Abs(existingTime - timeUntil) <= groupThreshold)
                {
                    matchingGroup = existingTime;
                    break;
                }
            }

            if (matchingGroup.HasValue)
            {
                groups[matchingGroup.Value].Add(marker);
            }
            else
            {
                groups[timeUntil] = new List<ActionMarker> { marker };
            }
        }

        return groups;
    }

    private IDalamudTextureWrap? GetOrLoadIcon(uint iconId)
    {
        if (iconCache.TryGetValue(iconId, out var cachedTexture))
        {
            return cachedTexture?.GetWrapOrDefault();
        }

        try
        {
            var texture = textureProvider.GetFromGameIcon(new GameIconLookup { IconId = iconId, HiRes = true });
            iconCache[iconId] = texture;
            return texture.GetWrapOrDefault();
        }
        catch
        {
            iconCache[iconId] = null;
            return null;
        }
    }
}
