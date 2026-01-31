using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Interface;
using Dalamud.Interface.Components;
using Dalamud.Interface.Windowing;
using Dalamud.Interface.Textures;
using Dalamud.Plugin.Services;
using Flowline.Configuration;
using Flowline.Data;
using Dalamud.Bindings.ImGui;

namespace Flowline.UI;

/// <summary>
/// Window for creating and editing timelines with visual timeline scrubber.
/// </summary>
public class TimelineEditorWindow : Window
{
    private readonly ConfigurationManager configManager;
    private readonly DutyDataService dutyDataService;
    private readonly ActionDataService actionDataService;
    private ITextureProvider? textureProvider;

    private Timeline? editingTimeline;
    private string timelineName = "";
    private float timelineDuration = 600f;

    // Expansion-based duty selection
    private int selectedExpansionIndex = 0;
    private int selectedDutyIndex = 0;
    private readonly string[] expansionNames = { "ARR", "HW", "StB", "ShB", "EW", "DT", "Ults" };
    private Dictionary<string, List<DutyData>> dutiesByExpansion = new();

    // Timeline scrubber state
    private float scrubberPosition = 0f;
    private float timelineZoom = 1f;
    private float timelineScrollOffset = 0f;

    // Job selection
    private int selectedJobIndex = 0;
    private readonly byte[] jobIds = new byte[]
    {
        19, 21, 32, 37, // Tanks: PLD, WAR, DRK, GNB
        24, 28, 33, 40, // Healers: WHM, SCH, AST, SGE
        20, 22, 30, 34, 39, 41, // Melee: MNK, DRG, NIN, SAM, RPR, VPR
        23, 31, 38, // Phys Ranged: BRD, MCH, DNC
        25, 27, 35, 42 // Casters: BLM, SMN, RDM, PCT
    };
    private readonly string[] jobNames = new string[]
    {
        "Paladin", "Warrior", "Dark Knight", "Gunbreaker",
        "White Mage", "Scholar", "Astrologian", "Sage",
        "Monk", "Dragoon", "Ninja", "Samurai", "Reaper", "Viper",
        "Bard", "Machinist", "Dancer",
        "Black Mage", "Summoner", "Red Mage", "Pictomancer"
    };

    // Action palette
    private string actionSearchQuery = "";
    private string textMarkerInput = "";
    private string jumpToTimeInput = "";

    // Action list panel
    private bool showActionList = true;
    private int? editingMarkerIndex = null;
    private float editingTimestamp = 0f;
    private string editingText = "";

    // Undo/Redo stacks
    private readonly Stack<List<ActionMarker>> undoStack = new();
    private readonly Stack<List<ActionMarker>> redoStack = new();
    private const int MaxUndoStackSize = 50;

    public TimelineEditorWindow(
        ConfigurationManager configManager,
        DutyDataService dutyDataService,
        ActionDataService actionDataService)
        : base("Timeline Editor##FlowlineEditor")
    {
        this.configManager = configManager;
        this.dutyDataService = dutyDataService;
        this.actionDataService = actionDataService;

        Size = new Vector2(1000, 800);
        SizeCondition = ImGuiCond.FirstUseEver;

        InitializeDutyLists();
    }

    public void SetTextureProvider(ITextureProvider textureProvider)
    {
        this.textureProvider = textureProvider;
    }

    private void InitializeDutyLists()
    {
        // Initialize expansion dictionaries
        dutiesByExpansion = new Dictionary<string, List<DutyData>>
        {
            { "ARR", new List<DutyData>() },
            { "HW", new List<DutyData>() },
            { "StB", new List<DutyData>() },
            { "ShB", new List<DutyData>() },
            { "EW", new List<DutyData>() },
            { "DT", new List<DutyData>() },
            { "Ults", new List<DutyData>() }
        };

        // Filter to only high-end content
        var duties = dutyDataService.GetAllDuties()
            .Where(d => d.DutyName.Contains("(Extreme)") ||
                        d.DutyName.Contains("(Savage)") ||
                        d.DutyName.Contains("Ultimate") ||
                        d.DutyName.Contains("(Unreal)"))
            .OrderBy(d => d.DutyName)
            .ToArray();

        foreach (var duty in duties)
        {
            var expansion = CategorizeByExpansion(duty);
            if (dutiesByExpansion.ContainsKey(expansion))
            {
                dutiesByExpansion[expansion].Add(duty);
            }
        }
    }

