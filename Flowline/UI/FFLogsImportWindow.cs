using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Windowing;
using Dalamud.Bindings.ImGui;
using Flowline.Configuration;
using Flowline.Data;
using Flowline.Services;

namespace Flowline.UI;

/// <summary>
/// Multi-step wizard window for importing timelines from FFLogs.
/// </summary>
public class FFLogsImportWindow : Window, IDisposable
{
    private readonly FFLogsService fflogsService;
    private readonly FFLogsConverter converter;
    private readonly ConfigurationManager configManager;

    private enum ImportStep
    {
        Credentials,
        ReportEntry,
        FightSelection,
        PlayerSelection,
        Preview
    }

    private ImportStep currentStep = ImportStep.Credentials;

    // Credentials step
    private string clientIdInput = string.Empty;
    private string clientSecretInput = string.Empty;
    private string credentialError = string.Empty;
    private bool isTestingConnection = false;
    private string connectionTestResult = string.Empty;

    // Report entry step
    private string reportUrlInput = string.Empty;
    private string reportError = string.Empty;
    private bool isFetchingReport = false;
    private FFLogsReport? currentReport;

    // Fight selection step
    private int selectedFightIndex = -1;

    // Player selection step
    private int selectedPlayerIndex = -1;
    private FFLogsImportOptions importOptions = new();
    private List<FFLogsActor> playerActors = new();

    // Preview step
    private Timeline? previewTimeline;
    private List<FFLogsCastEvent>? fetchedEvents;
    private bool isFetchingEvents = false;
    private string eventsError = string.Empty;

    public FFLogsImportWindow(
        FFLogsService fflogsService,
        FFLogsConverter converter,
        ConfigurationManager configManager)
        : base("Import from FFLogs", ImGuiWindowFlags.NoCollapse)
    {
        this.fflogsService = fflogsService;
        this.converter = converter;
        this.configManager = configManager;

        Size = new Vector2(500, 450);
        SizeCondition = ImGuiCond.FirstUseEver;
    }

    public override void OnOpen()
    {
        base.OnOpen();
        Reset();

        // Load saved credentials
        clientIdInput = configManager.Configuration.FFLogsClientId;
        clientSecretInput = configManager.Configuration.FFLogsClientSecret;

        // Skip credentials step if already configured
        if (fflogsService.IsConfigured)
        {
            currentStep = ImportStep.ReportEntry;
        }
    }

    private void Reset()
    {
        currentStep = fflogsService.IsConfigured ? ImportStep.ReportEntry : ImportStep.Credentials;
        reportUrlInput = string.Empty;
        reportError = string.Empty;
        currentReport = null;
        selectedFightIndex = -1;
        selectedPlayerIndex = -1;
        playerActors.Clear();
        importOptions = new FFLogsImportOptions();
        previewTimeline = null;
        fetchedEvents = null;
        eventsError = string.Empty;
        connectionTestResult = string.Empty;
        credentialError = string.Empty;
    }

    public override void Draw()
    {
        switch (currentStep)
        {
            case ImportStep.Credentials:
                DrawCredentialsStep();
                break;
            case ImportStep.ReportEntry:
                DrawReportEntryStep();
                break;
            case ImportStep.FightSelection:
                DrawFightSelectionStep();
                break;
            case ImportStep.PlayerSelection:
                DrawPlayerSelectionStep();
                break;
            case ImportStep.Preview:
                DrawPreviewStep();
                break;
        }
    }

