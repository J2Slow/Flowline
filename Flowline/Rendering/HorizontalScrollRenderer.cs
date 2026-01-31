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
/// Renders timeline as a horizontal scrolling bar (like a rhythm game).
/// </summary>
public class HorizontalScrollRenderer : ITimelineRenderer
{
    private readonly ITextureProvider textureProvider;
    private readonly Dictionary<uint, ISharedImmediateTexture?> iconCache = new();

    public HorizontalScrollRenderer(ITextureProvider textureProvider)
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
        ActionDataService actionDataService,
        float countdownRemaining = 0f)
    {
        var drawList = ImGui.GetWindowDrawList();

        // Calculate bar dimensions
        var barStart = position;
        var barEnd = position + size;
        var barHeight = size.Y;
        var barWidth = size.X;

        // Draw background
        drawList.AddRectFilled(barStart, barEnd, ImGui.GetColorU32(new Vector4(0, 0, 0, 0.7f)));

        // Draw countdown bar if countdown is active
        if (countdownRemaining > 0)
        {
            DrawCountdownBar(drawList, countdownRemaining, barStart, barWidth, barHeight);
        }

        // Draw timeline bar
        var timelineY = barStart.Y + barHeight / 2;
        drawList.AddLine(
            new Vector2(barStart.X, timelineY),
            new Vector2(barEnd.X, timelineY),
            ImGui.GetColorU32(new Vector4(1, 1, 1, 0.3f)),
            2.0f
        );

        // Draw current position indicator
        var indicatorX = barStart.X + 50; // Fixed position where actions "hit"
        drawList.AddLine(
            new Vector2(indicatorX, barStart.Y),
            new Vector2(indicatorX, barEnd.Y),
            ImGui.GetColorU32(new Vector4(1, 0, 0, 0.8f)),
            3.0f
        );

        // Calculate pixels per second
        var pixelsPerSecond = (barWidth - 100) / lookAheadSeconds;

        // Draw time markers (white every 15s, blue every 60s, green every 120s)
        DrawTimeMarkers(drawList, currentTime, lookAheadSeconds, indicatorX, pixelsPerSecond, barStart, barEnd);

        // Group markers by similar timestamps (within 0.5s of each other)
        var markerGroups = GroupMarkersByTime(markers, currentTime, 0.5f);

        foreach (var group in markerGroups)
        {
            var timeUntil = group.Key;
            if (timeUntil < 0)
                continue;

            // Calculate position (moving from right to left)
            var markerX = indicatorX + (timeUntil * pixelsPerSecond);

            if (markerX > barEnd.X)
                continue;

            // Draw each marker in the group, stacked vertically
            var groupMarkers = group.Value;
            var stackCount = groupMarkers.Count;

            for (int i = 0; i < stackCount; i++)
            {
                var marker = groupMarkers[i];
                // Offset each marker vertically to avoid overlap
                var verticalOffset = (i - (stackCount - 1) / 2f) * 55f;
                DrawMarker(
                    drawList,
                    marker,
                    new Vector2(markerX, timelineY + verticalOffset),
                    config,
                    actionDataService
                );
            }
        }

        // Draw time display
        if (config.ShowCountdownTimer)
        {
            var timeText = $"{currentTime:F1}s";
            var textSize = ImGui.CalcTextSize(timeText);
            var textPos = new Vector2(barStart.X + 10, barStart.Y + 5);
            drawList.AddText(textPos, ImGui.GetColorU32(new Vector4(1, 1, 1, 1)), timeText);
        }
    }

    private void DrawMarker(
        ImDrawListPtr drawList,
        ActionMarker marker,
        Vector2 position,
        FlowlineConfiguration config,
        ActionDataService actionDataService)
    {
        const float iconSize = 48f;
        const float markerWidth = 64f;
        const float markerHeight = 80f;

        var markerStart = position - new Vector2(markerWidth / 2, markerHeight / 2);

        // Handle text-only markers differently
        if (marker.IsTextMarker)
        {
            DrawTextMarker(drawList, marker, position);
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
                    var iconPos = markerStart + new Vector2((markerWidth - iconSize) / 2, 5);
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
        const float indicatorHeight = 20f;

        // Position text left-aligned from the marker position
        var boxStart = new Vector2(position.X + 5, position.Y - textSize.Y / 2 - padding);
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

        // Draw small vertical indicator at exact position
        drawList.AddLine(
            new Vector2(position.X, position.Y - indicatorHeight / 2),
            new Vector2(position.X, position.Y + indicatorHeight / 2),
            borderColor,
            3f
        );

        // Draw text left-aligned inside the box
        var textPos = new Vector2(boxStart.X + padding, boxStart.Y + padding);
        drawList.AddText(textPos, ImGui.GetColorU32(new Vector4(1f, 1f, 0.5f, 1f)), text);
    }

    private void DrawCountdownBar(
        ImDrawListPtr drawList,
        float countdownRemaining,
        Vector2 barStart,
        float barWidth,
        float barHeight)
    {
        // Calculate countdown bar dimensions (attached to left side of timeline)
        var countdownBarHeight = 30f;
        var countdownBarY = barStart.Y + barHeight - countdownBarHeight - 5;

        // Determine countdown bar width based on remaining time
        // Max width represents the initial countdown duration (assume max 30 seconds)
        var maxCountdownSeconds = Math.Max(countdownRemaining, 30f);
        var countdownBarWidth = Math.Min(barWidth * 0.8f, maxCountdownSeconds * 10f);
        var pixelsPerSecond = countdownBarWidth / maxCountdownSeconds;

        // Draw countdown bar background
        var countdownBarStart = new Vector2(barStart.X + 10, countdownBarY);
        var countdownBarEnd = new Vector2(countdownBarStart.X + countdownBarWidth, countdownBarY + countdownBarHeight);
        drawList.AddRectFilled(countdownBarStart, countdownBarEnd, ImGui.GetColorU32(new Vector4(0.2f, 0.1f, 0.1f, 0.9f)));
        drawList.AddRect(countdownBarStart, countdownBarEnd, ImGui.GetColorU32(new Vector4(0.8f, 0.3f, 0.3f, 1f)), 0f, ImDrawFlags.None, 2f);

        // Draw progress fill (fills from right to left as countdown decreases)
        var fillWidth = (countdownRemaining / maxCountdownSeconds) * countdownBarWidth;
        var fillEnd = new Vector2(countdownBarStart.X + fillWidth, countdownBarEnd.Y);
        drawList.AddRectFilled(countdownBarStart, fillEnd, ImGui.GetColorU32(new Vector4(0.8f, 0.3f, 0.3f, 0.6f)));

        // Draw markers: 5 second steps, except last 5 seconds have 1 second markers
        var markerStartTime = (float)Math.Ceiling(countdownRemaining);
        for (float time = markerStartTime; time >= 0; time -= 1f)
        {
            // Determine if this marker should be shown
            bool showMarker;
            if (time <= 5)
            {
                // Last 5 seconds: show every second
                showMarker = true;
            }
            else
            {
                // Before last 5 seconds: show every 5 seconds
                showMarker = time % 5 == 0;
            }

            if (!showMarker)
                continue;

            var markerX = countdownBarStart.X + (time / maxCountdownSeconds) * countdownBarWidth;
            if (markerX < countdownBarStart.X || markerX > countdownBarEnd.X)
                continue;

            // Determine marker style
            Vector4 color;
            float thickness;
            float markerHeight;

            if (time <= 5)
            {
                // Last 5 seconds: yellow/orange, thicker
                color = new Vector4(1f, 0.8f, 0.2f, 1f);
                thickness = 2.5f;
                markerHeight = countdownBarHeight;
            }
            else
            {
                // 5 second markers: white
                color = new Vector4(1f, 1f, 1f, 0.8f);
                thickness = 2f;
                markerHeight = countdownBarHeight * 0.7f;
            }

            // Draw vertical marker
            var markerTop = countdownBarY + (countdownBarHeight - markerHeight) / 2;
            drawList.AddLine(
                new Vector2(markerX, markerTop),
                new Vector2(markerX, markerTop + markerHeight),
                ImGui.GetColorU32(color),
                thickness
            );

            // Draw time label
            var timeLabel = $"{(int)time}";
            var textSize = ImGui.CalcTextSize(timeLabel);
            var textPos = new Vector2(markerX - textSize.X / 2, countdownBarY - textSize.Y - 2);
            drawList.AddText(textPos, ImGui.GetColorU32(color), timeLabel);
        }

        // Draw "Pull in X" text
        var pullText = $"Pull in {countdownRemaining:F0}";
        var pullTextSize = ImGui.CalcTextSize(pullText);
        var pullTextPos = new Vector2(
            countdownBarStart.X + (countdownBarWidth - pullTextSize.X) / 2,
            countdownBarY + (countdownBarHeight - pullTextSize.Y) / 2
        );
        drawList.AddText(pullTextPos, ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 1f)), pullText);
    }

    private void DrawTimeMarkers(
        ImDrawListPtr drawList,
        float currentTime,
        float lookAheadSeconds,
        float indicatorX,
        float pixelsPerSecond,
        Vector2 barStart,
        Vector2 barEnd)
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

            var markerX = indicatorX + (timeUntil * pixelsPerSecond);

            if (markerX > barEnd.X)
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

            // Draw vertical line
            drawList.AddLine(
                new Vector2(markerX, barStart.Y),
                new Vector2(markerX, barEnd.Y),
                ImGui.GetColorU32(color),
                thickness
            );

            // Draw time label for all markers
            var minutes = (int)(time / 60);
            var seconds = (int)(time % 60);
            var timeLabel = minutes > 0 ? $"{minutes}:{seconds:D2}" : $"0:{seconds:D2}";
            var textSize = ImGui.CalcTextSize(timeLabel);
            var textPos = new Vector2(markerX - textSize.X / 2, barStart.Y + 5);
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
