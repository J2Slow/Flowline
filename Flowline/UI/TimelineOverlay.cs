using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Interface;
using Dalamud.Interface.Components;
using Dalamud.Interface.Textures;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin.Services;
using Flowline.Configuration;
using Flowline.Data;
using Flowline.Rendering;
using Flowline.Services;
using Dalamud.Bindings.ImGui;

namespace Flowline.UI;

/// <summary>
/// Main timeline overlay window.
/// </summary>
public class TimelineOverlay : Window, IDisposable
{
    private readonly TimelinePlaybackService playbackService;
    private readonly ActionDataService actionDataService;
    private readonly ConfigurationManager configManager;
    private readonly ICondition condition;
    private readonly ConfigurationWindow configWindow;
    private readonly IObjectTable objectTable;
    private readonly IClientState clientState;
    private readonly ActionRecorderService? recorderService;
    private CountdownService? countdownService;
    private DutyDataService? dutyDataService;

    private readonly HorizontalScrollRenderer horizontalRenderer;
    private readonly VerticalListRenderer verticalListRenderer;
    private readonly VerticalScrollRenderer verticalScrollRenderer;

    private bool wasInCombat = false;
    private float playbackTime = 0f;
    private bool isPlaying = false;
    private int selectedTimelineIndex = -1;
    private Timeline? selectedTimeline = null;
    private ITextureProvider textureProvider;

    // Recording mode popup
    private bool showRecordingPopup = false;

    public void SetRecorderService(ActionRecorderService recorderService)
    {
        // Allow setting recorder service after construction
    }

    public void SetDutyDataService(DutyDataService dutyDataService)
    {
        this.dutyDataService = dutyDataService;
    }

    public void SetCountdownService(CountdownService countdownService)
    {
        this.countdownService = countdownService;

        // Subscribe to countdown reaching zero - auto start timeline
        this.countdownService.CountdownReachedZero += OnCountdownReachedZero;
    }

    private void OnCountdownReachedZero()
    {
        // Auto-start timeline when countdown reaches 0
        if (selectedTimeline != null && !isPlaying)
        {
            playbackTime = 0f;
            isPlaying = true;
        }

        // Notify recorder
        recorderService?.OnCountdownReached();
    }

    public TimelineOverlay(
        TimelinePlaybackService playbackService,
        ActionDataService actionDataService,
        ConfigurationManager configManager,
        ITextureProvider textureProvider,
        ICondition condition,
        ConfigurationWindow configWindow,
        IObjectTable objectTable,
        IClientState? clientState = null,
        ActionRecorderService? recorderService = null)
        : base("Flowline##FlowlineOverlay", ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoCollapse)
    {
        this.playbackService = playbackService;
        this.actionDataService = actionDataService;
        this.configManager = configManager;
        this.condition = condition;
        this.configWindow = configWindow;
        this.textureProvider = textureProvider;
        this.objectTable = objectTable;
        this.clientState = clientState!;
        this.recorderService = recorderService;

        // Initialize renderers
        horizontalRenderer = new HorizontalScrollRenderer(textureProvider);
        verticalListRenderer = new VerticalListRenderer(textureProvider);
        verticalScrollRenderer = new VerticalScrollRenderer(textureProvider);

        // Window settings - use Dalamud's built-in pinning/opacity via burger menu
        Size = configManager.Configuration.OverlaySize;
        SizeCondition = ImGuiCond.FirstUseEver;
        Position = configManager.Configuration.OverlayPosition;
        PositionCondition = ImGuiCond.FirstUseEver;

        // Enable the burger menu with pinning and opacity controls
        AllowPinning = true;
        AllowClickthrough = true;
    }

