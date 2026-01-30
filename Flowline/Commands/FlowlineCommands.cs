using System;
using Dalamud.Game.Command;
using Dalamud.Plugin.Services;
using Flowline.Services;
using Flowline.UI;

namespace Flowline.Commands;

/// <summary>
/// Handles slash commands for Flowline.
/// </summary>
public class FlowlineCommands : IDisposable
{
    private readonly ICommandManager commandManager;
    private readonly IPluginLog pluginLog;
    private readonly TimelineOverlay timelineOverlay;
    private readonly ConfigurationWindow configWindow;
    private readonly TimelineEditorWindow editorWindow;
    private readonly RecordingReviewWindow reviewWindow;
    private readonly TimelinePlaybackService playbackService;
    private readonly ActionRecorderService recorderService;

    private const string CommandName = "/flowline";
    private const string ConfigCommandName = "/flowlineconfig";

    public FlowlineCommands(
        ICommandManager commandManager,
        IPluginLog pluginLog,
        TimelineOverlay timelineOverlay,
        ConfigurationWindow configWindow,
        TimelineEditorWindow editorWindow,
        RecordingReviewWindow reviewWindow,
        TimelinePlaybackService playbackService,
        ActionRecorderService recorderService)
    {
        this.commandManager = commandManager;
        this.pluginLog = pluginLog;
        this.timelineOverlay = timelineOverlay;
        this.configWindow = configWindow;
        this.editorWindow = editorWindow;
        this.reviewWindow = reviewWindow;
        this.playbackService = playbackService;
        this.recorderService = recorderService;

        commandManager.AddHandler(CommandName, new CommandInfo(OnCommand)
        {
            HelpMessage = "Opens Flowline timeline bar. Use '/flowline help' for more commands."
        });

        commandManager.AddHandler(ConfigCommandName, new CommandInfo(OnConfigCommand)
        {
            HelpMessage = "Opens Flowline configuration window."
        });
    }

    private void OnConfigCommand(string command, string args)
    {
        configWindow.Toggle();
    }

    private void OnCommand(string command, string args)
    {
        var splitArgs = args.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        if (splitArgs.Length == 0)
        {
            timelineOverlay.Toggle();
            return;
        }

        switch (splitArgs[0].ToLowerInvariant())
        {
            case "config":
                configWindow.Toggle();
                break;

            case "editor":
                editorWindow.Toggle();
                break;

            case "recordings":
            case "review":
                reviewWindow.Toggle();
                break;

            case "start":
                playbackService.Start();
                break;

            case "stop":
                playbackService.Stop();
                break;

            case "pause":
                playbackService.Pause();
                break;

            case "resume":
                playbackService.Resume();
                break;

            case "record":
                recorderService.ToggleRecording();
                break;

            case "help":
                PrintHelp();
                break;

            default:
                PrintHelp();
                break;
        }
    }

    private void PrintHelp()
    {
        pluginLog.Information("Flowline Commands:");
        pluginLog.Information("  /flowline - Toggle timeline bar");
        pluginLog.Information("  /flowlineconfig - Open configuration window");
        pluginLog.Information("  /flowline config - Open configuration window");
        pluginLog.Information("  /flowline editor - Open timeline editor");
        pluginLog.Information("  /flowline recordings - Open recording review");
        pluginLog.Information("  /flowline start - Start current timeline");
        pluginLog.Information("  /flowline stop - Stop current timeline");
        pluginLog.Information("  /flowline pause - Pause current timeline");
        pluginLog.Information("  /flowline resume - Resume paused timeline");
        pluginLog.Information("  /flowline record - Toggle recording");
    }

    public void Dispose()
    {
        commandManager.RemoveHandler(CommandName);
        commandManager.RemoveHandler(ConfigCommandName);
    }
}
