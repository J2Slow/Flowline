using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace Flowline.Data;

/// <summary>
/// OAuth token response from FFLogs.
/// </summary>
public class FFLogsToken
{
    [JsonProperty("access_token")]
    public string AccessToken { get; set; } = string.Empty;

    [JsonProperty("token_type")]
    public string TokenType { get; set; } = string.Empty;

    [JsonProperty("expires_in")]
    public int ExpiresIn { get; set; }

    [JsonProperty("error")]
    public string? Error { get; set; }
}

/// <summary>
/// Report metadata from FFLogs.
/// </summary>
public class FFLogsReport
{
    public string Code { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public long StartTime { get; set; }
    public long EndTime { get; set; }
    public List<FFLogsFight> Fights { get; set; } = new();
    public List<FFLogsActor> Actors { get; set; } = new();
}

/// <summary>
/// A single fight/pull within a report.
/// </summary>
public class FFLogsFight
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public long StartTime { get; set; }
    public long EndTime { get; set; }
    public bool Kill { get; set; }
    public int? BossPercentage { get; set; }

    /// <summary>
    /// IDs of players participating in this specific fight.
    /// </summary>
    public List<int> FriendlyPlayers { get; set; } = new();

    /// <summary>
    /// The in-game zone ID (FFXIV territory ID).
    /// </summary>
    public int GameZoneId { get; set; }

    /// <summary>
    /// Duration in seconds.
    /// </summary>
    public float DurationSeconds => (EndTime - StartTime) / 1000f;

    /// <summary>
    /// Formatted duration string (m:ss).
    /// </summary>
    public string DurationFormatted
    {
        get
        {
            var total = TimeSpan.FromSeconds(DurationSeconds);
            return $"{(int)total.TotalMinutes}:{total.Seconds:D2}";
        }
    }
}

/// <summary>
/// A player or NPC actor in a report.
/// </summary>
public class FFLogsActor
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;  // "Player", "NPC", "Pet"
    public string SubType { get; set; } = string.Empty;  // Job name like "DarkKnight"
    public string? Server { get; set; }

    /// <summary>
    /// Whether this is a player (not NPC/Pet).
    /// </summary>
    public bool IsPlayer => Type == "Player";

    /// <summary>
    /// Display name with job.
    /// </summary>
    public string DisplayName => $"{Name} ({SubType})";
}

/// <summary>
/// A cast event from the events query.
/// </summary>
public class FFLogsCastEvent
{
    [JsonProperty("timestamp")]
    public long Timestamp { get; set; }

    [JsonProperty("type")]
    public string Type { get; set; } = string.Empty;  // "cast", "begincast"

    [JsonProperty("sourceID")]
    public int SourceID { get; set; }

    [JsonProperty("targetID")]
    public int? TargetID { get; set; }

    [JsonProperty("abilityGameID")]
    public uint AbilityGameID { get; set; }
}

/// <summary>
/// Events response wrapper with pagination.
/// </summary>
public class FFLogsEventsResponse
{
    public List<FFLogsCastEvent> Data { get; set; } = new();
    public long? NextPageTimestamp { get; set; }
}

/// <summary>
/// Import options selected by user.
/// </summary>
public class FFLogsImportOptions
{
    public string ReportCode { get; set; } = string.Empty;
    public int FightId { get; set; }
    public int SelectedActorId { get; set; }
    public string SelectedActorJob { get; set; } = string.Empty;

    // Category filters
    public bool IncludeTankMitigations { get; set; } = true;
    public bool IncludePartyMitigations { get; set; } = true;
    public bool IncludeRaidBuffs { get; set; } = true;
    public bool IncludeHealingOGCDs { get; set; } = true;
    public bool IncludeHealingGCDs { get; set; } = false;
    public bool IncludeDPSCooldowns { get; set; } = false;
    public bool IncludeAllActions { get; set; } = false;
}
