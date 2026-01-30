using System;
using System.Collections.Generic;
using Dalamud.Configuration;

namespace Flowline.Configuration;

/// <summary>
/// Display mode for the timeline overlay.
/// </summary>
public enum TimelineDisplayMode
{
    HorizontalScroll,
    VerticalList,
    VerticalScroll
}

/// <summary>
/// Recording mode for action tracking.
/// </summary>
public enum RecordingMode
{
    Manual,
    Automatic,
    Both
}

/// <summary>
/// Main configuration for the Flowline plugin.
/// </summary>
[Serializable]
public class FlowlineConfiguration : IPluginConfiguration
{
    public int Version { get; set; } = 1;

    // Display settings
    public TimelineDisplayMode DisplayMode { get; set; } = TimelineDisplayMode.HorizontalScroll;
    public bool ShowActionIcons { get; set; } = true;
    public bool ShowActionNames { get; set; } = true;
    public bool ShowPlayerNames { get; set; } = true;
    public bool ShowCountdownTimer { get; set; } = true;

    // Overlay settings
    public float OverlayOpacity { get; set; } = 1.0f;
    public bool LockOverlayPosition { get; set; } = false;
    public System.Numerics.Vector2 OverlayPosition { get; set; } = new(100, 100);
    public System.Numerics.Vector2 OverlaySize { get; set; } = new(800, 100);

    // Timeline settings
    public float LookAheadSeconds { get; set; } = 10.0f; // How many seconds ahead to show
    public float ActionDisplayDuration { get; set; } = 3.0f; // How long actions stay visible
    public bool AutoStartOnCountdown { get; set; } = true;

    // Recording settings
    public RecordingMode RecordingMode { get; set; } = RecordingMode.Both;
    public bool RecordPartyActions { get; set; } = true;
    public bool AutoRecordInConfiguredDuties { get; set; } = true;
    public int MaxRecordingsPerDuty { get; set; } = 10; // Auto-delete old recordings

    // Timelines (reference to timeline IDs)
    public List<Guid> TimelineIds { get; set; } = new();

    // Debug settings
    public bool DebugMode { get; set; } = false;
    public bool ShowDebugOverlay { get; set; } = false;

    /// <summary>
    /// Creates a new default configuration.
    /// </summary>
    public FlowlineConfiguration()
    {
    }
}
