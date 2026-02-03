using System;
using System.Linq;
using System.Numerics;
using Dalamud.Interface;
using Dalamud.Interface.Components;
using Dalamud.Interface.ImGuiNotification;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin.Services;
using Flowline.Configuration;
using Flowline.Data;
using Dalamud.Bindings.ImGui;
using Newtonsoft.Json;

namespace Flowline.UI;

/// <summary>
/// Main configuration window for Flowline settings.
/// </summary>
public class ConfigurationWindow : Window
{
    private readonly ConfigurationManager configManager;
    private readonly DutyDataService dutyDataService;
    private readonly TimelineEditorWindow editorWindow;
    private INotificationManager? notificationManager;
    private FFLogsImportWindow? fflogsImportWindow;

    // Import popup state
    private bool openImportPopup = false;
    private string importJsonText = string.Empty;
    private string importErrorMessage = string.Empty;

    // Export feedback
    private string exportFeedbackMessage = string.Empty;
    private DateTime exportFeedbackTime = DateTime.MinValue;

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

    public void SetNotificationManager(INotificationManager notificationManager)
    {
        this.notificationManager = notificationManager;
    }

    public void SetFFLogsImportWindow(FFLogsImportWindow fflogsImportWindow)
    {
        this.fflogsImportWindow = fflogsImportWindow;
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

        // Draw import popup if open
        DrawImportPopup();
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
        if (ImGui.Button("Import from Clipboard"))
        {
            openImportPopup = true;
            importJsonText = string.Empty;
            importErrorMessage = string.Empty;
        }

        ImGui.SameLine();
        if (ImGui.Button("Import from FFLogs"))
        {
            if (fflogsImportWindow != null)
            {
                fflogsImportWindow.IsOpen = true;
            }
        }

        // Show export feedback message
        if (!string.IsNullOrEmpty(exportFeedbackMessage) && (DateTime.Now - exportFeedbackTime).TotalSeconds < 3)
        {
            ImGui.SameLine();
            ImGui.TextColored(new Vector4(0.2f, 0.8f, 0.2f, 1), exportFeedbackMessage);
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
                ExportTimelineToClipboard(timeline);
            }
            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip("Export to Clipboard");
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

    private void ExportTimelineToClipboard(Timeline timeline)
    {
        try
        {
            var json = JsonConvert.SerializeObject(timeline, Formatting.Indented);
            ImGui.SetClipboardText(json);

            // Show feedback in window
            exportFeedbackMessage = $"Exported '{timeline.Name}' to clipboard!";
            exportFeedbackTime = DateTime.Now;

            // Show Dalamud notification
            if (notificationManager != null)
            {
                notificationManager.AddNotification(new Notification
                {
                    Title = "Flowline",
                    Content = $"Timeline '{timeline.Name}' exported to clipboard!",
                    Type = NotificationType.Success,
                    Minimized = false,
                });
            }
        }
        catch (Exception ex)
        {
            exportFeedbackMessage = $"Export failed: {ex.Message}";
            exportFeedbackTime = DateTime.Now;

            if (notificationManager != null)
            {
                notificationManager.AddNotification(new Notification
                {
                    Title = "Flowline",
                    Content = $"Failed to export timeline: {ex.Message}",
                    Type = NotificationType.Error,
                    Minimized = false,
                });
            }
        }
    }

    private void DrawImportPopup()
    {
        // Open popup if requested (must be done at window level, not inside tabs)
        if (openImportPopup)
        {
            ImGui.OpenPopup("Import Timeline##ImportPopup");
            openImportPopup = false;
        }

        // Set popup size
        ImGui.SetNextWindowSize(new Vector2(520, 420), ImGuiCond.FirstUseEver);

        var popupOpen = true;
        if (ImGui.BeginPopupModal("Import Timeline##ImportPopup", ref popupOpen, ImGuiWindowFlags.None))
        {
            ImGui.Text("Paste your timeline JSON below, or click 'Paste from Clipboard':");
            ImGui.Spacing();

            // Paste from clipboard button
            if (ImGui.Button("Paste from Clipboard", new Vector2(150, 0)))
            {
                var clipboardText = ImGui.GetClipboardText();
                if (!string.IsNullOrEmpty(clipboardText))
                {
                    importJsonText = clipboardText;
                    importErrorMessage = string.Empty;
                }
                else
                {
                    importErrorMessage = "Clipboard is empty.";
                }
            }

            ImGui.SameLine();
            if (ImGui.Button("Clear", new Vector2(60, 0)))
            {
                importJsonText = string.Empty;
                importErrorMessage = string.Empty;
            }

            ImGui.Spacing();

            // Multiline text input for JSON
            ImGui.InputTextMultiline("##ImportJson", ref importJsonText, 100000, new Vector2(500, 250));

            ImGui.Spacing();

            // Show error message if any
            if (!string.IsNullOrEmpty(importErrorMessage))
            {
                ImGui.TextColored(new Vector4(1, 0.3f, 0.3f, 1), importErrorMessage);
                ImGui.Spacing();
            }

            // Buttons
            if (ImGui.Button("Save as New Timeline", new Vector2(200, 0)))
            {
                ImportTimelineFromJson();
            }

            ImGui.SameLine();
            if (ImGui.Button("Cancel", new Vector2(100, 0)))
            {
                ImGui.CloseCurrentPopup();
            }

            ImGui.EndPopup();
        }
    }

    private void ImportTimelineFromJson()
    {
        if (string.IsNullOrWhiteSpace(importJsonText))
        {
            importErrorMessage = "Please paste a timeline JSON first.";
            return;
        }

        try
        {
            var timeline = JsonConvert.DeserializeObject<Timeline>(importJsonText);

            if (timeline == null)
            {
                importErrorMessage = "Failed to parse JSON. Please check the format.";
                return;
            }

            // Generate new ID to avoid conflicts
            timeline.Id = Guid.NewGuid();

            // Save the timeline
            configManager.SaveTimeline(timeline);

            // Show success notification
            if (notificationManager != null)
            {
                notificationManager.AddNotification(new Notification
                {
                    Title = "Flowline",
                    Content = $"Timeline '{timeline.Name}' imported successfully!",
                    Type = NotificationType.Success,
                    Minimized = false,
                });
            }

            // Close popup
            importJsonText = string.Empty;
            importErrorMessage = string.Empty;
            ImGui.CloseCurrentPopup();
        }
        catch (JsonException ex)
        {
            importErrorMessage = $"Invalid JSON format: {ex.Message}";
        }
        catch (Exception ex)
        {
            importErrorMessage = $"Import failed: {ex.Message}";
        }
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

        ImGui.TextColored(new Vector4(0.7f, 0.7f, 0.7f, 1), "Flowline v1.2.0");
        ImGui.TextColored(new Vector4(0.7f, 0.7f, 0.7f, 1), "A timeline overlay for FFXIV encounters");
    }

    public void Dispose()
    {
        // Cleanup if needed
    }
}
