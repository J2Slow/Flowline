using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using Newtonsoft.Json;

namespace Flowline.Configuration;

/// <summary>
/// Manages configuration persistence and timeline JSON files.
/// </summary>
public class ConfigurationManager : IDisposable
{
    private readonly IDalamudPluginInterface pluginInterface;
    private readonly IPluginLog pluginLog;
    private readonly string timelinesDirectory;
    private FlowlineConfiguration? configuration;
    private readonly Dictionary<Guid, Timeline> timelines = new();

    public FlowlineConfiguration Configuration => configuration ??= LoadConfiguration();

    public IReadOnlyDictionary<Guid, Timeline> Timelines => timelines;

    public ConfigurationManager(IDalamudPluginInterface pluginInterface, IPluginLog pluginLog)
    {
        this.pluginInterface = pluginInterface;
        this.pluginLog = pluginLog;

        // Create timelines directory if it doesn't exist
        timelinesDirectory = Path.Combine(
            pluginInterface.ConfigDirectory.FullName,
            "timelines"
        );

        Directory.CreateDirectory(timelinesDirectory);

        // Load configuration and timelines
        LoadConfiguration();
        LoadAllTimelines();
    }

    private FlowlineConfiguration LoadConfiguration()
    {
        var config = pluginInterface.GetPluginConfig() as FlowlineConfiguration;
        return config ?? new FlowlineConfiguration();
    }

    public void SaveConfiguration()
    {
        if (configuration != null)
        {
            pluginInterface.SavePluginConfig(configuration);
        }
    }

    private void LoadAllTimelines()
    {
        timelines.Clear();

        if (!Directory.Exists(timelinesDirectory))
            return;

        foreach (var file in Directory.GetFiles(timelinesDirectory, "*.json"))
        {
            try
            {
                var json = File.ReadAllText(file);
                var timeline = JsonConvert.DeserializeObject<Timeline>(json);

                if (timeline != null)
                {
                    timelines[timeline.Id] = timeline;
                }
            }
            catch (Exception ex)
            {
                // Log error but continue loading other timelines
                pluginLog.Error($"Failed to load timeline from {file}: {ex.Message}");
            }
        }
    }

    public void SaveTimeline(Timeline timeline)
    {
        timelines[timeline.Id] = timeline;

        var fileName = $"{SanitizeFileName(timeline.Name)}_{timeline.Id}.json";
        var filePath = Path.Combine(timelinesDirectory, fileName);

        var json = JsonConvert.SerializeObject(timeline, Formatting.Indented);
        File.WriteAllText(filePath, json);

        // Update configuration timeline IDs if needed
        if (!Configuration.TimelineIds.Contains(timeline.Id))
        {
            Configuration.TimelineIds.Add(timeline.Id);
            SaveConfiguration();
        }
    }

    public void DeleteTimeline(Guid id)
    {
        if (!timelines.TryGetValue(id, out var timeline))
            return;

        timelines.Remove(id);
        Configuration.TimelineIds.Remove(id);
        SaveConfiguration();

        // Delete JSON file
        var files = Directory.GetFiles(timelinesDirectory, $"*_{id}.json");
        foreach (var file in files)
        {
            File.Delete(file);
        }
    }

    public Timeline? GetTimelineForTerritory(ushort territoryId)
    {
        return timelines.Values
            .FirstOrDefault(t => t.TerritoryId == territoryId && t.IsEnabled);
    }

    public Timeline? GetTimeline(Guid id)
    {
        timelines.TryGetValue(id, out var timeline);
        return timeline;
    }

    public Timeline ImportTimelineFromFile(string filePath)
    {
        var json = File.ReadAllText(filePath);
        var timeline = JsonConvert.DeserializeObject<Timeline>(json);

        if (timeline == null)
            throw new InvalidOperationException("Failed to deserialize timeline from file");

        // Generate new ID to avoid conflicts
        timeline.Id = Guid.NewGuid();
        SaveTimeline(timeline);

        return timeline;
    }

    public void ExportTimelineToFile(Guid id, string filePath)
    {
        if (!timelines.TryGetValue(id, out var timeline))
            throw new InvalidOperationException($"Timeline {id} not found");

        var json = JsonConvert.SerializeObject(timeline, Formatting.Indented);
        File.WriteAllText(filePath, json);
    }

    private static string SanitizeFileName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        return string.Join("_", name.Split(invalid, StringSplitOptions.RemoveEmptyEntries));
    }

    public void Dispose()
    {
        SaveConfiguration();
    }
}