    private string CategorizeByExpansion(DutyData duty)
    {
        var name = duty.DutyName;

        // Ultimates go to their own category regardless of expansion
        if (name.Contains("Ultimate"))
        {
            return "Ults";
        }

        // ARR (A Realm Reborn) - Binding Coil, early Extremes
        if (name.Contains("Binding Coil") ||
            name.Contains("The Minstrel's Ballad: Ultima's Bane") ||
            name.Contains("Garuda (Extreme)") ||
            name.Contains("Titan (Extreme)") ||
            name.Contains("Ifrit (Extreme)") ||
            name.Contains("Leviathan (Extreme)") ||
            name.Contains("Good King Moggle Mog XII (Extreme)") ||
            name.Contains("Ramuh (Extreme)") ||
            name.Contains("Shiva (Extreme)") ||
            name.Contains("Odin (Extreme)"))
        {
            return "ARR";
        }

        // Heavensward - Alexander, HW Extremes
        if (name.Contains("Alexander") ||
            name.Contains("Thok ast Thok (Extreme)") ||  // Ravana
            name.Contains("The Limitless Blue (Extreme)") ||  // Bismarck
            name.Contains("The Minstrel's Ballad: Thordan's Reign") ||
            name.Contains("The Minstrel's Ballad: Nidhogg's Rage") ||
            name.Contains("Containment Bay S1T7 (Extreme)") ||  // Sophia
            name.Contains("Containment Bay P1T6 (Extreme)") ||  // Sephirot
            name.Contains("Containment Bay Z1T9 (Extreme)"))  // Zurvan
        {
            return "HW";
        }

        // Stormblood - Omega, SB Extremes
        if (name.Contains("Omega") ||
            name.Contains("Sigmascape") ||
            name.Contains("Alphascape") ||
            name.Contains("The Pool of Tribute (Extreme)") ||  // Susano
            name.Contains("Emanation (Extreme)") ||  // Lakshmi
            name.Contains("The Minstrel's Ballad: Shinryu's Domain") ||
            name.Contains("The Jade Stoa (Extreme)") ||  // Byakko
            name.Contains("The Minstrel's Ballad: Tsukuyomi's Pain") ||
            name.Contains("Hells' Kier (Extreme)") ||  // Suzaku
            name.Contains("The Wreath of Snakes (Extreme)"))  // Seiryu
        {
            return "StB";
        }

        // Shadowbringers - Eden, ShB Extremes
        if (name.Contains("Eden") ||
            name.Contains("The Dancing Plague (Extreme)") ||  // Titania
            name.Contains("The Crown of the Immaculate (Extreme)") ||  // Innocence
            name.Contains("The Minstrel's Ballad: Hades's Elegy") ||
            name.Contains("Cinder Drift (Extreme)") ||  // Ruby Weapon
            name.Contains("Castrum Marinum (Extreme)") ||  // Emerald Weapon
            name.Contains("The Cloud Deck (Extreme)") ||  // Diamond Weapon
            name.Contains("The Seat of Sacrifice (Extreme)"))  // Warrior of Light
        {
            return "ShB";
        }

        // Endwalker - Pandaemonium, EW Extremes
        if (name.Contains("Pandaemonium") ||
            name.Contains("Anabaseios") ||
            name.Contains("Abyssos") ||
            name.Contains("Asphodelos") ||
            name.Contains("The Minstrel's Ballad: Zodiark's Fall") ||
            name.Contains("The Minstrel's Ballad: Hydaelyn's Call") ||
            name.Contains("Storm's Crown (Extreme)") ||  // Barbariccia
            name.Contains("Mount Ordeals (Extreme)") ||  // Rubicante
            name.Contains("The Voidcast Dais (Extreme)") ||  // Golbez
            name.Contains("The Abyssal Fracture (Extreme)"))  // Zeromus
        {
            return "EW";
        }

        // Dawntrail - Newest content
        if (name.Contains("AAC Light-heavyweight") ||
            name.Contains("Worqor Zormor (Extreme)") ||  // Valigarmanda
            name.Contains("The Interphos (Extreme)") ||
            name.Contains("Everkeep (Extreme)") ||
            name.Contains("Jeuno: The First Walk") ||
            duty.TerritoryId >= 1200)  // DT content has higher territory IDs
        {
            return "DT";
        }

        // Default to DT for unknown high-end content (likely newest)
        return "DT";
    }

    public void CreateNewTimeline()
    {
        editingTimeline = new Timeline
        {
            Name = "New Timeline",
            DurationSeconds = 600f
        };
        timelineName = editingTimeline.Name;
        timelineDuration = editingTimeline.DurationSeconds;
        selectedExpansionIndex = 0;
        selectedDutyIndex = 0;
        scrubberPosition = 0f;
        timelineScrollOffset = 0f;
        editingMarkerIndex = null;
        undoStack.Clear();
        redoStack.Clear();
        IsOpen = true;
    }

    public void LoadTimeline(Timeline timeline)
    {
        editingTimeline = timeline;
        timelineName = timeline.Name;
        timelineDuration = timeline.DurationSeconds;

        // Find the expansion and duty index for this territory
        selectedExpansionIndex = 0;
        selectedDutyIndex = 0;
        for (int i = 0; i < expansionNames.Length; i++)
        {
            var expansion = expansionNames[i];
            if (dutiesByExpansion.ContainsKey(expansion))
            {
                var duties = dutiesByExpansion[expansion];
                for (int j = 0; j < duties.Count; j++)
                {
                    if (duties[j].TerritoryId == timeline.TerritoryId)
                    {
                        selectedExpansionIndex = i;
                        selectedDutyIndex = j;
                        break;
                    }
                }
            }
        }

        scrubberPosition = 0f;
        timelineScrollOffset = 0f;
        editingMarkerIndex = null;
        undoStack.Clear();
        redoStack.Clear();
    }

