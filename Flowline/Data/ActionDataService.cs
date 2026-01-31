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

    // PVP action range
    private const uint PvpActionStart = 29000;
    private const uint PvpActionEnd = 30000;

    // Valid action categories for player combat actions
    // 2 = Spell, 3 = Weaponskill, 4 = Ability
    private static readonly HashSet<byte> ValidActionCategories = new() { 2, 3, 4 };

    // All combat job IDs (classes and jobs)
    private static readonly byte[] AllJobIds = {
        1, 2, 3, 4, 5, 6, 7,           // Base classes: GLA, PGL, MRD, LNC, ARC, CNJ, THM
        19, 20, 21, 22, 23, 24, 25,    // Jobs: PLD, MNK, WAR, DRG, BRD, WHM, BLM
        26, 27, 28, 29, 30,            // ACN, SMN, SCH, ROG, NIN
        31, 32, 33, 34, 35, 36,        // MCH, DRK, AST, SAM, RDM, BLU
        37, 38, 39, 40, 41, 42         // GNB, DNC, RPR, SGE, VPR, PCT
    };

    public ActionDataService(IDataManager dataManager)
    {
        this.dataManager = dataManager;
        InitializeCache();
    }

    private void InitializeCache()
    {
        var actionSheet = dataManager.GetExcelSheet<Action>();
        var classJobCategorySheet = dataManager.GetExcelSheet<ClassJobCategory>();
        if (actionSheet == null || classJobCategorySheet == null)
            return;

        foreach (var action in actionSheet)
        {
            // Skip invalid entries
            if (action.RowId == 0)
                continue;

            var actionName = action.Name.ToString();
            if (string.IsNullOrEmpty(actionName))
                continue;

            // Skip PVP actions
            if (action.RowId >= PvpActionStart && action.RowId < PvpActionEnd)
                continue;

            // Skip actions with IsPvP flag
            if (action.IsPvP)
                continue;

            // Skip actions with no icon (usually system actions)
            if (action.Icon == 0)
                continue;

            // Only include Spells (2), Weaponskills (3), and Abilities (4)
            var actionCategory = (byte)action.ActionCategory.RowId;
            if (!ValidActionCategories.Contains(actionCategory))
                continue;

            var classJobCategoryId = action.ClassJobCategory.RowId;

            // Skip if no job category (can't determine who can use it)
            if (classJobCategoryId == 0)
                continue;

            // Skip if no level requirement (usually system/NPC actions)
            if (action.ClassJobLevel == 0)
                continue;

            var actionData = new ActionData
            {
                ActionId = action.RowId,
                Name = actionName,
                IconId = action.Icon,
                ClassJob = (byte)action.ClassJob.RowId,
                ClassJobCategoryId = classJobCategoryId,
                ClassJobLevel = action.ClassJobLevel,
                ActionCategory = actionCategory,
                RecastTime = action.Recast100ms / 10f,
            };

            actionCache[action.RowId] = actionData;

            // Use ClassJobCategory to determine which jobs can use this action
            var categoryRow = classJobCategorySheet.GetRowOrDefault(classJobCategoryId);
            if (categoryRow.HasValue)
            {
                IndexActionByCategory(actionData, categoryRow.Value);
            }
        }

        // Sort each job's actions by level for better display
        foreach (var jobActions in actionsByJob.Values)
        {
            jobActions.Sort((a, b) => a.ClassJobLevel.CompareTo(b.ClassJobLevel));
        }
    }

    private void IndexActionByCategory(ActionData actionData, ClassJobCategory category)
    {
        // Check each job and add this action to their list if they can use it
        foreach (var jobId in AllJobIds)
        {
            if (CanJobUseCategory(jobId, category))
            {
                AddActionToJob(actionData, jobId);
            }
        }
    }

    private bool CanJobUseCategory(byte jobId, ClassJobCategory category)
    {
        return jobId switch
        {
            1 => category.GLA,   // Gladiator
            2 => category.PGL,   // Pugilist
            3 => category.MRD,   // Marauder
            4 => category.LNC,   // Lancer
            5 => category.ARC,   // Archer
            6 => category.CNJ,   // Conjurer
            7 => category.THM,   // Thaumaturge
            19 => category.PLD,  // Paladin
            20 => category.MNK,  // Monk
            21 => category.WAR,  // Warrior
            22 => category.DRG,  // Dragoon
            23 => category.BRD,  // Bard
            24 => category.WHM,  // White Mage
            25 => category.BLM,  // Black Mage
            26 => category.ACN,  // Arcanist
            27 => category.SMN,  // Summoner
            28 => category.SCH,  // Scholar
            29 => category.ROG,  // Rogue
            30 => category.NIN,  // Ninja
            31 => category.MCH,  // Machinist
            32 => category.DRK,  // Dark Knight
            33 => category.AST,  // Astrologian
            34 => category.SAM,  // Samurai
            35 => category.RDM,  // Red Mage
            36 => category.BLU,  // Blue Mage
            37 => category.GNB,  // Gunbreaker
            38 => category.DNC,  // Dancer
            39 => category.RPR,  // Reaper
            40 => category.SGE,  // Sage
            41 => category.VPR,  // Viper
            42 => category.PCT,  // Pictomancer
            _ => false
        };
    }

    private void AddActionToJob(ActionData actionData, byte jobId)
    {
        if (!actionsByJob.TryGetValue(jobId, out var jobActions))
        {
            jobActions = new List<ActionData>();
            actionsByJob[jobId] = jobActions;
        }
        // Avoid duplicates
        if (!jobActions.Any(a => a.ActionId == actionData.ActionId))
        {
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

        // If job specified, get all actions available to that job (including role actions)
        if (jobId.HasValue && jobId.Value > 0)
        {
            if (actionsByJob.TryGetValue(jobId.Value, out var jobActions))
            {
                var results = new List<ActionData>();
                foreach (var data in jobActions)
                {
                    if (string.IsNullOrEmpty(lowerQuery) || data.Name.ToLowerInvariant().Contains(lowerQuery))
                        results.Add(data);
                }
                return results; // Already sorted by level
            }
            return Enumerable.Empty<ActionData>();
        }

        // No job filter - search all
        var allResults = new List<ActionData>();
        foreach (var data in actionCache.Values)
        {
            if (string.IsNullOrEmpty(lowerQuery) || data.Name.ToLowerInvariant().Contains(lowerQuery))
                allResults.Add(data);
        }
        allResults.Sort((a, b) => a.ClassJobLevel.CompareTo(b.ClassJobLevel));
        return allResults;
    }

    /// <summary>
    /// Gets all actions for a specific job, including role actions.
    /// </summary>
    public IEnumerable<ActionData> GetActionsForJob(byte jobId)
    {
        return SearchActions(string.Empty, jobId);
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
    public uint ClassJobCategoryId { get; set; }
    public byte ClassJobLevel { get; set; }
    public byte ActionCategory { get; set; } // 2=Spell, 3=Weaponskill, 4=Ability
    public float RecastTime { get; set; }
}