    public override void Draw()
    {
        var config = configManager.Configuration;

        // Note: countdownService.Update() is called in Plugin.OnFrameworkUpdate
        // to ensure it runs every frame even when overlay is hidden

        // Update playback time if timeline is selected
        if (selectedTimeline != null)
        {
            UpdatePlayback();
        }

        // Draw titlebar controls (in same line as title)
        DrawTitlebarControls();

        ImGui.Separator();
        ImGui.Spacing();

        // Update position and size from window
        var windowPos = ImGui.GetWindowPos();
        var windowSize = ImGui.GetWindowSize();

        // Save position and size
        config.OverlayPosition = windowPos;
        config.OverlaySize = windowSize;

        // Timeline area
        var contentHeight = ImGui.GetContentRegionAvail().Y;

        ImGui.BeginChild("TimelineArea", new Vector2(0, contentHeight), false);
        {
            var markers = GetCurrentMarkers();
            var currentTime = playbackTime;

            var timelinePos = ImGui.GetCursorScreenPos();
            var timelineSize = ImGui.GetContentRegionAvail();

            var countdownRemaining = countdownService?.CountdownRemaining ?? 0f;

            if (selectedTimeline != null && (isPlaying || countdownRemaining > 0))
            {
                // Always render timeline when playing or during countdown (even with no markers)
                var renderer = GetRenderer(config.DisplayMode);
                renderer.Render(
                    markers,
                    currentTime,
                    config.LookAheadSeconds,
                    timelinePos,
                    timelineSize,
                    config,
                    actionDataService,
                    countdownRemaining
                );
            }
            else if (selectedTimeline != null)
            {
                ImGui.TextColored(new Vector4(0.7f, 0.7f, 0.7f, 1), $"Timeline: {selectedTimeline.Name}");
                ImGui.TextColored(new Vector4(0.7f, 0.7f, 0.7f, 1), "Press Play to start.");
            }
            else
            {
                ImGui.TextColored(new Vector4(0.7f, 0.7f, 0.7f, 1), "Select a timeline above to preview.");
            }

            // Debug overlay
            if (config.ShowDebugOverlay)
            {
                DrawDebugInfo(timelinePos, timelineSize);
            }
        }
        ImGui.EndChild();
    }