    public override void Draw()
    {
        if (editingTimeline == null)
        {
            ImGui.Text("No timeline loaded.");
            if (ImGui.Button("Create New Timeline"))
            {
                CreateNewTimeline();
            }
            return;
        }

        // Top section: Timeline info
        DrawTimelineInfo();

        ImGui.Separator();
        ImGui.Spacing();

        // Middle section: Visual timeline scrubber
        DrawTimelineScrubber();

        ImGui.Separator();
        ImGui.Spacing();

        // Split bottom area: Action palette and Action list
        var availableHeight = ImGui.GetContentRegionAvail().Y - 35;
        var paletteHeight = showActionList ? availableHeight * 0.5f : availableHeight;

        // Action palette
        ImGui.BeginChild("ActionPaletteSection", new Vector2(0, paletteHeight), false);
        DrawActionPalette();
        ImGui.EndChild();

        // Action list panel (retractable)
        if (showActionList)
        {
            ImGui.Separator();
            ImGui.BeginChild("ActionListSection", new Vector2(0, availableHeight - paletteHeight - 5), false);
            DrawActionList();
            ImGui.EndChild();
        }

        ImGui.Spacing();

        // Save/Cancel buttons
        DrawBottomButtons();
    }

    private void DrawTimelineInfo()
    {
        ImGui.Columns(2, "TimelineInfoColumns", false);

        // Left column
        ImGui.SetColumnWidth(0, 450);
        ImGui.InputText("Name", ref timelineName, 100);
        editingTimeline!.Name = timelineName;

        // Expansion dropdown
        ImGui.SetNextItemWidth(80);
        if (ImGui.Combo("##Expansion", ref selectedExpansionIndex, expansionNames, expansionNames.Length))
        {
            // Reset duty selection when expansion changes
            selectedDutyIndex = 0;
        }

        ImGui.SameLine();

        // Duty dropdown for selected expansion
        var currentExpansion = expansionNames[selectedExpansionIndex];
        var dutiesInExpansion = dutiesByExpansion.ContainsKey(currentExpansion)
            ? dutiesByExpansion[currentExpansion]
            : new List<DutyData>();

        var dutyNames = dutiesInExpansion.Select(d => d.DutyName).ToArray();
        if (dutyNames.Length == 0)
        {
            dutyNames = new[] { "No duties found" };
        }

        ImGui.SetNextItemWidth(300);
        if (ImGui.Combo("Duty", ref selectedDutyIndex, dutyNames, dutyNames.Length))
        {
            if (selectedDutyIndex >= 0 && selectedDutyIndex < dutiesInExpansion.Count)
            {
                editingTimeline.TerritoryId = dutiesInExpansion[selectedDutyIndex].TerritoryId;
            }
        }

        ImGui.NextColumn();

        // Right column
        if (ImGui.InputFloat("Duration (sec)", ref timelineDuration, 30f, 60f))
        {
            timelineDuration = Math.Max(30f, timelineDuration);
            editingTimeline.DurationSeconds = timelineDuration;
        }

        ImGui.Text($"Markers: {editingTimeline.Markers.Count}");

        ImGui.Columns(1);
    }

