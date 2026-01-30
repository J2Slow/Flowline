using System.Collections.Generic;
using Dalamud.Plugin.Services;
using Lumina.Excel.Sheets;

namespace Flowline.Data;

/// <summary>
/// Service for accessing FFXIV duty and territory information.
/// </summary>
public class DutyDataService
{
    private readonly IDataManager dataManager;
    private readonly Dictionary<ushort, DutyData> dutyCache = new();
    private readonly Dictionary<ushort, string> territoryCache = new();

    public DutyDataService(IDataManager dataManager)
    {
        this.dataManager = dataManager;
        InitializeCache();
    }

    private void InitializeCache()
    {
        // Cache ContentFinderCondition data (duties)
        var cfc = dataManager.GetExcelSheet<ContentFinderCondition>();
        if (cfc != null)
        {
            foreach (var duty in cfc)
            {
                if (duty.RowId == 0 || duty.TerritoryType.RowId == 0)
                    continue;

                var territoryId = (ushort)duty.TerritoryType.RowId;
                dutyCache[territoryId] = new DutyData
                {
                    TerritoryId = territoryId,
                    DutyName = duty.Name.ToString(),
                    ContentType = duty.ContentType.RowId
                };
            }
        }

        // Cache TerritoryType data
        var territorySheet = dataManager.GetExcelSheet<TerritoryType>();
        if (territorySheet != null)
        {
            foreach (var territory in territorySheet)
            {
                if (territory.RowId == 0)
                    continue;

                territoryCache[(ushort)territory.RowId] = territory.PlaceName.Value.Name.ToString();
            }
        }
    }

    public string GetDutyName(ushort territoryId)
    {
        if (dutyCache.TryGetValue(territoryId, out var data))
            return data.DutyName;

        if (territoryCache.TryGetValue(territoryId, out var name))
            return name;

        return $"Unknown Duty ({territoryId})";
    }

    public DutyData? GetDutyData(ushort territoryId)
    {
        dutyCache.TryGetValue(territoryId, out var data);
        return data;
    }

    public bool IsDuty(ushort territoryId)
    {
        return dutyCache.ContainsKey(territoryId);
    }

    public IEnumerable<DutyData> GetAllDuties()
    {
        return dutyCache.Values;
    }

    public IEnumerable<DutyData> SearchDuties(string query)
    {
        var lowerQuery = query.ToLowerInvariant();
        foreach (var data in dutyCache.Values)
        {
            if (data.DutyName.ToLowerInvariant().Contains(lowerQuery))
                yield return data;
        }
    }
}

/// <summary>
/// Cached duty data.
/// </summary>
public class DutyData
{
    public ushort TerritoryId { get; set; }
    public string DutyName { get; set; } = string.Empty;
    public uint ContentType { get; set; }
}
