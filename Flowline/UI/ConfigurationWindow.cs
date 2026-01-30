using System;
using System.Linq;
using System.Numerics;
using Dalamud.Interface;
using Dalamud.Interface.Components;
using Dalamud.Interface.Windowing;
using Flowline.Configuration;
using Flowline.Data;
using Dalamud.Bindings.ImGui;

namespace Flowline.UI;

/// <summary>
/// Main configuration window for Flowline settings.
/// </summary>
public class ConfigurationWindow : Window
{
    private readonly ConfigurationManager configManager;
    private readonly DutyDataService dutyDataService;
    private readonly TimelineEditorWindow editorWindow;

    public ConfigurationWindow(
        ConfigurationManager configManager,
        DutyDataService dutyDataService,
        TimelineEditorWindow editorWindow)
        : base("Flowline Configuration##FlowlineConfig")
    {
        this.configManager = configManager;
        this.dutyDataService = dutyDataService;
        this.editorWindow = editorWindow;

        Size = new Vector2(600, 500);
        SizeCondition = ImGuiCond.FirstUseEver;
    }

    public override void Draw()
    {
        var config = configManager.Configuration;

        if (ImGui.BeginTabBar("FlowlineConfigTabs"))
        {
            if (ImGui.BeginTabItem("Display"))
            {
                DrawDisplaySettings(config);
                ImGui.EndTabItem();
            }

            if (ImGui.BeginTabItem("Timelines"))
            {
                DrawTimelinesTab(config);
                ImGui.EndTabItem();
            }

            if (ImGui.BeginTabItem("Recording"))
            {
                DrawRecordingSettings(config);
                ImGui.EndTabItem();
            }

            if (ImGui.BeginTabItem("Advanced"))
            {
                DrawAdvancedSettings(config);
                ImGui.EndTabItem();
            }

            ImGui.EndTabBar();
        }
    }

    private void DrawDisplaySettings(FlowlineConfiguration config)
    {
        ImGui.Text("Display Mode");
        ImGui.Separator();

        var displayMode = (int)config.DisplayMode;
        if (ImGui.Combo("##DisplayMode", ref displayMode, "Horizontal Scroll\0Vertical List\0Vertical Scroll\0"))
        {
            config.DisplayMode = (TimelineDisplayMode)displayMode;
            configManager.SaveConfiguration();
        }

        ImGui.Spacing();
        ImGui.Text("Display Options");
        ImGui.Separator();

        var showActionIcons = config.ShowActionIcons;
        if (ImGui.Checkbox("Show Action Icons", ref showActionIcons))
        {
            config.ShowActionIcons = showActionIcons;
            configManager.SaveConfiguration();
        }

        var showActionNames = config.ShowActionNames;
        if (ImGui.Checkbox("Show Action Names", ref showActionNames))
        {
            config.ShowActionNames = showActionNames;
            configManager.SaveConfiguration();
        }

        var showPlayerNames = config.ShowPlayerNames;
        if (ImGui.Checkbox("Show Player Names", ref showPlayerNames))
        {
            config.ShowPlayerNames = showPlayerNames;
            configManager.SaveConfiguration();
        }

        var showCountdownTimer = config.ShowCountdownTimer;
        if (ImGui.Checkbox("Show Countdown Timer", ref showCountdownTimer))
        {
            config.ShowCountdownTimer = showCountdownTimer;
            configManager.SaveConfiguration();
        }

        ImGui.Spacing();
        ImGui.Text("Overlay Settings");
        ImGui.Separator();

        var overlayOpacity = config.OverlayOpacity;
        if (ImGui.SliderFloat("Opacity", ref overlayOpacity, 0.1f, 1.0f))
        {
            config.OverlayOpacity = overlayOpacity;
            configManager.SaveConfiguration();
        }

        var lockOverlayPosition = config.LockOverlayPosition;
        if (ImGui.Checkbox("Lock Overlay Position", ref lockOverlayPosition))
        {
            config.LockOverlayPosition = lockOverlayPosition;
            configManager.SaveConfiguration();
        }

        ImGui.Spacing();
        ImGui.Text("Timeline Settings");
        ImGui.Separator();

        var lookAheadSeconds = config.LookAheadSeconds;
        if (ImGui.SliderFloat("Look Ahead (seconds)", ref lookAheadSeconds, 5f, 30f))
        {
            config.LookAheadSeconds = lookAheadSeconds;
            configManager.SaveConfiguration();
        }

        var actionDisplayDuration = config.ActionDisplayDuration;
        if (ImGui.SliderFloat("Action Display Duration", ref actionDisplayDuration, 1f, 10f))
        {
            config.ActionDisplayDuration = actionDisplayDuration;
            configManager.SaveConfiguration();
        }

        var autoStartOnCountdown = config.AutoStartOnCountdown;
        if (ImGui.Checkbox("Auto-start on Countdown", ref autoStartOnCountdown))
        {
            config.AutoStartOnCountdown = autoStartOnCountdown;
            configManager.SaveConfiguration();
        }
    }

