using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using Flowline.Configuration;
using Flowline.Data;
using Newtonsoft.Json;

namespace Flowline.Services;

/// <summary>
/// Recording mode for action tracking.
/// </summary>
public enum RecordingType
{
    /// <summary>Fresh recording without existing timeline.</summary>
    Fresh,
    /// <summary>Recording alongside an existing timeline.</summary>
    Sidebyside
}

/// <summary>
/// Records player and party actions during encounters.
/// </summary>
public class ActionRecorderService : IDisposable
{
    private readonly IChatGui chatGui;
    private readonly IClientState clientState;
    private readonly IObjectTable objectTable;
    private readonly IPartyList partyList;
    private readonly IDalamudPluginInterface pluginInterface;
    private readonly IPluginLog pluginLog;
    private readonly FlowlineConfiguration config;

    private readonly Stopwatch recordingStopwatch = new();
    private RecordedEncounter? currentRecording;
    private bool isManuallyRecording = false;
    private readonly string recordingsDirectory;

    // New recording mode fields
    private RecordingType currentRecordingType = RecordingType.Fresh;
    private string? sidebysideTimelineName;
    private bool waitingForCombatOrCountdown = false;
    private bool countdownReached = false;

    public RecordingType CurrentRecordingType => currentRecordingType;
    public bool WaitingForStart => waitingForCombatOrCountdown;

    // Regex for parsing action usage from chat (English)
    // Example: "You use Superbolide."
    private static readonly Regex ActionUsePattern = new Regex(
        @"(You|.+?) uses? (.+?)\.",
        RegexOptions.Compiled | RegexOptions.IgnoreCase
    );

    public bool IsRecording => currentRecording != null;
    public RecordedEncounter? CurrentRecording => currentRecording;

    /// <summary>
    /// Event fired when recording starts.
    /// </summary>
    public event Action<RecordedEncounter>? RecordingStarted;

    /// <summary>
    /// Event fired when recording stops.
    /// </summary>
    public event Action<RecordedEncounter>? RecordingStopped;

    public ActionRecorderService(
        IChatGui chatGui,
        IClientState clientState,
        IObjectTable objectTable,
        IPartyList partyList,
        IDalamudPluginInterface pluginInterface,
        IPluginLog pluginLog,
        FlowlineConfiguration config)
    {
        this.chatGui = chatGui;
        this.clientState = clientState;
        this.objectTable = objectTable;
        this.partyList = partyList;
        this.pluginInterface = pluginInterface;
        this.pluginLog = pluginLog;
        this.config = config;

        // Create recordings directory
        recordingsDirectory = Path.Combine(
            pluginInterface.ConfigDirectory.FullName,
            "recordings"
        );
        Directory.CreateDirectory(recordingsDirectory);

        // Subscribe to chat messages for action tracking
        this.chatGui.ChatMessage += OnChatMessage;
    }

    /// <summary>
    /// Starts recording manually.
    /// </summary>
    public void StartRecording(ushort territoryId, string name = "")
    {
        if (IsRecording)
            StopRecording();

        currentRecording = new RecordedEncounter(territoryId, name);
        recordingStopwatch.Restart();
        isManuallyRecording = true;

        // Capture current party members
        CapturePartyMembers();

        RecordingStarted?.Invoke(currentRecording);
    }

    /// <summary>
    /// Starts manual recording with territory and job abbreviation as name.
    /// </summary>
    public void StartManualRecording(ushort territoryId, string jobAbbrev)
    {
        var name = $"{territoryId}_{jobAbbrev}";
        StartRecording(territoryId, name);
    }

    /// <summary>
    /// Starts a Fresh recording - waits for combat or countdown to begin timing.
    /// </summary>
    public void StartFreshRecording(ushort territoryId, string jobAbbrev, string instanceName)
    {
        if (IsRecording)
            StopRecording();

        currentRecordingType = RecordingType.Fresh;
        sidebysideTimelineName = null;
        waitingForCombatOrCountdown = true;
        countdownReached = false;

        var date = DateTime.Now.ToString("yyyy-MM-dd HH-mm");
        var name = $"Recording {jobAbbrev} {instanceName} {date}";

        currentRecording = new RecordedEncounter(territoryId, name);
        CapturePartyMembers();
        RecordingStarted?.Invoke(currentRecording);
    }

