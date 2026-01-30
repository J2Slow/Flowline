using System;
using Dalamud.Plugin.Services;
using Flowline.Configuration;

namespace Flowline.Services;

/// <summary>
/// Detects when the player enters/exits duties and manages timeline activation.
/// </summary>
public class DutyDetectionService : IDisposable
{
    private readonly IClientState clientState;
    private readonly ConfigurationManager configManager;
    private readonly TimelinePlaybackService playbackService;

    private ushort currentTerritoryId = 0;
    private Timeline? activeTimeline;

    /// <summary>
    /// Event fired when a timeline-configured duty is entered.
    /// </summary>
    public event Action<Timeline>? DutyEntered;

    /// <summary>
    /// Event fired when leaving a timeline-configured duty.
    /// </summary>
    public event Action? DutyExited;

    public Timeline? ActiveTimeline => activeTimeline;
    public ushort CurrentTerritoryId => currentTerritoryId;

    public DutyDetectionService(
        IClientState clientState,
        ConfigurationManager configManager,
        TimelinePlaybackService playbackService)
    {
        this.clientState = clientState;
        this.configManager = configManager;
        this.playbackService = playbackService;

        // Subscribe to territory changes
        this.clientState.TerritoryChanged += OnTerritoryChanged;

        // Check current territory on initialization
        CheckCurrentTerritory();
    }

    private void OnTerritoryChanged(ushort territoryId)
    {
        currentTerritoryId = territoryId;
        CheckCurrentTerritory();
    }

    private void CheckCurrentTerritory()
    {
        var previousTimeline = activeTimeline;

        // Check if the current territory has a configured timeline
        var timeline = configManager.GetTimelineForTerritory(currentTerritoryId);

        if (timeline != null && timeline != previousTimeline)
        {
            // Entered a duty with a configured timeline
            activeTimeline = timeline;
            playbackService.LoadTimeline(timeline);
            DutyEntered?.Invoke(timeline);
        }
        else if (timeline == null && previousTimeline != null)
        {
            // Exited a duty with a timeline
            activeTimeline = null;
            playbackService.UnloadTimeline();
            DutyExited?.Invoke();
        }
    }

    /// <summary>
    /// Forces a recheck of the current territory (useful after timeline config changes).
    /// </summary>
    public void RefreshCurrentDuty()
    {
        CheckCurrentTerritory();
    }

    public void Dispose()
    {
        clientState.TerritoryChanged -= OnTerritoryChanged;
    }
}
