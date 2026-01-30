using System.Collections.Generic;
using System.Linq;
using Dalamud.Plugin.Services;
using Lumina.Excel.Sheets;

namespace Flowline.Data;

/// <summary>
/// Service for accessing FFXIV action data (names, icons, etc.).
/// </summary>
public class ActionDataService
{
    private readonly IDataManager dataManager;
    private readonly Dictionary<uint, ActionData> actionCache = new();
    private readonly Dictionary<byte, List<ActionData>> actionsByJob = new();

    // Known action upgrade chains (base action -> final form)
    // These are actions that upgrade to higher versions
    private static readonly HashSet<uint> UpgradedAwayActions = new()
    {
        // AST Malefic chain (only keep highest)
        3596, 3598, 7442, 16555, // Malefic I-IV (keep 25871 Malefic for Dawntrail)
        // WHM Stone chain
        119, 127, 3568, 7431, // Stone I-IV (keep 25859 Glare III)
        // SCH Broil chain
        3584, 7435, 16541, // Broil I-III (keep 25865 Broil IV)
        // SGE Dosis chain
        24283, 24306, // Dosis I-II (keep 24312 Dosis III)
        // BLM Fire chain
        141, 147, // Fire I-II (keep Fire III)
        // SMN Ruin chain
        163, 172, 7426, // Ruin I-III
    };

    // PVP action IDs start from these ranges
    private const uint PvpActionStart = 29000;

    public ActionDataService(IDataManager dataManager)
    {
        this.dataManager = dataManager;
        InitializeCache();
    }

    private void InitializeCache()
    {
        var actionSheet = dataManager.GetExcelSheet<Action>();
        if (actionSheet == null)
            return;

        foreach (var action in actionSheet)
        {
            if (action.RowId == 0 || string.IsNullOrEmpty(action.Name.ToString()))
                continue;

            // Skip PVP actions (they have IsPvP flag or are in PVP range)
            if (action.RowId >= PvpActionStart && action.RowId < 30000)
                continue;

            // Skip actions that have been upgraded to better versions
            if (UpgradedAwayActions.Contains(action.RowId))
                continue;

            // Skip actions with no icon (usually not player actions)
            if (action.Icon == 0)
                continue;

            var classJobId = (byte)action.ClassJob.RowId;

            var actionData = new ActionData
            {
                ActionId = action.RowId,
                Name = action.Name.ToString(),
                IconId = action.Icon,
                ClassJob = classJobId,
                // Store recast time as duration hint (in seconds)
                RecastTime = action.Recast100ms / 10f,
            };

            actionCache[action.RowId] = actionData;

            // Index by job for faster filtering
            if (!actionsByJob.TryGetValue(classJobId, out var jobActions))
            {
                jobActions = new List<ActionData>();
                actionsByJob[classJobId] = jobActions;
            }
            jobActions.Add(actionData);
        }
    }

    public string GetActionName(uint actionId)
    {
        if (actionCache.TryGetValue(actionId, out var data))
            return data.Name;

        return $"Unknown Action ({actionId})";
    }

    public uint GetActionIconId(uint actionId)
    {
        if (actionCache.TryGetValue(actionId, out var data))
            return data.IconId;

        return 0;
    }

    public ActionData? GetActionData(uint actionId)
    {
        actionCache.TryGetValue(actionId, out var data);
        return data;
    }

    public IEnumerable<ActionData> SearchActions(string query)
    {
        var lowerQuery = query.ToLowerInvariant();
        foreach (var data in actionCache.Values)
        {
            if (data.Name.ToLowerInvariant().Contains(lowerQuery))
                yield return data;
        }
    }

    public IEnumerable<ActionData> SearchActions(string query, byte? jobId)
    {
        var lowerQuery = query.ToLowerInvariant();

        // If job specified, use indexed lookup for better performance
        if (jobId.HasValue && actionsByJob.TryGetValue(jobId.Value, out var jobActions))
        {
            foreach (var data in jobActions)
            {
                if (string.IsNullOrEmpty(lowerQuery) || data.Name.ToLowerInvariant().Contains(lowerQuery))
                    yield return data;
            }
            yield break;
        }

        // No job filter - search all
        foreach (var data in actionCache.Values)
        {
            if (string.IsNullOrEmpty(lowerQuery) || data.Name.ToLowerInvariant().Contains(lowerQuery))
                yield return data;
        }
    }

    /// <summary>
    /// Gets estimated duration for an action (based on common buff/debuff durations).
    /// </summary>
    public float GetActionDuration(uint actionId)
    {
        // Common buff/ability durations - these are approximate
        return actionId switch
        {
            // Tank mitigations (usually 15-20s)
            7531 => 15f, // Rampart
            7535 => 10f, // Reprisal
            7548 => 6f,  // Arm's Length

            // GNB
            16152 => 8f,  // Camouflage
            16161 => 8f,  // Heart of Light
            16153 => 4f,  // Heart of Stone
            25758 => 4f,  // Heart of Corundum
            16160 => 10f, // Superbolide (invuln)

            // PLD
            7382 => 6f,  // Sentinel
            7385 => 10f, // Hallowed Ground
            3542 => 6f,  // Divine Veil

            // WAR
            44 => 20f,   // Vengeance
            43 => 10f,   // Holmgang
            7388 => 15f, // Shake It Off

            // DRK
            7393 => 10f, // The Blackest Night
            3636 => 15f, // Dark Mind
            3634 => 10f, // Living Dead

            // Healer shields/buffs
            7433 => 30f, // Sacred Soil (SCH)
            3583 => 30f, // Asylum (WHM)
            16556 => 15f, // Neutral Sect (AST)
            24298 => 20f, // Kerachole (SGE)

            // DPS buffs
            7396 => 20f, // Battle Litany (DRG)
            7398 => 15f, // Brotherhood (MNK)
            16461 => 15f, // Divination (AST)
            7520 => 20f, // Chain Stratagem (SCH)
            16552 => 15f, // Technical Finish (DNC)
            25785 => 20f, // Arcane Circle (RPR)

            _ => 0f // Unknown/instant
        };
    }
}

/// <summary>
/// Cached action data.
/// </summary>
public class ActionData
{
    public uint ActionId { get; set; }
    public string Name { get; set; } = string.Empty;
    public uint IconId { get; set; }
    public byte ClassJob { get; set; }
    public float RecastTime { get; set; }
}