    /// <summary>
    /// Starts a Sidebyside recording alongside an existing timeline.
    /// </summary>
    public void StartSidebysideRecording(ushort territoryId, string jobAbbrev, string instanceName, string timelineName)
    {
        if (IsRecording)
            StopRecording();

        currentRecordingType = RecordingType.Sidebyside;
        sidebysideTimelineName = timelineName;
        waitingForCombatOrCountdown = true;
        countdownReached = false;

        var date = DateTime.Now.ToString("yyyy-MM-dd HH-mm");
        var name = $"Sidebyside {timelineName} {jobAbbrev} {instanceName} {date}";

        currentRecording = new RecordedEncounter(territoryId, name);
        CapturePartyMembers();
        RecordingStarted?.Invoke(currentRecording);
    }

    /// <summary>
    /// Called when countdown reaches 0 - starts the actual recording timer.
    /// </summary>
    public void OnCountdownReached()
    {
        if (!IsRecording || !waitingForCombatOrCountdown)
            return;

        countdownReached = true;
        waitingForCombatOrCountdown = false;
        recordingStopwatch.Restart();
    }

    /// <summary>
    /// Called when combat starts - starts timer if countdown hasn't already.
    /// </summary>
    public void OnCombatStart()
    {
        if (!IsRecording || !waitingForCombatOrCountdown)
            return;

        // Countdown has priority, so only start if countdown hasn't triggered
        if (!countdownReached)
        {
            waitingForCombatOrCountdown = false;
            recordingStopwatch.Restart();
        }
    }

    /// <summary>
    /// Called when combat ends - optionally stops recording.
    /// </summary>
    public void OnCombatEnd()
    {
        if (!IsRecording)
            return;

        // Auto-stop recording when combat ends
        StopRecording();
    }

    /// <summary>
    /// Starts recording automatically (triggered by duty detection).
    /// </summary>
    public void StartAutomaticRecording(ushort territoryId)
    {
        if (!config.AutoRecordInConfiguredDuties)
            return;

        if (IsRecording)
            return;

        currentRecording = new RecordedEncounter(territoryId);
        recordingStopwatch.Restart();
        isManuallyRecording = false;

        CapturePartyMembers();
        RecordingStarted?.Invoke(currentRecording);
    }

    /// <summary>
    /// Stops the current recording and saves it.
    /// </summary>
    public void StopRecording(bool isCleared = false)
    {
        if (!IsRecording || currentRecording == null)
            return;

        recordingStopwatch.Stop();
        currentRecording.DurationSeconds = (float)recordingStopwatch.Elapsed.TotalSeconds;
        currentRecording.IsCleared = isCleared;

        // Save recording
        SaveRecording(currentRecording);

        var recordingToReturn = currentRecording;
        currentRecording = null;
        isManuallyRecording = false;
        waitingForCombatOrCountdown = false;
        countdownReached = false;
        sidebysideTimelineName = null;

        RecordingStopped?.Invoke(recordingToReturn);
    }

    /// <summary>
    /// Toggles manual recording on/off.
    /// </summary>
    public void ToggleRecording()
    {
        if (IsRecording)
        {
            StopRecording();
        }
        else
        {
            var territoryId = clientState.TerritoryType;
            StartRecording(territoryId);
        }
    }

    private void OnChatMessage(
        XivChatType type,
        int timestamp,
        ref SeString sender,
        ref SeString message,
        ref bool isHandled)
    {
        if (!IsRecording || currentRecording == null)
            return;

        // Don't record while waiting for combat/countdown
        if (waitingForCombatOrCountdown)
            return;

        // Only track action-related chat types
        if (!IsActionChatType(type))
            return;

        var messageText = message.TextValue;
        var match = ActionUsePattern.Match(messageText);

        if (match.Success)
        {
            var playerName = match.Groups[1].Value;
            var actionName = match.Groups[2].Value;

            // Normalize "You" to actual player name
            if (playerName.Equals("You", StringComparison.OrdinalIgnoreCase))
            {
                playerName = objectTable.LocalPlayer?.Name.TextValue ?? "You";
            }

            // Only record party members if configured
            if (!config.RecordPartyActions && playerName != objectTable.LocalPlayer?.Name.TextValue)
                return;

            // Get timestamp
            var recordingTime = (float)recordingStopwatch.Elapsed.TotalSeconds;

            // Create recorded action (we don't have action ID from chat, so set to 0)
            // A more advanced implementation would use hooks to get actual action IDs
            var recordedAction = new RecordedAction
            {
                TimestampSeconds = recordingTime,
                ActionId = 0, // Unknown from chat
                PlayerName = playerName,
                JobId = GetJobIdForPlayer(playerName),
                TargetName = "" // Unknown from chat
            };

            currentRecording.AddAction(recordedAction);
        }
    }