    private void DrawTimelineScrubber()
    {
        ImGui.Text("Timeline (double-click action to place at arrow, CTRL+click marker to remove)");

        var drawList = ImGui.GetWindowDrawList();
        var startPos = ImGui.GetCursorScreenPos();
        var availableWidth = ImGui.GetContentRegionAvail().X;
        var scrubberHeight = 150f;

        // Draw scrubber background
        var bgEnd = startPos + new Vector2(availableWidth, scrubberHeight);
        drawList.AddRectFilled(startPos, bgEnd, ImGui.GetColorU32(new Vector4(0.15f, 0.15f, 0.15f, 1f)));
        drawList.AddRect(startPos, bgEnd, ImGui.GetColorU32(new Vector4(0.4f, 0.4f, 0.4f, 1f)));

        // Calculate visible time range
        var pixelsPerSecond = availableWidth / (timelineDuration / timelineZoom);

        // Draw time markers (only 30s and even minutes)
        for (float time = 0; time <= timelineDuration; time += 30f)
        {
            var screenX = startPos.X + (time - timelineScrollOffset) * pixelsPerSecond;
            if (screenX < startPos.X || screenX > bgEnd.X) continue;

            Vector4 color;
            float height;
            float thickness;

            if (time % 120 == 0) // Green every 2 minutes
            {
                color = new Vector4(0, 1, 0, 0.8f);
                height = scrubberHeight;
                thickness = 2.5f;
            }
            else if (time % 60 == 0) // Blue every minute
            {
                color = new Vector4(0.3f, 0.6f, 1, 0.7f);
                height = scrubberHeight * 0.85f;
                thickness = 2.0f;
            }
            else // White every 30 seconds
            {
                color = new Vector4(1, 1, 1, 0.3f);
                height = scrubberHeight * 0.5f;
                thickness = 1.5f;
            }

            drawList.AddLine(
                new Vector2(screenX, startPos.Y),
                new Vector2(screenX, startPos.Y + height),
                ImGui.GetColorU32(color),
                thickness
            );

            // Time label
            var minutes = (int)(time / 60);
            var seconds = (int)(time % 60);
            var timeLabel = $"{minutes}:{seconds:D2}";
            var textSize = ImGui.CalcTextSize(timeLabel);
            drawList.AddText(
                new Vector2(screenX - textSize.X / 2, startPos.Y + scrubberHeight - 18),
                ImGui.GetColorU32(new Vector4(1, 1, 1, 0.8f)),
                timeLabel
            );
        }

        // Determine if zoomed in enough to show icons (zoom > 2)
        var showIcons = timelineZoom >= 2f;
        var iconSize = showIcons ? 32f : 0f;

        // Draw existing markers on timeline
        for (int i = 0; i < editingTimeline!.Markers.Count; i++)
        {
            var marker = editingTimeline.Markers[i];
            var screenX = startPos.X + (marker.TimestampSeconds - timelineScrollOffset) * pixelsPerSecond;
            if (screenX < startPos.X - 20 || screenX > bgEnd.X + 20) continue;

            var markerY = startPos.Y + 30;

            // Draw duration line if action has duration
            if (marker.DurationSeconds > 0)
            {
                var endX = startPos.X + (marker.TimestampSeconds + marker.DurationSeconds - timelineScrollOffset) * pixelsPerSecond;
                var durationColor = marker.DurationColor != 0 ? marker.DurationColor : 0x80FFB050;
                drawList.AddLine(
                    new Vector2(screenX, markerY + 8),
                    new Vector2(Math.Min(endX, bgEnd.X), markerY + 8),
                    durationColor,
                    4f
                );
            }

            // Handle text markers differently
            if (marker.IsTextMarker)
            {
                // Draw text label left-aligned
                var textColor = ImGui.GetColorU32(new Vector4(1f, 1f, 0.5f, 1f));
                drawList.AddText(
                    new Vector2(screenX + 5, markerY),
                    textColor,
                    marker.CustomLabel
                );
            }
            else if (showIcons && textureProvider != null)
            {
                // Draw action icon when zoomed in
                try
                {
                    var iconId = marker.IconId != 0 ? marker.IconId : actionDataService.GetActionIconId(marker.ActionId);
                    if (iconId != 0)
                    {
                        var texture = textureProvider.GetFromGameIcon(new GameIconLookup { IconId = iconId, HiRes = false });
                        var wrap = texture?.GetWrapOrDefault();
                        if (wrap != null)
                        {
                            var iconPos = new Vector2(screenX - iconSize / 2, markerY);
                            drawList.AddImage(wrap.Handle, iconPos, iconPos + new Vector2(iconSize, iconSize));
                        }
                    }
                }
                catch { }
            }
            else
            {
                // Draw simple marker dot
                var markerColor = ImGui.GetColorU32(new Vector4(1f, 0.7f, 0.2f, 1f));
                drawList.AddCircleFilled(new Vector2(screenX, markerY + 8), 6f, markerColor);
            }

            // Handle click on marker
            var hitBoxSize = showIcons ? iconSize : 12f;
            var hitBoxStart = new Vector2(screenX - hitBoxSize / 2, markerY);
            var hitBoxEnd = hitBoxStart + new Vector2(hitBoxSize, hitBoxSize);

            if (ImGui.IsMouseHoveringRect(hitBoxStart, hitBoxEnd))
            {
                // Tooltip
                ImGui.BeginTooltip();
                if (marker.IsTextMarker)
                {
                    ImGui.Text($"Text: {marker.CustomLabel}");
                }
                else
                {
                    ImGui.Text(actionDataService.GetActionName(marker.ActionId));
                }
                ImGui.Text($"Time: {marker.TimestampSeconds:F1}s");
                if (!string.IsNullOrEmpty(marker.PlayerName))
                    ImGui.Text($"Player: {marker.PlayerName}");
                ImGui.TextColored(new Vector4(0.7f, 0.7f, 0.7f, 1f), "CTRL+Click to delete");
                ImGui.TextColored(new Vector4(1f, 0.5f, 0.5f, 1f), "CTRL+Right-click to delete ALL of this action");
                ImGui.EndTooltip();

                // CTRL+Click to remove single marker
                if (ImGui.IsMouseClicked(ImGuiMouseButton.Left) && ImGui.GetIO().KeyCtrl)
                {
                    SaveStateForUndo();
                    editingTimeline.Markers.RemoveAt(i);
                    i--;
                }

                // CTRL+Right-click to mass delete all of this action
                if (ImGui.IsMouseClicked(ImGuiMouseButton.Right) && ImGui.GetIO().KeyCtrl)
                {
                    if (marker.IsTextMarker)
                    {
                        MassDeleteTextMarker(marker.CustomLabel);
                    }
                    else
                    {
                        MassDeleteAction(marker.ActionId);
                    }
                    break; // Break out since markers list has changed
                }
            }
        }

        // Draw scrubber arrow (current position)
        var arrowX = startPos.X + (scrubberPosition - timelineScrollOffset) * pixelsPerSecond;
        if (arrowX >= startPos.X && arrowX <= bgEnd.X)
        {
            var arrowTop = startPos.Y - 5;
            var arrowSize = 10f;
            drawList.AddTriangleFilled(
                new Vector2(arrowX, arrowTop + arrowSize),
                new Vector2(arrowX - arrowSize / 2, arrowTop),
                new Vector2(arrowX + arrowSize / 2, arrowTop),
                ImGui.GetColorU32(new Vector4(1, 0, 0, 1))
            );

            drawList.AddLine(
                new Vector2(arrowX, startPos.Y),
                new Vector2(arrowX, bgEnd.Y),
                ImGui.GetColorU32(new Vector4(1, 0, 0, 0.8f)),
                2f
            );
        }

        // Handle click on timeline to set scrubber position (snap to full seconds)
        if (ImGui.IsMouseHoveringRect(startPos, bgEnd) && ImGui.IsMouseClicked(ImGuiMouseButton.Left) && !ImGui.GetIO().KeyCtrl)
        {
            var mouseX = ImGui.GetMousePos().X;
            var relativeX = mouseX - startPos.X;
            var rawPosition = timelineScrollOffset + (relativeX / pixelsPerSecond);
            // Snap to full seconds
            scrubberPosition = (float)Math.Round(rawPosition);
            scrubberPosition = Math.Clamp(scrubberPosition, 0f, timelineDuration);
        }

        // Reserve space for the scrubber
        ImGui.Dummy(new Vector2(availableWidth, scrubberHeight));

        // Scrubber controls
        ImGui.SetNextItemWidth(200);
        var snappedPosition = (float)Math.Round(scrubberPosition);
        if (ImGui.SliderFloat("Position", ref snappedPosition, 0f, timelineDuration, "%.0f s"))
        {
            scrubberPosition = Math.Clamp(snappedPosition, 0f, timelineDuration);
        }

        ImGui.SameLine();
        ImGui.SetNextItemWidth(100);
        if (ImGui.SliderFloat("Zoom", ref timelineZoom, 0.5f, 4f, "%.1f"))
        {
            timelineZoom = Math.Clamp(timelineZoom, 0.5f, 4f);
        }

        ImGui.SameLine();
        ImGui.SetNextItemWidth(150);
        ImGui.SliderFloat("Scroll", ref timelineScrollOffset, 0f, Math.Max(0, timelineDuration - (timelineDuration / timelineZoom)), "%.0f s");

        // Jump to time input
        ImGui.SameLine();
        ImGui.SetNextItemWidth(80);
        ImGui.InputTextWithHint("##JumpToTime", "m:ss", ref jumpToTimeInput, 10);
        ImGui.SameLine();
        if (ImGui.Button("Go"))
        {
            JumpToTime();
        }
        if (ImGui.IsItemHovered())
        {
            ImGui.BeginTooltip();
            ImGui.Text("Jump to time (format: m:ss or seconds)");
            ImGui.EndTooltip();
        }

        // Text marker input (new line)
        ImGui.SetNextItemWidth(150);
        ImGui.InputTextWithHint("##TextMarker", "Text label...", ref textMarkerInput, 100);
        ImGui.SameLine();
        if (ImGui.Button("Add Text") && !string.IsNullOrWhiteSpace(textMarkerInput))
        {
            AddTextMarkerAtScrubberPosition(textMarkerInput);
            textMarkerInput = "";
        }
    }