    private void DrawTitlebarControls()
    {
        var config = configManager.Configuration;
        var timelines = configManager.Timelines.Values.ToList();

        // Timeline selector dropdown
        ImGui.SetNextItemWidth(200);
        var timelineNames = new List<string> { "-- Select Timeline --" };
        timelineNames.AddRange(timelines.Select(t => t.Name));

        var currentIndex = selectedTimelineIndex + 1; // +1 for "Select" option
        if (ImGui.Combo("##TimelineSelect", ref currentIndex, timelineNames.ToArray(), timelineNames.Count))
        {
            selectedTimelineIndex = currentIndex - 1;
            if (selectedTimelineIndex >= 0 && selectedTimelineIndex < timelines.Count)
            {
                selectedTimeline = timelines[selectedTimelineIndex];
                playbackTime = 0f;
            }
            else
            {
                selectedTimeline = null;
            }
        }

        ImGui.SameLine();

        // Timer controls
        if (selectedTimeline != null)
        {
            if (isPlaying)
            {
                if (ImGuiComponents.IconButton(FontAwesomeIcon.Undo))
                {
                    playbackTime = 0f;
                    isPlaying = false;
                }
                if (ImGui.IsItemHovered())
                {
                    ImGui.SetTooltip("Reset");
                }
            }
            else
            {
                if (ImGuiComponents.IconButton(FontAwesomeIcon.Play))
                {
                    playbackTime = 0f;
                    isPlaying = true;
                }
                if (ImGui.IsItemHovered())
                {
                    ImGui.SetTooltip("Start");
                }
            }

            ImGui.SameLine();
            // Show time with proper formatting (negative for prepull)
            var displayTime = playbackTime;
            var timeColor = displayTime < 0 ? new Vector4(1f, 0.5f, 0.5f, 1f) : new Vector4(1f, 1f, 1f, 1f);
            ImGui.TextColored(timeColor, $"{displayTime:F1}s");
        }

        ImGui.SameLine();

        // Recording button
        var isRecording = recorderService?.IsRecording ?? false;
        if (isRecording)
        {
            // Show recording status indicator
            if (recorderService?.WaitingForStart ?? false)
            {
                ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.8f, 0.6f, 0.2f, 1f));
                ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(0.9f, 0.7f, 0.3f, 1f));
            }
            else
            {
                ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.8f, 0.2f, 0.2f, 1f));
                ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(0.9f, 0.3f, 0.3f, 1f));
            }
            if (ImGuiComponents.IconButton(FontAwesomeIcon.Stop))
            {
                recorderService?.StopRecording();
            }
            ImGui.PopStyleColor(2);
            if (ImGui.IsItemHovered())
            {
                var status = (recorderService?.WaitingForStart ?? false)
                    ? "Waiting for combat/countdown..."
                    : "Stop Recording";
                ImGui.SetTooltip(status);
            }
        }
        else
        {
            ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0.9f, 0.2f, 0.2f, 1f));
            if (ImGuiComponents.IconButton(FontAwesomeIcon.Circle))
            {
                showRecordingPopup = true;
                ImGui.OpenPopup("RecordingModePopup");
            }
            ImGui.PopStyleColor();
            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip("Start Recording (click for mode selection)");
            }
        }

        // Recording mode popup
        DrawRecordingModePopup();

        // Right-aligned settings button only (opacity/pin are in burger menu now)
        ImGui.SameLine();
        ImGui.SetCursorPosX(ImGui.GetWindowWidth() - 40);

        // Settings button
        if (ImGuiComponents.IconButton(FontAwesomeIcon.Cog))
        {
            configWindow.Toggle();
        }
        if (ImGui.IsItemHovered())
        {
            ImGui.SetTooltip("Settings");
        }
    }

    private void DrawRecordingModePopup()
    {
        if (ImGui.BeginPopup("RecordingModePopup"))
        {
            ImGui.Text("Select Recording Mode");
            ImGui.Separator();

            // Fresh mode
            if (ImGui.Selectable("Fresh Recording"))
            {
                StartFreshRecording();
                ImGui.CloseCurrentPopup();
            }
            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip("Start a new recording without any timeline.\nTimer starts when countdown reaches 0 or combat starts.");
            }

            // Sidebyside mode (only available if a timeline is selected)
            var canSidebyside = selectedTimeline != null;
            if (!canSidebyside)
            {
                ImGui.BeginDisabled();
            }

            if (ImGui.Selectable("Side-by-side Recording"))
            {
                StartSidebysideRecording();
                ImGui.CloseCurrentPopup();
            }
            if (ImGui.IsItemHovered())
            {
                if (canSidebyside)
                {
                    ImGui.SetTooltip($"Record alongside '{selectedTimeline!.Name}'.\nYour actions will be recorded while the timeline plays.");
                }
                else
                {
                    ImGui.SetTooltip("Select a timeline first to use side-by-side mode.");
                }
            }

            if (!canSidebyside)
            {
                ImGui.EndDisabled();
            }

            ImGui.EndPopup();
        }
    }

    private void StartFreshRecording()
    {
        if (recorderService == null) return;

        var territoryId = clientState?.TerritoryType ?? 0;
        var jobAbbrev = GetJobAbbreviation((byte)(objectTable?.LocalPlayer?.ClassJob.RowId ?? 0));
        var instanceName = dutyDataService?.GetDutyName(territoryId) ?? $"Zone{territoryId}";

        recorderService.StartFreshRecording(territoryId, jobAbbrev, instanceName);
    }

    private void StartSidebysideRecording()
    {
        if (recorderService == null || selectedTimeline == null) return;

        var territoryId = clientState?.TerritoryType ?? 0;
        var jobAbbrev = GetJobAbbreviation((byte)(objectTable?.LocalPlayer?.ClassJob.RowId ?? 0));
        var instanceName = dutyDataService?.GetDutyName(territoryId) ?? $"Zone{territoryId}";

        recorderService.StartSidebysideRecording(territoryId, jobAbbrev, instanceName, selectedTimeline.Name);

        // Also start timeline playback
        playbackTime = 0f;
        isPlaying = true;
    }

    private void StartRecording()
    {
        // Default to showing the popup
        showRecordingPopup = true;
        ImGui.OpenPopup("RecordingModePopup");
    }

    private string GetJobAbbreviation(byte jobId)
    {
        return jobId switch
        {
            1 => "GLA", 2 => "PGL", 3 => "MRD", 4 => "LNC", 5 => "ARC",
            6 => "CNJ", 7 => "THM", 19 => "PLD", 20 => "MNK", 21 => "WAR",
            22 => "DRG", 23 => "BRD", 24 => "WHM", 25 => "BLM", 26 => "ACN",
            27 => "SMN", 28 => "SCH", 30 => "NIN", 31 => "MCH", 32 => "DRK",
            33 => "AST", 34 => "SAM", 35 => "RDM", 36 => "BLU", 37 => "GNB",
            38 => "DNC", 39 => "RPR", 40 => "SGE", 41 => "VPR", 42 => "PCT",
            _ => "UNK"
        };
    }

    private ITimelineRenderer GetRenderer(TimelineDisplayMode mode)
    {
        return mode switch
        {
            TimelineDisplayMode.HorizontalScroll => horizontalRenderer,
            TimelineDisplayMode.VerticalList => verticalListRenderer,
            TimelineDisplayMode.VerticalScroll => verticalScrollRenderer,
            _ => horizontalRenderer
        };
    }

    private void UpdatePlayback()
    {
        var config = configManager.Configuration;

        // Check if entering combat
        var inCombat = condition[ConditionFlag.InCombat];

        if (inCombat && !wasInCombat)
        {
            // Just entered combat - start timeline
            playbackTime = 0f;
            isPlaying = true;

            // Notify recorder service about combat start
            recorderService?.OnCombatStart();
        }
        else if (!inCombat && wasInCombat)
        {
            // Combat ended - notify recorder service
            recorderService?.OnCombatEnd();
        }

        wasInCombat = inCombat;

        // Progress playback time when playing
        if (isPlaying)
        {
            playbackTime += ImGui.GetIO().DeltaTime;

            // Stop at end of timeline
            if (selectedTimeline != null && playbackTime > selectedTimeline.DurationSeconds)
            {
                isPlaying = false;
            }
        }
    }

    private List<ActionMarker> GetCurrentMarkers()
    {
        if (selectedTimeline == null || !isPlaying)
            return new List<ActionMarker>();

        var config = configManager.Configuration;
        var visibleMarkers = new List<ActionMarker>();

        foreach (var marker in selectedTimeline.Markers)
        {
            var timeUntil = marker.TimestampSeconds - playbackTime;
            // Show markers within the look-ahead window (including during prepull)
            if (timeUntil >= 0 && timeUntil <= config.LookAheadSeconds)
            {
                visibleMarkers.Add(marker);
            }
        }

        return visibleMarkers;
    }

    private void DrawDebugInfo(Vector2 position, Vector2 size)
    {
        var debugText = $"State: {playbackService.State}\n" +
                       $"Time: {playbackTime:F2}s\n" +
                       $"Timeline: {selectedTimeline?.Name ?? "None"}";

        ImGui.SetCursorPos(new Vector2(5, size.Y - 60));
        ImGui.TextColored(new Vector4(0, 1, 0, 1), debugText);
    }

    public void Dispose()
    {
        if (countdownService != null)
        {
            countdownService.CountdownReachedZero -= OnCountdownReachedZero;
        }
    }
}