    private bool IsActionChatType(XivChatType type)
    {
        return type switch
        {
            XivChatType.StandardEmote => true,
            XivChatType.CustomEmote => true,
            XivChatType.Echo => false,
            _ => type >= XivChatType.CustomEmote && type <= XivChatType.Party
        };
    }

    private uint GetJobIdForPlayer(string playerName)
    {
        // Try to find the player in the party list
        var partyMember = partyList.FirstOrDefault(m => m.Name.TextValue == playerName);
        if (partyMember != null)
        {
            return partyMember.ClassJob.RowId;
        }

        // If it's the local player
        if (playerName == objectTable.LocalPlayer?.Name.TextValue)
        {
            return objectTable.LocalPlayer?.ClassJob.RowId ?? 0;
        }

        return 0;
    }

    private void CapturePartyMembers()
    {
        if (currentRecording == null)
            return;

        currentRecording.PartyMembers.Clear();

        // Add local player
        if (objectTable.LocalPlayer != null)
        {
            currentRecording.PartyMembers.Add(objectTable.LocalPlayer.Name.TextValue);
        }

        // Add party members
        foreach (var member in partyList)
        {
            if (member.Name.TextValue != null && !currentRecording.PartyMembers.Contains(member.Name.TextValue))
            {
                currentRecording.PartyMembers.Add(member.Name.TextValue);
            }
        }
    }

    private void SaveRecording(RecordedEncounter recording)
    {
        var fileName = $"{SanitizeFileName(recording.Name)}_{recording.Id}.json";
        var filePath = Path.Combine(recordingsDirectory, fileName);

        var json = JsonConvert.SerializeObject(recording, Formatting.Indented);
        File.WriteAllText(filePath, json);

        // Clean up old recordings if configured
        CleanupOldRecordings(recording.TerritoryId);
    }

    private void CleanupOldRecordings(ushort territoryId)
    {
        if (config.MaxRecordingsPerDuty <= 0)
            return;

        var recordings = LoadRecordingsForTerritory(territoryId)
            .OrderByDescending(r => r.RecordedAt)
            .ToList();

        if (recordings.Count > config.MaxRecordingsPerDuty)
        {
            var toDelete = recordings.Skip(config.MaxRecordingsPerDuty);
            foreach (var recording in toDelete)
            {
                var files = Directory.GetFiles(recordingsDirectory, $"*_{recording.Id}.json");
                foreach (var file in files)
                {
                    File.Delete(file);
                }
            }
        }
    }

    public List<RecordedEncounter> LoadRecordingsForTerritory(ushort territoryId)
    {
        var recordings = new List<RecordedEncounter>();

        if (!Directory.Exists(recordingsDirectory))
            return recordings;

        foreach (var file in Directory.GetFiles(recordingsDirectory, "*.json"))
        {
            try
            {
                var json = File.ReadAllText(file);
                var recording = JsonConvert.DeserializeObject<RecordedEncounter>(json);

                if (recording != null && recording.TerritoryId == territoryId)
                {
                    recordings.Add(recording);
                }
            }
            catch (Exception ex)
            {
                pluginLog.Error($"Failed to load recording from {file}: {ex.Message}");
            }
        }

        return recordings;
    }

    public List<RecordedEncounter> LoadAllRecordings()
    {
        var recordings = new List<RecordedEncounter>();

        if (!Directory.Exists(recordingsDirectory))
            return recordings;

        foreach (var file in Directory.GetFiles(recordingsDirectory, "*.json"))
        {
            try
            {
                var json = File.ReadAllText(file);
                var recording = JsonConvert.DeserializeObject<RecordedEncounter>(json);

                if (recording != null)
                {
                    recordings.Add(recording);
                }
            }
            catch (Exception ex)
            {
                pluginLog.Error($"Failed to load recording from {file}: {ex.Message}");
            }
        }

        return recordings;
    }

    private static string SanitizeFileName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        return string.Join("_", name.Split(invalid, StringSplitOptions.RemoveEmptyEntries));
    }

    public void Dispose()
    {
        chatGui.ChatMessage -= OnChatMessage;

        if (IsRecording)
        {
            StopRecording();
        }
    }
}