    private void DrawCredentialsStep()
    {
        ImGui.TextWrapped("To import from FFLogs, you need API credentials.");
        ImGui.Spacing();

        ImGui.TextColored(new Vector4(0.7f, 0.7f, 1f, 1f), "Setup Instructions:");
        ImGui.Indent();
        ImGui.TextWrapped("1. Go to fflogs.com/api/clients");
        ImGui.SameLine();
        if (ImGui.SmallButton("Open"))
        {
            try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = "https://www.fflogs.com/api/clients",
                UseShellExecute = true
            }); } catch { }
        }
        ImGui.TextWrapped("2. Click \"Create Client\"");
        ImGui.TextWrapped("3. Name it anything (e.g., \"Flowline Plugin\")");
        ImGui.TextWrapped("4. Copy the credentials below");
        ImGui.Unindent();

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        ImGui.SetNextItemWidth(-1);
        ImGui.InputText("Client ID", ref clientIdInput, 256);

        ImGui.SetNextItemWidth(-1);
        ImGui.InputText("Client Secret", ref clientSecretInput, 256, ImGuiInputTextFlags.Password);

        ImGui.Spacing();

        if (!string.IsNullOrEmpty(credentialError))
        {
            ImGui.TextColored(new Vector4(1, 0.4f, 0.4f, 1), credentialError);
        }

        if (!string.IsNullOrEmpty(connectionTestResult))
        {
            var color = connectionTestResult.StartsWith("Success")
                ? new Vector4(0.4f, 1, 0.4f, 1)
                : new Vector4(1, 0.4f, 0.4f, 1);
            ImGui.TextColored(color, connectionTestResult);
        }

        ImGui.Spacing();

        var canTest = !string.IsNullOrWhiteSpace(clientIdInput) &&
                      !string.IsNullOrWhiteSpace(clientSecretInput) &&
                      !isTestingConnection;

        if (!canTest) ImGui.BeginDisabled();
        if (ImGui.Button(isTestingConnection ? "Testing..." : "Test Connection"))
        {
            TestConnection();
        }
        if (!canTest) ImGui.EndDisabled();

        ImGui.SameLine();

        if (!canTest) ImGui.BeginDisabled();
        if (ImGui.Button("Save & Continue"))
        {
            SaveCredentialsAndContinue();
        }
        if (!canTest) ImGui.EndDisabled();

        // Add link to skip if already configured
        if (fflogsService.IsConfigured)
        {
            ImGui.SameLine();
            if (ImGui.Button("Skip"))
            {
                currentStep = ImportStep.ReportEntry;
            }
        }
    }

    private async void TestConnection()
    {
        if (isTestingConnection) return;
        isTestingConnection = true;
        connectionTestResult = string.Empty;

        // Temporarily save credentials for testing
        configManager.Configuration.FFLogsClientId = clientIdInput.Trim();
        configManager.Configuration.FFLogsClientSecret = clientSecretInput.Trim();

        var (success, error) = await fflogsService.TestConnectionAsync();

        connectionTestResult = success
            ? "Success! Connection established."
            : $"Failed: {error}";

        isTestingConnection = false;
    }

    private void SaveCredentialsAndContinue()
    {
        if (string.IsNullOrWhiteSpace(clientIdInput) || string.IsNullOrWhiteSpace(clientSecretInput))
        {
            credentialError = "Please enter both Client ID and Client Secret";
            return;
        }

        configManager.Configuration.FFLogsClientId = clientIdInput.Trim();
        configManager.Configuration.FFLogsClientSecret = clientSecretInput.Trim();
        configManager.SaveConfiguration();

        credentialError = string.Empty;
        currentStep = ImportStep.ReportEntry;
    }

    private void DrawReportEntryStep()
    {
        ImGui.Text("Paste FFLogs URL or report code:");
        ImGui.Spacing();

        ImGui.SetNextItemWidth(-1);
        if (ImGui.InputText("##reportUrl", ref reportUrlInput, 512, ImGuiInputTextFlags.EnterReturnsTrue))
        {
            FetchReport();
        }

        ImGui.Spacing();

        ImGui.TextColored(new Vector4(0.6f, 0.6f, 0.6f, 1), "Supported formats:");
        ImGui.TextColored(new Vector4(0.6f, 0.6f, 0.6f, 1), "  - https://www.fflogs.com/reports/ABC123");
        ImGui.TextColored(new Vector4(0.6f, 0.6f, 0.6f, 1), "  - https://cn.fflogs.com/reports/ABC123?fight=8");
        ImGui.TextColored(new Vector4(0.6f, 0.6f, 0.6f, 1), "  - ABC123");

        ImGui.Spacing();

        if (!string.IsNullOrEmpty(reportError))
        {
            ImGui.TextColored(new Vector4(1, 0.4f, 0.4f, 1), reportError);
        }

        ImGui.Spacing();

        var canFetch = !string.IsNullOrWhiteSpace(reportUrlInput) && !isFetchingReport;

        if (!canFetch) ImGui.BeginDisabled();
        if (ImGui.Button(isFetchingReport ? "Fetching..." : "Fetch Report"))
        {
            FetchReport();
        }
        if (!canFetch) ImGui.EndDisabled();

        // Settings button to go back to credentials
        ImGui.SameLine();
        if (ImGui.Button("API Settings"))
        {
            currentStep = ImportStep.Credentials;
        }
    }

    private async void FetchReport()
    {
        if (isFetchingReport) return;

        var code = FFLogsService.ParseReportCode(reportUrlInput);
        if (code == null)
        {
            reportError = "Invalid report URL or code";
            return;
        }

        isFetchingReport = true;
        reportError = string.Empty;

        var (report, error) = await fflogsService.GetReportAsync(code);

        if (error != null)
        {
            reportError = error;
            isFetchingReport = false;
            return;
        }

        currentReport = report;
        importOptions.ReportCode = code;

        // Check if URL had a fight ID
        var fightId = FFLogsService.ParseFightIdFromUrl(reportUrlInput);
        if (fightId.HasValue && currentReport != null)
        {
            var fightIndex = currentReport.Fights.FindIndex(f => f.Id == fightId.Value);
            if (fightIndex >= 0)
            {
                selectedFightIndex = fightIndex;
            }
        }

        isFetchingReport = false;
        currentStep = ImportStep.FightSelection;
    }

    private void DrawFightSelectionStep()
    {
        if (currentReport == null)
        {
            currentStep = ImportStep.ReportEntry;
            return;
        }

        ImGui.Text($"Report: {currentReport.Title}");
        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        ImGui.Text("Select a fight:");
        ImGui.Spacing();

        if (ImGui.BeginTable("FightsTable", 4, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg | ImGuiTableFlags.ScrollY,
            new Vector2(-1, 250)))
        {
            ImGui.TableSetupColumn("#", ImGuiTableColumnFlags.WidthFixed, 30);
            ImGui.TableSetupColumn("Fight Name", ImGuiTableColumnFlags.WidthStretch);
            ImGui.TableSetupColumn("Duration", ImGuiTableColumnFlags.WidthFixed, 60);
            ImGui.TableSetupColumn("Clear", ImGuiTableColumnFlags.WidthFixed, 40);
            ImGui.TableHeadersRow();

            for (int i = 0; i < currentReport.Fights.Count; i++)
            {
                var fight = currentReport.Fights[i];
                ImGui.TableNextRow();

                ImGui.TableNextColumn();
                var isSelected = selectedFightIndex == i;
                if (ImGui.Selectable($"{fight.Id}##fight{i}", isSelected, ImGuiSelectableFlags.SpanAllColumns))
                {
                    selectedFightIndex = i;
                }

                ImGui.TableNextColumn();
                ImGui.Text(fight.Name);

                ImGui.TableNextColumn();
                ImGui.Text(fight.DurationFormatted);

                ImGui.TableNextColumn();
                if (fight.Kill)
                {
                    ImGui.TextColored(new Vector4(0.4f, 1, 0.4f, 1), "Yes");
                }
            }

            ImGui.EndTable();
        }

        ImGui.Spacing();

        if (ImGui.Button("< Back"))
        {
            currentStep = ImportStep.ReportEntry;
        }

        ImGui.SameLine();

        var canContinue = selectedFightIndex >= 0;
        if (!canContinue) ImGui.BeginDisabled();
        if (ImGui.Button("Next >"))
        {
            PreparePlayerSelection();
            currentStep = ImportStep.PlayerSelection;
        }
        if (!canContinue) ImGui.EndDisabled();
    }

    private void PreparePlayerSelection()
    {
        if (currentReport == null || selectedFightIndex < 0) return;

        var fight = currentReport.Fights[selectedFightIndex];
        importOptions.FightId = fight.Id;

        // Filter to only player actors
        playerActors = currentReport.Actors
            .Where(a => a.IsPlayer)
            .OrderBy(a => a.SubType)
            .ThenBy(a => a.Name)
            .ToList();

        selectedPlayerIndex = -1;
    }

    private void DrawPlayerSelectionStep()
    {
        if (currentReport == null || selectedFightIndex < 0)
        {
            currentStep = ImportStep.FightSelection;
            return;
        }

        var fight = currentReport.Fights[selectedFightIndex];
        ImGui.Text($"Fight: {fight.Name} ({fight.DurationFormatted})");
        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        // Player selection
        ImGui.Text("Select player to import:");
        ImGui.Spacing();

        if (ImGui.BeginChild("PlayerList", new Vector2(-1, 150), true))
        {
            for (int i = 0; i < playerActors.Count; i++)
            {
                var actor = playerActors[i];
                var isSelected = selectedPlayerIndex == i;
                var jobName = FFLogsMappings.GetJobDisplayName(actor.SubType);

                if (ImGui.Selectable($"{actor.Name} ({jobName})##player{i}", isSelected))
                {
                    selectedPlayerIndex = i;
                    importOptions.SelectedActorId = actor.Id;
                    importOptions.SelectedActorJob = actor.SubType;
                }
            }
        }
        ImGui.EndChild();

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        // Action category filters
        ImGui.Text("Action Categories to Import:");
        ImGui.Spacing();

        var includeTankMit = importOptions.IncludeTankMitigations;
        if (ImGui.Checkbox("Tank Personal Mitigations", ref includeTankMit))
            importOptions.IncludeTankMitigations = includeTankMit;

        var includePartyMit = importOptions.IncludePartyMitigations;
        if (ImGui.Checkbox("Party Mitigations", ref includePartyMit))
            importOptions.IncludePartyMitigations = includePartyMit;

        var includeRaidBuffs = importOptions.IncludeRaidBuffs;
        if (ImGui.Checkbox("Raid Buffs", ref includeRaidBuffs))
            importOptions.IncludeRaidBuffs = includeRaidBuffs;

        var includeHealingOGCDs = importOptions.IncludeHealingOGCDs;
        if (ImGui.Checkbox("Healing oGCDs", ref includeHealingOGCDs))
            importOptions.IncludeHealingOGCDs = includeHealingOGCDs;

        var includeHealingGCDs = importOptions.IncludeHealingGCDs;
        if (ImGui.Checkbox("Healing GCDs", ref includeHealingGCDs))
            importOptions.IncludeHealingGCDs = includeHealingGCDs;

        var includeDPSCooldowns = importOptions.IncludeDPSCooldowns;
        if (ImGui.Checkbox("DPS Cooldowns", ref includeDPSCooldowns))
            importOptions.IncludeDPSCooldowns = includeDPSCooldowns;

        ImGui.Spacing();
        var includeAll = importOptions.IncludeAllActions;
        if (ImGui.Checkbox("Include ALL Actions", ref includeAll))
            importOptions.IncludeAllActions = includeAll;

        if (importOptions.IncludeAllActions)
        {
            ImGui.SameLine();
            ImGui.TextColored(new Vector4(1, 0.8f, 0.4f, 1), "(overrides above filters)");
        }

        ImGui.Spacing();

        if (!string.IsNullOrEmpty(eventsError))
        {
            ImGui.TextColored(new Vector4(1, 0.4f, 0.4f, 1), eventsError);
        }

        ImGui.Spacing();

        if (ImGui.Button("< Back"))
        {
            currentStep = ImportStep.FightSelection;
        }

        ImGui.SameLine();

        var canImport = selectedPlayerIndex >= 0 && !isFetchingEvents;
        if (!canImport) ImGui.BeginDisabled();
        if (ImGui.Button(isFetchingEvents ? "Importing..." : "Import"))
        {
            FetchEventsAndPreview();
        }
        if (!canImport) ImGui.EndDisabled();
    }

    private async void FetchEventsAndPreview()
    {
        if (isFetchingEvents || currentReport == null || selectedFightIndex < 0 || selectedPlayerIndex < 0)
            return;

        isFetchingEvents = true;
        eventsError = string.Empty;

        var fight = currentReport.Fights[selectedFightIndex];
        var player = playerActors[selectedPlayerIndex];

        var (events, error) = await fflogsService.GetPlayerCastsAsync(
            currentReport.Code,
            fight.Id,
            player.Id,
            fight.StartTime,
            fight.EndTime);

        if (error != null)
        {
            eventsError = error;
            isFetchingEvents = false;
            return;
        }

        fetchedEvents = events;
        previewTimeline = converter.ConvertToTimeline(currentReport, fight, player, events, importOptions);

        isFetchingEvents = false;
        currentStep = ImportStep.Preview;
    }

    private void DrawPreviewStep()
    {
        if (previewTimeline == null)
        {
            currentStep = ImportStep.PlayerSelection;
            return;
        }

        ImGui.Text($"Timeline: {previewTimeline.Name}");
        ImGui.Text($"Duration: {FormatDuration(previewTimeline.DurationSeconds)}");
        ImGui.Text($"Markers: {previewTimeline.Markers.Count}");

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        // Preview table
        if (ImGui.BeginTable("PreviewTable", 2, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg | ImGuiTableFlags.ScrollY,
            new Vector2(-1, 250)))
        {
            ImGui.TableSetupColumn("Time", ImGuiTableColumnFlags.WidthFixed, 60);
            ImGui.TableSetupColumn("Action", ImGuiTableColumnFlags.WidthStretch);
            ImGui.TableHeadersRow();

            foreach (var marker in previewTimeline.Markers.Take(100)) // Show first 100
            {
                ImGui.TableNextRow();

                ImGui.TableNextColumn();
                ImGui.Text(FormatTimestamp(marker.TimestampSeconds));

                ImGui.TableNextColumn();
                var actionName = GetActionName(marker.ActionId);
                ImGui.Text(actionName);
            }

            if (previewTimeline.Markers.Count > 100)
            {
                ImGui.TableNextRow();
                ImGui.TableNextColumn();
                ImGui.TableNextColumn();
                ImGui.TextColored(new Vector4(0.6f, 0.6f, 0.6f, 1),
                    $"... and {previewTimeline.Markers.Count - 100} more");
            }

            ImGui.EndTable();
        }

        ImGui.Spacing();

        if (ImGui.Button("< Back"))
        {
            currentStep = ImportStep.PlayerSelection;
        }

        ImGui.SameLine();

        if (ImGui.Button("Save Timeline"))
        {
            SaveTimeline();
        }
    }

    private void SaveTimeline()
    {
        if (previewTimeline == null) return;

        configManager.SaveTimeline(previewTimeline);
        IsOpen = false;
    }

    private string GetActionName(uint actionId)
    {
        // This would ideally use ActionDataService, but we'll use a simple approach
        return $"Action {actionId}";
    }

    private static string FormatTimestamp(float seconds)
    {
        var ts = TimeSpan.FromSeconds(seconds);
        return $"{(int)ts.TotalMinutes}:{ts.Seconds:D2}";
    }

    private static string FormatDuration(float seconds)
    {
        var ts = TimeSpan.FromSeconds(seconds);
        return $"{(int)ts.TotalMinutes}:{ts.Seconds:D2} ({(int)seconds} seconds)";
    }

    public void Dispose()
    {
    }
}
