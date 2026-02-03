using System.Collections.Generic;

namespace Flowline.Data;

/// <summary>
/// Static mappings for FFLogs data conversion.
/// </summary>
public static class FFLogsMappings
{
    /// <summary>
    /// Maps FFLogs job names to FFXIV job IDs.
    /// </summary>
    public static readonly Dictionary<string, uint> JobNameToId = new()
    {
        // Tanks
        { "Paladin", 19 },
        { "Warrior", 21 },
        { "DarkKnight", 32 },
        { "Gunbreaker", 37 },

        // Healers
        { "WhiteMage", 24 },
        { "Scholar", 28 },
        { "Astrologian", 33 },
        { "Sage", 40 },

        // Melee DPS
        { "Monk", 20 },
        { "Dragoon", 22 },
        { "Ninja", 30 },
        { "Samurai", 34 },
        { "Reaper", 39 },
        { "Viper", 41 },

        // Physical Ranged DPS
        { "Bard", 23 },
        { "Machinist", 31 },
        { "Dancer", 38 },

        // Magical Ranged DPS
        { "BlackMage", 25 },
        { "Summoner", 27 },
        { "RedMage", 35 },
        { "Pictomancer", 42 },

        // Limited
        { "BlueMage", 36 },
    };

    /// <summary>
    /// Maps FFXIV job IDs to display names.
    /// </summary>
    public static readonly Dictionary<uint, string> JobIdToName = new()
    {
        { 19, "Paladin" },
        { 21, "Warrior" },
        { 32, "Dark Knight" },
        { 37, "Gunbreaker" },
        { 24, "White Mage" },
        { 28, "Scholar" },
        { 33, "Astrologian" },
        { 40, "Sage" },
        { 20, "Monk" },
        { 22, "Dragoon" },
        { 30, "Ninja" },
        { 34, "Samurai" },
        { 39, "Reaper" },
        { 41, "Viper" },
        { 23, "Bard" },
        { 31, "Machinist" },
        { 38, "Dancer" },
        { 25, "Black Mage" },
        { 27, "Summoner" },
        { 35, "Red Mage" },
        { 42, "Pictomancer" },
        { 36, "Blue Mage" },
    };

    /// <summary>
    /// Tank personal mitigation abilities.
    /// </summary>
    public static readonly HashSet<uint> TankMitigations = new()
    {
        // Role actions
        7531,  // Rampart
        7548,  // Arm's Length

        // Paladin
        17,    // Sentinel -> Guardian
        3542,  // Sheltron -> Holy Sheltron
        7382,  // Intervention
        7385,  // Hallowed Ground
        25746, // Bulwark
        36918, // Guardian

        // Warrior
        40,    // Thrill of Battle
        42,    // Vengeance -> Damnation
        43,    // Holmgang
        7388,  // Shake It Off
        25751, // Bloodwhetting
        36923, // Damnation

        // Dark Knight
        3634,  // Shadow Wall -> Shadowed Vigil
        3636,  // Dark Mind
        3638,  // Living Dead
        7389,  // The Blackest Night
        25754, // Oblation
        36927, // Shadowed Vigil

        // Gunbreaker
        16140, // Camouflage
        16148, // Nebula -> Great Nebula
        16152, // Superbolide
        16161, // Heart of Stone -> Heart of Corundum
        25758, // Heart of Corundum
        36933, // Great Nebula
    };

    /// <summary>
    /// Party-wide mitigation abilities.
    /// </summary>
    public static readonly HashSet<uint> PartyMitigations = new()
    {
        // Role actions
        7535,  // Reprisal

        // Paladin
        3540,  // Divine Veil
        7383,  // Passage of Arms
        36921, // Divine Guardian

        // Warrior
        7388,  // Shake It Off

        // Dark Knight
        3635,  // Dark Missionary

        // Gunbreaker
        16160, // Heart of Light

        // Healer role
        7561,  // Surecast (healer)

        // White Mage
        3569,  // Divine Benison
        7432,  // Temperance
        25862, // Aquaveil
        37011, // Divine Caress

        // Scholar
        188,   // Sacred Soil
        805,   // Fey Illumination
        3584,  // Deployment Tactics
        7436,  // Fey Blessing
        16538, // Seraphic Illumination
        25868, // Expedient
        37013, // Seraphism

        // Astrologian
        3606,  // Collective Unconscious
        3613,  // Celestial Opposition
        16553, // Neutral Sect
        25873, // Exaltation
        25874, // Macrocosmos
        37017, // Sun Sign

        // Sage
        24288, // Kerachole
        24298, // Holos
        24301, // Panhaima
        24311, // Philosophia
    };

    /// <summary>
    /// Raid buff abilities.
    /// </summary>
    public static readonly HashSet<uint> RaidBuffs = new()
    {
        // Melee
        66,    // Dragon Sight -> Searing Light (Dragoon raid buff)
        7396,  // Battle Litany
        7549,  // Feint
        36955, // Lance Charge (DRG)

        // Ninja
        2258,  // Trick Attack
        16489, // Mug (debuff)

        // Monk
        65,    // Mantra
        36943, // Brotherhood

        // Samurai
        // (Samurai has no raid buff)

        // Reaper
        24405, // Arcane Circle

        // Viper
        // Check for Viper raid buffs

        // Physical Ranged
        118,   // Troubadour (Bard)
        3559,  // Battle Voice
        7405,  // Nature's Minne
        16012, // Tactician (MCH)
        16889, // Devilment (DNC)
        16004, // Technical Step/Finish (DNC)
        16196, // Shield Samba (DNC)

        // Caster
        7520,  // Addle
        25797, // Magick Barrier (RDM)
        3557,  // Embolden (RDM)
        25801, // Searing Light (SMN)
    };

