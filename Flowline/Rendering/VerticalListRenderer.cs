using System;
using System.Collections.Generic;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Textures;
using Dalamud.Interface.Textures.TextureWraps;
using Dalamud.Plugin.Services;
using Flowline.Configuration;
using Flowline.Data;

namespace Flowline.Rendering;

/// <summary>
/// Renders timeline as a vertical list with countdown timers.
/// </summary>
public class VerticalListRenderer : ITimelineRenderer
{
    private readonly ITextureProvider textureProvider;
    private readonly Dictionary<uint, ISharedImmediateTexture?> iconCache = new();

    public VerticalListRenderer(ITextureProvider textureProvider)
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

        // Draw header
        var headerText = "Upcoming Actions";
        var headerSize = ImGui.CalcTextSize(headerText);
        var headerPos = new Vector2(position.X + (size.X - headerSize.X) / 2, position.Y + 5);
        drawList.AddText(headerPos, ImGui.GetColorU32(new Vector4(1, 1, 1, 1)), headerText);

        // Group markers by similar timestamps (within 0.5s of each other)
        var markerGroups = GroupMarkersByTime(markers, currentTime, 0.5f);
        var sortedGroups = new List<KeyValuePair<float, List<ActionMarker>>>(markerGroups);
        sortedGroups.Sort((a, b) => a.Key.CompareTo(b.Key));

        // Draw markers as a list
        var yOffset = position.Y + headerSize.Y + 15;
        const float rowHeight = 56f;
        const float iconSize = 48f;

        // Track previous marker time for time marker insertion
        float? previousMarkerTime = null;

