using System;

namespace Flowline.Configuration;

/// <summary>
/// Type of marker on the timeline.
/// </summary>
public enum MarkerType
{
    Action,
    TextLabel
}

/// <summary>
/// Represents a single action to display on the timeline at a specific timestamp.
/// </summary>
[Serializable]
public class ActionMarker
{
    /// <summary>
    /// Type of marker (action or text label).
    /// </summary>
    public MarkerType Type { get; set; } = MarkerType.Action;

    /// <summary>
    /// Timestamp in seconds when this action should be displayed.
    /// </summary>
    public float TimestampSeconds { get; set; }

    /// <summary>
    /// The FFXIV action ID (used to look up icon and name).
    /// </summary>
    public uint ActionId { get; set; }

    /// <summary>
    /// Optional player name who should use this action.
    /// </summary>
    public string PlayerName { get; set; } = string.Empty;

    /// <summary>
    /// Optional job ID for the player (used for color coding or filtering).
    /// </summary>
    public uint JobId { get; set; }

    /// <summary>
    /// Cached icon ID from action data (to avoid repeated lookups).
    /// </summary>
    public uint IconId { get; set; }

    /// <summary>
    /// Optional custom label/note for this marker (or text for TextLabel type).
    /// </summary>
    public string CustomLabel { get; set; } = string.Empty;

    /// <summary>
    /// Duration of the action effect in seconds (0 = instant).
    /// </summary>
    public float DurationSeconds { get; set; } = 0f;

    /// <summary>
    /// Color for duration line (stored as ARGB uint).
    /// </summary>
    public uint DurationColor { get; set; } = 0xFFFFFFFF;

    /// <summary>
    /// Whether this is a text-only marker (attack name label).
    /// </summary>
    public bool IsTextMarker => Type == MarkerType.TextLabel;

    /// <summary>
    /// Creates a new action marker.
    /// </summary>
    public ActionMarker()
    {
    }

    /// <summary>
    /// Creates a new action marker with specified values.
    /// </summary>
    public ActionMarker(float timestamp, uint actionId, string playerName = "", uint jobId = 0)
    {
        TimestampSeconds = timestamp;
        ActionId = actionId;
        PlayerName = playerName;
        JobId = jobId;
    }

    /// <summary>
    /// Creates a text label marker.
    /// </summary>
    public static ActionMarker CreateTextLabel(float timestamp, string text)
    {
        return new ActionMarker
        {
            Type = MarkerType.TextLabel,
            TimestampSeconds = timestamp,
            CustomLabel = text,
            ActionId = 0
        };
    }
}
