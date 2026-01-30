using System;
using System.Collections.Generic;

namespace Flowline.Configuration;

/// <summary>
/// Represents a timeline configuration for a specific duty encounter.
/// </summary>
[Serializable]
public class Timeline
{
    /// <summary>
    /// Unique identifier for this timeline.
    /// </summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// User-friendly name for this timeline.
    /// </summary>
    public string Name { get; set; } = "Untitled Timeline";

    /// <summary>
    /// Territory/Duty ID this timeline applies to.
    /// </summary>
    public ushort TerritoryId { get; set; }

    /// <summary>
    /// Total duration of the timeline in seconds.
    /// </summary>
    public float DurationSeconds { get; set; } = 600; // Default 10 minutes

    /// <summary>
    /// List of action markers placed on this timeline.
    /// </summary>
    public List<ActionMarker> Markers { get; set; } = new();

    /// <summary>
    /// Whether this timeline is enabled (will activate in duty).
    /// </summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>
    /// Optional description or notes about this timeline.
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Creates a new timeline.
    /// </summary>
    public Timeline()
    {
    }

    /// <summary>
    /// Creates a new timeline with specified values.
    /// </summary>
    public Timeline(string name, ushort territoryId, float durationSeconds)
    {
        Name = name;
        TerritoryId = territoryId;
        DurationSeconds = durationSeconds;
    }
}