    private void DrawTimelinesTab(FlowlineConfiguration config)
    {
        ImGui.Text("Configured Timelines");
        ImGui.Separator();

        if (ImGui.Button("Create New Timeline"))
        {
            editorWindow.CreateNewTimeline();
            editorWindow.IsOpen = true;
        }

        ImGui.SameLine();
        if (ImGui.Button("Import Timeline"))
        {
            // TODO: File picker for importing
            ImGui.OpenPopup("ImportNotImplemented");
        }

        if (ImGui.BeginPopup("ImportNotImplemented"))
        {
            ImGui.Text("File picker not yet implemented.");
            ImGui.Text("Place JSON files in the timelines directory.");
            ImGui.EndPopup();
        }

        ImGui.Spacing();

        // List existing timelines
        var timelines = configManager.Timelines.Values.ToList();

        if (timelines.Count == 0)
        {
            ImGui.TextColored(new Vector4(0.7f, 0.7f, 0.7f, 1), "No timelines configured yet.");
            return;
        }

        ImGui.BeginChild("TimelineList", new Vector2(0, 0), true);

        foreach (var timeline in timelines)
        {
            var dutyName = dutyDataService.GetDutyName(timeline.TerritoryId);

            ImGui.PushID(timeline.Id.ToString());

            // Icon buttons at the start of the row
            if (ImGuiComponents.IconButton(FontAwesomeIcon.Edit))
            {
                editorWindow.LoadTimeline(timeline);
                editorWindow.IsOpen = true;
            }
            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip("Edit");
            }

            ImGui.SameLine();
            if (ImGuiComponents.IconButton(FontAwesomeIcon.FileExport))
            {
                // TODO: File picker for exporting
            }
            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip("Export");
            }

            ImGui.SameLine();
            if (ImGuiComponents.IconButton(FontAwesomeIcon.Trash))
            {
                ImGui.OpenPopup("DeleteConfirm");
            }
            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip("Delete");
            }

            ImGui.SameLine();
            ImGui.Text($"{timeline.Name} - {dutyName} ({timeline.DurationSeconds:F0}s, {timeline.Markers.Count} markers)");

            if (ImGui.BeginPopup("DeleteConfirm"))
            {
                ImGui.Text($"Delete timeline '{timeline.Name}'?");
                if (ImGui.Button("Yes"))
                {
                    configManager.DeleteTimeline(timeline.Id);
                    ImGui.CloseCurrentPopup();
                }
                ImGui.SameLine();
                if (ImGui.Button("No"))
                {
                    ImGui.CloseCurrentPopup();
                }
                ImGui.EndPopup();
            }

            ImGui.PopID();
        }

        ImGui.EndChild();
    }

    private void DrawRecordingSettings(FlowlineConfiguration config)
    {
        ImGui.Text("Recording Mode");
        ImGui.Separator();

        var recordingMode = (int)config.RecordingMode;
        if (ImGui.Combo("##RecordingMode", ref recordingMode, "Manual\0Automatic\0Both\0"))
        {
            config.RecordingMode = (RecordingMode)recordingMode;
            configManager.SaveConfiguration();
        }

        ImGui.Spacing();

        var recordPartyActions = config.RecordPartyActions;
        if (ImGui.Checkbox("Record Party Actions", ref recordPartyActions))
        {
            config.RecordPartyActions = recordPartyActions;
            configManager.SaveConfiguration();
        }

        var autoRecordInConfiguredDuties = config.AutoRecordInConfiguredDuties;
        if (ImGui.Checkbox("Auto-record in Configured Duties", ref autoRecordInConfiguredDuties))
        {
            config.AutoRecordInConfiguredDuties = autoRecordInConfiguredDuties;
            configManager.SaveConfiguration();
        }

        ImGui.Spacing();
        ImGui.Text("Recording Management");
        ImGui.Separator();

        var maxRecordingsPerDuty = config.MaxRecordingsPerDuty;
        if (ImGui.SliderInt("Max Recordings Per Duty", ref maxRecordingsPerDuty, 0, 50))
        {
            config.MaxRecordingsPerDuty = maxRecordingsPerDuty;
            configManager.SaveConfiguration();
        }

        ImGui.TextColored(new Vector4(0.7f, 0.7f, 0.7f, 1), "Set to 0 to keep all recordings.");
    }

    private void DrawAdvancedSettings(FlowlineConfiguration config)
    {
        ImGui.Text("Debug Options");
        ImGui.Separator();

        var debugMode = config.DebugMode;
        if (ImGui.Checkbox("Debug Mode", ref debugMode))
        {
            config.DebugMode = debugMode;
            configManager.SaveConfiguration();
        }

        var showDebugOverlay = config.ShowDebugOverlay;
        if (ImGui.Checkbox("Show Debug Overlay", ref showDebugOverlay))
        {
            config.ShowDebugOverlay = showDebugOverlay;
            configManager.SaveConfiguration();
        }

        ImGui.Spacing();
        ImGui.Text("Plugin Information");
        ImGui.Separator();

        ImGui.TextColored(new Vector4(0.7f, 0.7f, 0.7f, 1), "Flowline v1.0.0");
        ImGui.TextColored(new Vector4(0.7f, 0.7f, 0.7f, 1), "A timeline overlay for FFXIV encounters");
    }

    public void Dispose()
    {
        // Cleanup if needed
    }
}
