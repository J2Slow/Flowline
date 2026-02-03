using System.Collections.Generic;
using System.Linq;
using Flowline.Configuration;
using Flowline.Data;

namespace Flowline.Services;

/// <summary>
/// Converts FFLogs data to Flowline timelines.
/// </summary>
public class FFLogsConverter
{
    private readonly ActionDataService actionDataService;

    public FFLogsConverter(ActionDataService actionDataService)
    {
        this.actionDataService = actionDataService;
    }

    /// <summary>
    /// Converts FFLogs cast events to a Flowline timeline.
    /// </summary>
    public Timeline ConvertToTimeline(
        FFLogsReport report,
        FFLogsFight fight,
        FFLogsActor player,
        List<FFLogsCastEvent> events,
        FFLogsImportOptions options)
    {
        var jobDisplayName = FFLogsMappings.GetJobDisplayName(player.SubType);
        var jobId = FFLogsMappings.GetJobId(player.SubType);

        var timeline = new Timeline
        {
            Name = $"{jobDisplayName} - {fight.Name}",
            TerritoryId = 0, // User can set this later in editor
            DurationSeconds = fight.DurationSeconds,
            Description = $"Imported from FFLogs report {report.Code}, fight #{fight.Id}",
            IsEnabled = true
        };

        foreach (var castEvent in events)
        {
            // Only include "cast" events (completed casts), not "begincast"
            if (castEvent.Type != "cast")
                continue;

            // Filter based on options
            if (!options.IncludeAllActions && !FFLogsMappings.ShouldIncludeAction(castEvent.AbilityGameID, options))
                continue;

            // Convert timestamp from report-relative to fight-relative
            var timestampSeconds = (castEvent.Timestamp - fight.StartTime) / 1000f;

            // Skip if negative (before fight start) or after fight end
            if (timestampSeconds < 0 || timestampSeconds > fight.DurationSeconds)
                continue;

            // Get action data from game data service
            var actionData = actionDataService.GetActionData(castEvent.AbilityGameID);

            // Skip unknown actions unless including all
            if (actionData == null && !options.IncludeAllActions)
                continue;

            var marker = new ActionMarker
            {
                Type = MarkerType.Action,
                TimestampSeconds = timestampSeconds,
                ActionId = castEvent.AbilityGameID,
                PlayerName = string.Empty, // No player name - user IS this player
                JobId = jobId,
                IconId = actionData?.IconId ?? 0,
                DurationSeconds = actionDataService.GetActionDuration(castEvent.AbilityGameID),
                CustomLabel = string.Empty
            };

            timeline.Markers.Add(marker);
        }

        // Sort markers by timestamp
        timeline.Markers = timeline.Markers
            .OrderBy(m => m.TimestampSeconds)
            .ToList();

        // Remove duplicate consecutive actions (same action within 2.5s)
        RemoveDuplicates(timeline.Markers);

        return timeline;
    }

    /// <summary>
    /// Removes duplicate consecutive actions (same action cast multiple times rapidly).
    /// </summary>
    private void RemoveDuplicates(List<ActionMarker> markers)
    {
        for (int i = markers.Count - 1; i > 0; i--)
        {
            var current = markers[i];
            var previous = markers[i - 1];

            // If same action within 2.5 seconds, remove the later one
            if (current.ActionId == previous.ActionId &&
                (current.TimestampSeconds - previous.TimestampSeconds) < 2.5f)
            {
                markers.RemoveAt(i);
            }
        }
    }
}