    /// <summary>
    /// Healing oGCD abilities.
    /// </summary>
    public static readonly HashSet<uint> HealingOGCDs = new()
    {
        // White Mage
        140,   // Tetragrammaton
        3570,  // Assize
        7432,  // Temperance
        16531, // Afflatus Misery
        25862, // Aquaveil
        37010, // Glare IV

        // Scholar
        190,   // Lustrate
        805,   // Fey Illumination
        3583,  // Indomitability
        7434,  // Excogitation
        7436,  // Fey Blessing
        16537, // Recitation
        25867, // Protraction
        25868, // Expedient
        37013, // Seraphism

        // Astrologian
        3594,  // Essential Dignity
        3595,  // Aspected Benefic (instant)
        3600,  // Synastry
        7439,  // Earthly Star
        16552, // Celestial Intersection
        25871, // Exaltation
        37017, // Sun Sign

        // Sage
        24283, // Druochole
        24285, // Ixochole
        24287, // Taurochole
        24288, // Kerachole
        24290, // Pepsis
        24294, // Haima
        24296, // Rhizomata
        24298, // Holos
        24300, // Krasis
        24301, // Panhaima
        24302, // Pneuma
    };

    /// <summary>
    /// Healing GCD abilities.
    /// </summary>
    public static readonly HashSet<uint> HealingGCDs = new()
    {
        // White Mage
        120,   // Cure
        135,   // Cure II
        131,   // Medica
        133,   // Regen
        137,   // Medica II
        3568,  // Cure III
        16532, // Afflatus Solace
        16534, // Afflatus Rapture

        // Scholar
        185,   // Adloquium
        186,   // Succor
        189,   // Physick

        // Astrologian
        3594,  // Benefic
        3610,  // Benefic II
        3596,  // Aspected Helios
        3614,  // Helios

        // Sage
        24284, // Diagnosis
        24286, // Prognosis
        24291, // Eukrasian Diagnosis
        24293, // Eukrasian Prognosis
    };

    /// <summary>
    /// DPS cooldown abilities (major damage buffs).
    /// </summary>
    public static readonly HashSet<uint> DPSCooldowns = new()
    {
        // General
        7535,  // Tincture/Potion

        // Dragoon
        83,    // Life Surge
        85,    // Lance Charge
        92,    // Battle Litany

        // Monk
        69,    // Perfect Balance
        4262,  // Riddle of Fire

        // Ninja
        2259,  // Kassatsu
        16493, // Meisui
        2264,  // Ten Chi Jin

        // Samurai
        7494,  // Meikyo Shisui
        16481, // Ikishoten

        // Reaper
        24378, // Soul Slice
        24380, // Enshroud

        // Bard
        101,   // Raging Strikes
        3559,  // Battle Voice
        7408,  // Radiant Finale

        // Machinist
        2878,  // Wildfire
        16498, // Reassemble
        16501, // Barrel Stabilizer

        // Dancer
        15997, // Standard Step
        16004, // Technical Step
        16013, // Flourish

        // Black Mage
        3573,  // Ley Lines
        7421,  // Triplecast

        // Summoner
        25799, // Searing Light
        25800, // Energy Drain

        // Red Mage
        7520,  // Embolden
        7521,  // Manafication
    };

    /// <summary>
    /// Gets the job ID from FFLogs job name.
    /// </summary>
    public static uint GetJobId(string jobName)
    {
        return JobNameToId.TryGetValue(jobName, out var id) ? id : 0;
    }

    /// <summary>
    /// Gets display name for a job ID.
    /// </summary>
    public static string GetJobDisplayName(uint jobId)
    {
        return JobIdToName.TryGetValue(jobId, out var name) ? name : "Unknown";
    }

    /// <summary>
    /// Gets display name for FFLogs job name.
    /// </summary>
    public static string GetJobDisplayName(string fflogsJobName)
    {
        var jobId = GetJobId(fflogsJobName);
        return jobId > 0 ? GetJobDisplayName(jobId) : fflogsJobName;
    }

    /// <summary>
    /// Checks if an action should be included based on filter options.
    /// </summary>
    public static bool ShouldIncludeAction(uint actionId, FFLogsImportOptions options)
    {
        if (options.IncludeAllActions)
            return true;

        if (options.IncludeTankMitigations && TankMitigations.Contains(actionId))
            return true;

        if (options.IncludePartyMitigations && PartyMitigations.Contains(actionId))
            return true;

        if (options.IncludeRaidBuffs && RaidBuffs.Contains(actionId))
            return true;

        if (options.IncludeHealingOGCDs && HealingOGCDs.Contains(actionId))
            return true;

        if (options.IncludeHealingGCDs && HealingGCDs.Contains(actionId))
            return true;

        if (options.IncludeDPSCooldowns && DPSCooldowns.Contains(actionId))
            return true;

        return false;
    }
}