        foreach (var group in sortedGroups)
        {
            if (yOffset + rowHeight > position.Y + size.Y)
                break; // Don't draw beyond overlay bounds

            var timeUntil = group.Key;
            if (timeUntil < 0)
                continue;

            var groupMarkers = group.Value;
            var firstMarker = groupMarkers[0];

            // Draw time markers between items if crossing a time boundary
            if (previousMarkerTime.HasValue)
            {
                DrawTimeDividers(
                    drawList,
                    previousMarkerTime.Value + currentTime,
                    firstMarker.TimestampSeconds,
                    new Vector2(position.X + 5, yOffset - 3),
                    size.X - 10
                );
            }
            previousMarkerTime = timeUntil;

            // Draw grouped list item (shows multiple icons side by side)
            DrawGroupedListItem(
                drawList,
                groupMarkers,
                new Vector2(position.X + 5, yOffset),
                size.X - 10,
                timeUntil,
                config,
                actionDataService,
                iconSize
            );

            yOffset += rowHeight + 5;
        }
    }

    private void DrawGroupedListItem(
        ImDrawListPtr drawList,
        List<ActionMarker> markers,
        Vector2 position,
        float width,
        float timeUntil,
        FlowlineConfiguration config,
        ActionDataService actionDataService,
        float iconSize)
    {
        var xOffset = position.X;

        // Separate text-only markers from action markers
        var actionMarkers = new List<ActionMarker>();
        var textMarkers = new List<ActionMarker>();
        foreach (var marker in markers)
        {
            if (marker.IsTextMarker)
                textMarkers.Add(marker);
            else
                actionMarkers.Add(marker);
        }

        // Draw icons if configured (multiple icons side by side) - only for action markers
        if (config.ShowActionIcons)
        {
            foreach (var marker in actionMarkers)
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
                        drawList.AddImage(
                            texture.Handle,
                            new Vector2(xOffset, position.Y),
                            new Vector2(xOffset + iconSize, position.Y + iconSize)
                        );
                    }
                }

                xOffset += iconSize + 5;
            }
            if (actionMarkers.Count > 0)
                xOffset += 5; // Extra spacing after icons
        }

        var textY = position.Y + 5;

        // Draw action names if configured (comma-separated for grouped actions)
        if (config.ShowActionNames)
        {
            var actionNames = new List<string>();
            foreach (var marker in actionMarkers)
            {
                var name = actionDataService.GetActionName(marker.ActionId);
                if (!string.IsNullOrEmpty(name) && !actionNames.Contains(name))
                    actionNames.Add(name);
            }

            // Add "Textaction" for text-only markers
            foreach (var marker in textMarkers)
            {
                actionNames.Add("Textaction");
                break; // Only add once
            }

            if (actionNames.Count > 0)
            {
                var actionNameText = string.Join(", ", actionNames);
                drawList.AddText(
                    new Vector2(xOffset, textY),
                    ImGui.GetColorU32(new Vector4(1, 1, 1, 1)),
                    actionNameText
                );
                textY += 18;
            }
        }

        // Draw text labels from text markers (as distinct line)
        if (textMarkers.Count > 0)
        {
            var textLabels = new List<string>();
            foreach (var marker in textMarkers)
            {
                if (!string.IsNullOrEmpty(marker.CustomLabel) && !textLabels.Contains(marker.CustomLabel))
                    textLabels.Add(marker.CustomLabel);
            }

            if (textLabels.Count > 0)
            {
                var textLabelText = string.Join(", ", textLabels);
                drawList.AddText(
                    new Vector2(xOffset, textY),
                    ImGui.GetColorU32(new Vector4(1f, 1f, 0.5f, 1f)),
                    textLabelText
                );
                textY += 18;
            }
        }

        // Draw player names if configured (only for action markers)
        if (config.ShowPlayerNames)
        {
            var playerNames = new List<string>();
            foreach (var marker in actionMarkers)
            {
                if (!string.IsNullOrEmpty(marker.PlayerName) && !playerNames.Contains(marker.PlayerName))
                    playerNames.Add(marker.PlayerName);
            }

            if (playerNames.Count > 0)
            {
                var playerNameText = string.Join(", ", playerNames);
                drawList.AddText(
                    new Vector2(xOffset, textY),
                    ImGui.GetColorU32(new Vector4(0.7f, 0.7f, 1, 1)),
                    playerNameText
                );
            }
        }

        // Draw countdown if configured
        if (config.ShowCountdownTimer)
        {
            var countdownText = $"in {timeUntil:F1}s";
            var countdownSize = ImGui.CalcTextSize(countdownText);
            var countdownPos = new Vector2(
                position.X + width - countdownSize.X,
                position.Y + (iconSize - countdownSize.Y) / 2
            );
            drawList.AddText(
                countdownPos,
                ImGui.GetColorU32(new Vector4(1, 1, 0, 1)),
                countdownText
            );
        }
    }

    private void DrawListItem(
        ImDrawListPtr drawList,
        ActionMarker marker,
        Vector2 position,
        float width,
        float timeUntil,
        FlowlineConfiguration config,
        ActionDataService actionDataService,
        float iconSize)
    {
        var xOffset = position.X;

        // Draw icon if configured
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
                    drawList.AddImage(
                        texture.Handle,
                        position,
                        position + new Vector2(iconSize, iconSize)
                    );
                }
            }

            xOffset += iconSize + 10;
        }

        var textY = position.Y + 5;

        // Draw action name if configured
        if (config.ShowActionNames)
        {
            var actionName = actionDataService.GetActionName(marker.ActionId);
            if (!string.IsNullOrEmpty(actionName))
            {
                drawList.AddText(
                    new Vector2(xOffset, textY),
                    ImGui.GetColorU32(new Vector4(1, 1, 1, 1)),
                    actionName
                );
                textY += 18;
            }
        }

        // Draw player name if configured
        if (config.ShowPlayerNames && !string.IsNullOrEmpty(marker.PlayerName))
        {
            drawList.AddText(
                new Vector2(xOffset, textY),
                ImGui.GetColorU32(new Vector4(0.7f, 0.7f, 1, 1)),
                marker.PlayerName
            );
            textY += 18;
        }

        // Draw countdown if configured
        if (config.ShowCountdownTimer)
        {
            var countdownText = $"in {timeUntil:F1}s";
            var countdownSize = ImGui.CalcTextSize(countdownText);
            var countdownPos = new Vector2(
                position.X + width - countdownSize.X,
                position.Y + (iconSize - countdownSize.Y) / 2
            );
            drawList.AddText(
                countdownPos,
                ImGui.GetColorU32(new Vector4(1, 1, 0, 1)),
                countdownText
            );
        }
    }

    private void DrawTimeDividers(
        ImDrawListPtr drawList,
        float previousTime,
        float currentTime,
        Vector2 position,
        float width)
    {
        // Check if we crossed any time boundaries between previous and current marker
        var previousMinute = (int)(previousTime / 60);
        var currentMinute = (int)(currentTime / 60);

        // Check for 2-minute markers (green)
        var previous2Min = (int)(previousTime / 120);
        var current2Min = (int)(currentTime / 120);
        if (current2Min > previous2Min)
        {
            var timeLabel = $"{current2Min * 2}:00";
            DrawTimeDivider(drawList, position, width, timeLabel, new Vector4(0, 1, 0, 0.6f), 2.0f);
            return; // Don't draw other markers if we have a 2-minute marker
        }

        // Check for 1-minute markers (blue)
        if (currentMinute > previousMinute)
        {
            var minutes = currentMinute;
            var timeLabel = $"{minutes}:00";
            DrawTimeDivider(drawList, position, width, timeLabel, new Vector4(0.3f, 0.6f, 1, 0.5f), 1.5f);
            return; // Don't draw 30s marker if we have a minute marker
        }

        // Check for 15-second markers (white)
        var previous15s = (int)(previousTime / 15);
        var current15s = (int)(currentTime / 15);
        if (current15s > previous15s)
        {
            var seconds = current15s * 15;
            var minutes = seconds / 60;
            var secs = seconds % 60;
            var timeLabel = minutes > 0 ? $"{minutes}:{secs:D2}" : $"0:{secs:D2}";
            DrawTimeDivider(drawList, position, width, timeLabel, new Vector4(1, 1, 1, 0.3f), 1.0f);
        }
    }

    private void DrawTimeDivider(
        ImDrawListPtr drawList,
        Vector2 position,
        float width,
        string? timeLabel,
        Vector4 color,
        float thickness)
    {
        // Draw horizontal line
        drawList.AddLine(
            position,
            new Vector2(position.X + width, position.Y),
            ImGui.GetColorU32(color),
            thickness
        );

        // Draw time label if provided
        if (!string.IsNullOrEmpty(timeLabel))
        {
            var textSize = ImGui.CalcTextSize(timeLabel);
            var textPos = new Vector2(position.X + width - textSize.X - 5, position.Y - textSize.Y - 2);
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