    private void JumpToTime()
    {
        if (string.IsNullOrWhiteSpace(jumpToTimeInput)) return;

        float targetTime = 0f;

        // Try parsing as m:ss format
        if (jumpToTimeInput.Contains(':'))
        {
            var parts = jumpToTimeInput.Split(':');
            if (parts.Length == 2 &&
                int.TryParse(parts[0], out var minutes) &&
                int.TryParse(parts[1], out var seconds))
            {
                targetTime = minutes * 60 + seconds;
            }
        }
        else
        {
            // Try parsing as plain seconds
            float.TryParse(jumpToTimeInput, out targetTime);
        }

        // Clamp and set position
        targetTime = Math.Clamp(targetTime, 0f, timelineDuration);
        scrubberPosition = targetTime;

        // Also adjust scroll to make the position visible
        var visibleDuration = timelineDuration / timelineZoom;
        if (targetTime < timelineScrollOffset || targetTime > timelineScrollOffset + visibleDuration)
        {
            timelineScrollOffset = Math.Max(0, targetTime - visibleDuration / 2);
            timelineScrollOffset = Math.Min(timelineScrollOffset, timelineDuration - visibleDuration);
        }

        jumpToTimeInput = "";
    }

    private void DrawActionPalette()
    {
        ImGui.Text("Actions (double-click to add at arrow position)");

        // Job selector
        ImGui.SetNextItemWidth(150);
        if (ImGui.Combo("Job", ref selectedJobIndex, jobNames, jobNames.Length))
        {
            // Job changed
        }

        ImGui.SameLine();

        // Search box
        ImGui.SetNextItemWidth(200);
        ImGui.InputTextWithHint("##ActionSearch", "Search actions...", ref actionSearchQuery, 256);

        ImGui.Spacing();

        // Action grid
        ImGui.BeginChild("ActionPaletteGrid", new Vector2(0, 0), true);

        var currentJobId = selectedJobIndex < jobIds.Length ? jobIds[selectedJobIndex] : (byte)0;
        var actions = string.IsNullOrWhiteSpace(actionSearchQuery)
            ? actionDataService.SearchActions("", currentJobId)
            : actionDataService.SearchActions(actionSearchQuery, currentJobId);

        const float iconSize = 40f;
        const float padding = 5f;
        var availableWidth = ImGui.GetContentRegionAvail().X;
        var iconsPerRow = (int)((availableWidth + padding) / (iconSize + padding));
        if (iconsPerRow < 1) iconsPerRow = 1;

        var actionsList = new List<ActionData>(actions);
        for (int i = 0; i < actionsList.Count && i < 100; i++)
        {
            var action = actionsList[i];

            if (i % iconsPerRow != 0)
                ImGui.SameLine();

            DrawActionIcon(action, iconSize);
        }

        ImGui.EndChild();
    }

