using System;
using System.Collections.Generic;

namespace Flowline.Data;

/// <summary>
/// Represents a recorded encounter with all actions from party members.
/// </summary>
[Serializable]
public class RecordedEncounter
{
    /// <summary>
    /// Unique identifier for this recording.
    /// </summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Name/title of this recording.
    /// </summary>
    public string Name { get; set; } = "Recorded Encounter";

    /// <summary>
    /// Territory ID where this was recorded.
    /// </summary>
    public ushort TerritoryId { get; set; }

    /// <summary>
    /// When this encounter was recorded.
    /// </summary>
    public DateTime RecordedAt { get; set; } = DateTime.Now;

    /// <summary>
    /// Total duration of the encounter in seconds.
    /// </summary>
    public float DurationSeconds { get; set; }

    /// <summary>
    /// All recorded actions from all party members.
    /// </summary>
    public List<RecordedAction> Actions { get; set; } = new();

    /// <summary>
    /// List of party member names present during this recording.
    /// </summary>
    public List<string> PartyMembers { get; set; } = new();

    /// <summary>
    /// Whether the encounter was cleared successfully.
    /// </summary>
    public bool IsCleared { get; set; }

    public RecordedEncounter()
    {
    }

    public RecordedEncounter(ushort territoryId, string name = "")
    {
        TerritoryId = territoryId;
        Name = string.IsNullOrEmpty(name) ? $"Recording {DateTime.Now:yyyy-MM-dd HH:mm}" : name;
    }

    /// <summary>
    /// Adds a recorded action to this encounter.
    /// </summary>
    public void AddAction(RecordedAction action)
    {
        Actions.Add(action);
        DurationSeconds = Math.Max(DurationSeconds, action.TimestampSeconds);
    }
}
