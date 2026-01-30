using System;

namespace Flowline.Data;

/// <summary>
/// Represents a single action that was recorded during an encounter.
/// </summary>
[Serializable]
public class RecordedAction
{
    /// <summary>
    /// Timestamp in seconds from the start of the encounter.
    /// </summary>
    public float TimestampSeconds { get; set; }

    /// <summary>
    /// The FFXIV action ID.
    /// </summary>
    public uint ActionId { get; set; }

    /// <summary>
    /// Name of the player who used this action.
    /// </summary>
    public string PlayerName { get; set; } = string.Empty;

    /// <summary>
    /// Job ID of the player.
    /// </summary>
    public uint JobId { get; set; }

    /// <summary>
    /// Target name (if applicable).
    /// </summary>
    public string TargetName { get; set; } = string.Empty;

    public RecordedAction()
    {
    }

    public RecordedAction(float timestamp, uint actionId, string playerName, uint jobId, string targetName = "")
    {
        TimestampSeconds = timestamp;
        ActionId = actionId;
        PlayerName = playerName;
        JobId = jobId;
        TargetName = targetName;
    }
}
