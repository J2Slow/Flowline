using System.Collections.Generic;
using System.Numerics;
using Flowline.Configuration;
using Flowline.Data;

namespace Flowline.Rendering;

/// <summary>
/// Interface for timeline rendering implementations.
/// </summary>
public interface ITimelineRenderer
{
    /// <summary>
    /// Renders the timeline with the given markers.
    /// </summary>
    /// <param name="markers">List of action markers to display</param>
    /// <param name="currentTime">Current playback time in seconds</param>
    /// <param name="lookAheadSeconds">How many seconds ahead to display</param>
    /// <param name="position">Overlay position</param>
    /// <param name="size">Overlay size</param>
    /// <param name="config">Display configuration</param>
    /// <param name="actionDataService">Service for getting action data</param>
    /// <param name="countdownRemaining">Remaining countdown time (0 or negative = no countdown)</param>
    void Render(
        List<ActionMarker> markers,
        float currentTime,
        float lookAheadSeconds,
        Vector2 position,
        Vector2 size,
        FlowlineConfiguration config,
        ActionDataService actionDataService,
        float countdownRemaining = 0f);
}
