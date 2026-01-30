using System;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;
using Flowline.Configuration;
using Flowline.Data;
using Flowline.Services;

namespace Flowline.UI;

/// <summary>
/// Window for reviewing recorded encounters.
/// </summary>
public class RecordingReviewWindow : Window
{
    private readonly ActionRecorderService recorderService;
    private readonly ActionDataService actionDataService;
    private readonly DutyDataService dutyDataService;
    private readonly ConfigurationManager configManager;

    private RecordedEncounter? selectedRecording;
    private string filterPlayerName = "";

    public RecordingReviewWindow(
        ActionRecorderService recorderService,
        ActionDataService actionDataService,
        DutyDataService dutyDataService,
        ConfigurationManager configManager)
        : base("Recording Review##FlowlineRecordings")
    {
        this.recorderService = recorderService;
        this.actionDataService = actionDataService;
        this.dutyDataService = dutyDataService;
        this.configManager = configManager;

        Size = new Vector2(700, 500);
        SizeCondition = ImGuiCond.FirstUseEver;
    }

    public override void Draw()
    {
        // Left panel: List of recordings
        ImGui.BeginChild("RecordingsList", new Vector2(250, 0), true);
        DrawRecordingsList();
        ImGui.EndChild();

        ImGui.SameLine();

        // Right panel: Recording details
        ImGui.BeginChild("RecordingDetails", new Vector2(0, 0), true);
        DrawRecordingDetails();
        ImGui.EndChild();
    }

    private void DrawRecordingsList()
    {
        ImGui.Text("Recordings");
        ImGui.Separator();

        var recordings = recorderService.LoadAllRecordings()
            .OrderByDescending(r => r.RecordedAt)
            .ToList();

        if (recordings.Count == 0)
        {
            ImGui.TextColored(new Vector4(0.7f, 0.7f, 0.7f, 1), "No recordings yet.");
            return;
        }

        foreach (var recording in recordings)
        {
            var dutyName = dutyDataService.GetDutyName(recording.TerritoryId);
            var label = $"{recording.Name}\n{dutyName}\n{recording.RecordedAt:MM/dd HH:mm}";

            if (ImGui.Selectable(label, selectedRecording == recording, ImGuiSelectableFlags.AllowDoubleClick))
            {
                selectedRecording = recording;
            }

            ImGui.Separator();
        }
    }

    private void DrawRecordingDetails()
    {
        if (selectedRecording == null)
        {
            ImGui.Text("Select a recording to view details.");
            return;
        }

        ImGui.Text($"Recording: {selectedRecording.Name}");
        ImGui.Text($"Duty: {dutyDataService.GetDutyName(selectedRecording.TerritoryId)}");
        ImGui.Text($"Duration: {selectedRecording.DurationSeconds:F1}s");
        ImGui.Text($"Actions: {selectedRecording.Actions.Count}");
        ImGui.Text($"Status: {(selectedRecording.IsCleared ? "Cleared" : "Not Cleared")}");

        ImGui.Separator();

        // Filter options
        ImGui.InputText("Filter by Player", ref filterPlayerName, 50);

        if (ImGui.Button("Convert to Timeline"))
        {
            ConvertRecordingToTimeline(selectedRecording);
        }

        ImGui.Spacing();
        ImGui.Text("Actions:");
        ImGui.Separator();

        // List actions
        ImGui.BeginChild("ActionsList", new Vector2(0, 0), true);

        var actions = selectedRecording.Actions.AsEnumerable();

        if (!string.IsNullOrEmpty(filterPlayerName))
        {
            actions = actions.Where(a =>
                a.PlayerName.Contains(filterPlayerName, StringComparison.OrdinalIgnoreCase));
        }

        foreach (var action in actions.OrderBy(a => a.TimestampSeconds))
        {
            var actionName = actionDataService.GetActionName(action.ActionId);
            if (actionName.StartsWith("Unknown"))
                actionName = "Unknown Action";

            var text = $"[{action.TimestampSeconds:F1}s] {action.PlayerName}: {actionName}";

            if (!string.IsNullOrEmpty(action.TargetName))
                text += $" → {action.TargetName}";

            ImGui.Text(text);
        }

        ImGui.EndChild();
    }

    private void ConvertRecordingToTimeline(RecordedEncounter recording)
    {
        var timeline = new Timeline
        {
            Name = $"{recording.Name} (Converted)",
            TerritoryId = recording.TerritoryId,
            DurationSeconds = recording.DurationSeconds
        };

        // Convert recorded actions to markers
        // Group by player and action to avoid duplicates
        var groupedActions = recording.Actions
            .Where(a => a.ActionId > 0) // Only include known actions
            .GroupBy(a => new { a.ActionId, a.PlayerName, Timestamp = (int)(a.TimestampSeconds * 2) / 2f }) // Round to 0.5s
            .Select(g => g.First())
            .ToList();

        foreach (var action in groupedActions)
        {
            var marker = new ActionMarker
            {
                TimestampSeconds = action.TimestampSeconds,
                ActionId = action.ActionId,
                PlayerName = action.PlayerName,
                JobId = action.JobId,
                IconId = actionDataService.GetActionIconId(action.ActionId)
            };

            timeline.Markers.Add(marker);
        }

        configManager.SaveTimeline(timeline);

        ImGui.OpenPopup("ConversionComplete");
    }

    public void Dispose()
    {
        // Cleanup if needed
    }
}