    private void DrawActionIcon(ActionData action, float size)
    {
        var iconId = action.IconId;
        if (iconId == 0)
        {
            ImGui.Dummy(new Vector2(size, size));
            return;
        }

        try
        {
            if (textureProvider != null)
            {
                var texture = textureProvider.GetFromGameIcon(new GameIconLookup { IconId = iconId, HiRes = false });
                var wrap = texture?.GetWrapOrDefault();

                if (wrap != null)
                {
                    ImGui.Image(wrap.Handle, new Vector2(size, size));

                    if (ImGui.IsItemHovered() && ImGui.IsMouseDoubleClicked(ImGuiMouseButton.Left))
                    {
                        AddActionAtScrubberPosition(action);
                    }

                    if (ImGui.IsItemHovered())
                    {
                        ImGui.BeginTooltip();
                        ImGui.Text(action.Name);
                        ImGui.Text("Double-click to add at arrow position");
                        ImGui.EndTooltip();
                    }

                    return;
                }
            }
        }
        catch
        {
            // Fallback
        }

        // Fallback: button with action name
        ImGui.PushID((int)action.ActionId);
        if (ImGui.Button(action.Name.Length > 6 ? action.Name.Substring(0, 6) : action.Name, new Vector2(size, size)))
        {
            // Single click fallback
        }
        if (ImGui.IsItemHovered() && ImGui.IsMouseDoubleClicked(ImGuiMouseButton.Left))
        {
            AddActionAtScrubberPosition(action);
        }
        if (ImGui.IsItemHovered())
        {
            ImGui.BeginTooltip();
            ImGui.Text(action.Name);
            ImGui.Text("Double-click to add at arrow position");
            ImGui.EndTooltip();
        }
        ImGui.PopID();
    }

    private void DrawActionList()
    {
        // Header with toggle icon and text
        var toggleIcon = showActionList ? FontAwesomeIcon.ChevronDown : FontAwesomeIcon.ChevronRight;
        if (ImGuiComponents.IconButton(toggleIcon))
        {
            showActionList = !showActionList;
        }
        ImGui.SameLine();
        ImGui.Text("Action List");
        if (!showActionList) return;

        ImGui.SameLine();
        ImGui.Text($"({editingTimeline?.Markers.Count ?? 0} markers)");

        ImGui.BeginChild("ActionListContent", new Vector2(0, 0), true);

        if (editingTimeline == null || editingTimeline.Markers.Count == 0)
        {
            ImGui.TextColored(new Vector4(0.7f, 0.7f, 0.7f, 1f), "No markers yet. Double-click actions to add them.");
            ImGui.EndChild();
            return;
        }

        // Table header
        ImGui.Columns(4, "ActionListColumns", true);
        ImGui.SetColumnWidth(0, 60);
        ImGui.SetColumnWidth(1, 80);
        ImGui.SetColumnWidth(2, 200);
        ImGui.Text("Actions");
        ImGui.NextColumn();
        ImGui.Text("Time");
        ImGui.NextColumn();
        ImGui.Text("Name");
        ImGui.NextColumn();
        ImGui.Text("Text");
        ImGui.NextColumn();
        ImGui.Separator();

        for (int i = 0; i < editingTimeline.Markers.Count; i++)
        {
            var marker = editingTimeline.Markers[i];
            ImGui.PushID(i);

            // Edit/Delete buttons using FontAwesome icons
            if (ImGuiComponents.IconButton(FontAwesomeIcon.Edit))
            {
                editingMarkerIndex = i;
                editingTimestamp = marker.TimestampSeconds;
                editingText = marker.IsTextMarker ? marker.CustomLabel : marker.PlayerName;
                ImGui.OpenPopup("EditMarkerPopup");
            }
            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip("Edit");
            }

            ImGui.SameLine();
            if (ImGuiComponents.IconButton(FontAwesomeIcon.Trash))
            {
                SaveStateForUndo();
                editingTimeline.Markers.RemoveAt(i);
                i--;
                ImGui.PopID();
                continue;
            }
            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip("Delete");
            }

            // Edit popup
            if (ImGui.BeginPopup("EditMarkerPopup"))
            {
                ImGui.Text("Edit Marker");
                ImGui.Separator();

                ImGui.SetNextItemWidth(100);
                if (ImGui.InputFloat("Timestamp", ref editingTimestamp, 1f, 5f, "%.0f s"))
                {
                    editingTimestamp = Math.Clamp(editingTimestamp, 0f, timelineDuration);
                }

                ImGui.SetNextItemWidth(200);
                ImGui.InputText("Text", ref editingText, 100);

                if (ImGui.Button("Save"))
                {
                    if (editingMarkerIndex.HasValue && editingMarkerIndex.Value < editingTimeline.Markers.Count)
                    {
                        var editedMarker = editingTimeline.Markers[editingMarkerIndex.Value];
                        editedMarker.TimestampSeconds = editingTimestamp;
                        if (editedMarker.IsTextMarker)
                        {
                            editedMarker.CustomLabel = editingText;
                        }
                        else
                        {
                            editedMarker.PlayerName = editingText;
                        }
                        editingTimeline.Markers = editingTimeline.Markers.OrderBy(m => m.TimestampSeconds).ToList();
                    }
                    editingMarkerIndex = null;
                    ImGui.CloseCurrentPopup();
                }
                ImGui.SameLine();
                if (ImGui.Button("Cancel"))
                {
                    editingMarkerIndex = null;
                    ImGui.CloseCurrentPopup();
                }
                ImGui.EndPopup();
            }

            ImGui.NextColumn();

            // Timestamp
            var minutes = (int)(marker.TimestampSeconds / 60);
            var seconds = (int)(marker.TimestampSeconds % 60);
            ImGui.Text($"{minutes}:{seconds:D2}");
            ImGui.NextColumn();

            // Name
            if (marker.IsTextMarker)
            {
                ImGui.TextColored(new Vector4(1f, 1f, 0.5f, 1f), $"[Text] {marker.CustomLabel}");
            }
            else
            {
                ImGui.Text(actionDataService.GetActionName(marker.ActionId));
            }
            ImGui.NextColumn();

            // Text (shows CustomLabel for text markers, PlayerName for actions)
            var displayText = marker.IsTextMarker ? marker.CustomLabel : marker.PlayerName;
            ImGui.Text(displayText);
            ImGui.NextColumn();

            ImGui.PopID();
        }

        ImGui.Columns(1);
        ImGui.EndChild();
    }

    private void AddActionAtScrubberPosition(ActionData action)
    {
        if (editingTimeline == null) return;

        SaveStateForUndo();

        // Snap to full seconds
        var snappedPosition = (float)Math.Round(scrubberPosition);

        // Get duration from action data service
        var duration = actionDataService.GetActionDuration(action.ActionId);

        var marker = new ActionMarker
        {
            Type = MarkerType.Action,
            TimestampSeconds = snappedPosition,
            ActionId = action.ActionId,
            PlayerName = "",
            IconId = action.IconId,
            DurationSeconds = duration
        };

        // Set duration color based on icon
        if (duration > 0)
        {
            marker.DurationColor = 0x80FFB050; // Orange-ish tint
        }

        editingTimeline.Markers.Add(marker);
        editingTimeline.Markers = editingTimeline.Markers.OrderBy(m => m.TimestampSeconds).ToList();
    }

    private void AddTextMarkerAtScrubberPosition(string text)
    {
        if (editingTimeline == null) return;

        SaveStateForUndo();

        var snappedPosition = (float)Math.Round(scrubberPosition);

        var marker = ActionMarker.CreateTextLabel(snappedPosition, text);
        editingTimeline.Markers.Add(marker);
        editingTimeline.Markers = editingTimeline.Markers.OrderBy(m => m.TimestampSeconds).ToList();
    }

    private void SaveStateForUndo()
    {
        if (editingTimeline == null) return;

        // Deep copy current markers
        var markersCopy = editingTimeline.Markers.Select(m => new ActionMarker
        {
            Type = m.Type,
            TimestampSeconds = m.TimestampSeconds,
            ActionId = m.ActionId,
            PlayerName = m.PlayerName,
            JobId = m.JobId,
            IconId = m.IconId,
            CustomLabel = m.CustomLabel,
            DurationSeconds = m.DurationSeconds,
            DurationColor = m.DurationColor
        }).ToList();

        undoStack.Push(markersCopy);
        redoStack.Clear(); // Clear redo stack on new action

        // Limit stack size
        while (undoStack.Count > MaxUndoStackSize)
        {
            var temp = new Stack<List<ActionMarker>>();
            while (undoStack.Count > 1)
            {
                temp.Push(undoStack.Pop());
            }
            undoStack.Pop(); // Remove oldest
            while (temp.Count > 0)
            {
                undoStack.Push(temp.Pop());
            }
        }
    }

    private void Undo()
    {
        if (editingTimeline == null || undoStack.Count == 0) return;

        // Save current state to redo stack
        var currentState = editingTimeline.Markers.Select(m => new ActionMarker
        {
            Type = m.Type,
            TimestampSeconds = m.TimestampSeconds,
            ActionId = m.ActionId,
            PlayerName = m.PlayerName,
            JobId = m.JobId,
            IconId = m.IconId,
            CustomLabel = m.CustomLabel,
            DurationSeconds = m.DurationSeconds,
            DurationColor = m.DurationColor
        }).ToList();
        redoStack.Push(currentState);

        // Restore previous state
        editingTimeline.Markers = undoStack.Pop();
    }

    private void Redo()
    {
        if (editingTimeline == null || redoStack.Count == 0) return;

        // Save current state to undo stack
        var currentState = editingTimeline.Markers.Select(m => new ActionMarker
        {
            Type = m.Type,
            TimestampSeconds = m.TimestampSeconds,
            ActionId = m.ActionId,
            PlayerName = m.PlayerName,
            JobId = m.JobId,
            IconId = m.IconId,
            CustomLabel = m.CustomLabel,
            DurationSeconds = m.DurationSeconds,
            DurationColor = m.DurationColor
        }).ToList();
        undoStack.Push(currentState);

        // Restore redo state
        editingTimeline.Markers = redoStack.Pop();
    }

    private void MassDeleteAction(uint actionId)
    {
        if (editingTimeline == null) return;

        SaveStateForUndo();

        // Remove all markers with the same action ID
        editingTimeline.Markers = editingTimeline.Markers
            .Where(m => m.ActionId != actionId || m.IsTextMarker)
            .ToList();
    }

    private void MassDeleteTextMarker(string customLabel)
    {
        if (editingTimeline == null) return;

        SaveStateForUndo();

        // Remove all text markers with the same label
        editingTimeline.Markers = editingTimeline.Markers
            .Where(m => !m.IsTextMarker || m.CustomLabel != customLabel)
            .ToList();
    }

    private void DrawBottomButtons()
    {
        // Undo button
        var canUndo = undoStack.Count > 0;
        if (!canUndo) ImGui.BeginDisabled();
        if (ImGuiComponents.IconButton(FontAwesomeIcon.Undo))
        {
            Undo();
        }
        if (ImGui.IsItemHovered())
        {
            ImGui.SetTooltip($"Undo ({undoStack.Count})");
        }
        if (!canUndo) ImGui.EndDisabled();

        ImGui.SameLine();

        // Redo button
        var canRedo = redoStack.Count > 0;
        if (!canRedo) ImGui.BeginDisabled();
        if (ImGuiComponents.IconButton(FontAwesomeIcon.Redo))
        {
            Redo();
        }
        if (ImGui.IsItemHovered())
        {
            ImGui.SetTooltip($"Redo ({redoStack.Count})");
        }
        if (!canRedo) ImGui.EndDisabled();

        ImGui.SameLine();
        ImGui.Spacing();
        ImGui.SameLine();

        if (ImGui.Button("Save Timeline"))
        {
            configManager.SaveTimeline(editingTimeline!);
            IsOpen = false;
        }

        ImGui.SameLine();
        if (ImGui.Button("Clear All Markers"))
        {
            if (editingTimeline != null && editingTimeline.Markers.Count > 0)
            {
                SaveStateForUndo();
                editingTimeline.Markers.Clear();
            }
        }

        ImGui.SameLine();
        if (ImGui.Button(showActionList ? "Hide List" : "Show List"))
        {
            showActionList = !showActionList;
        }

        ImGui.SameLine();
        if (ImGui.Button("Cancel"))
        {
            IsOpen = false;
        }
    }

    public void Dispose()
    {
        // Cleanup if needed
    }
}
