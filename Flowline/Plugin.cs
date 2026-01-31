using Dalamud.Game.Command;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using Flowline.Commands;
using Flowline.Configuration;
using Flowline.Data;
using Flowline.Services;
using Flowline.UI;

namespace Flowline;

/// <summary>
/// Main plugin class for Flowline.
/// </summary>
public sealed class Plugin : IDalamudPlugin
{
    public string Name => "Flowline";

    // Services
    private readonly ConfigurationManager configManager;
    private readonly ActionDataService actionDataService;
    private readonly DutyDataService dutyDataService;
    private readonly TimelinePlaybackService playbackService;
    private readonly DutyDetectionService dutyDetectionService;
    private readonly CountdownService countdownService;
    private readonly ActionRecorderService recorderService;

    // UI
    private readonly WindowSystem windowSystem;
    private readonly TimelineOverlay timelineOverlay;
    private readonly ConfigurationWindow configWindow;
    private readonly TimelineEditorWindow editorWindow;
    private readonly RecordingReviewWindow reviewWindow;

    // Commands
    private readonly FlowlineCommands commands;

    // Dalamud services (injected via constructor)
    private readonly IDalamudPluginInterface pluginInterface;
    private readonly IFramework framework;
    private readonly IClientState clientState;
    private readonly IChatGui chatGui;
    private readonly IPartyList partyList;
    private readonly IDataManager dataManager;
    private readonly ITextureProvider textureProvider;
    private readonly ICommandManager commandManager;
    private readonly ICondition condition;
    private readonly IPluginLog pluginLog;
    private readonly IObjectTable objectTable;

    public Plugin(
        IDalamudPluginInterface pluginInterface,
        IFramework framework,
        IClientState clientState,
        IChatGui chatGui,
        IPartyList partyList,
        IDataManager dataManager,
        ITextureProvider textureProvider,
        ICommandManager commandManager,
        ICondition condition,
        IPluginLog pluginLog,
        IObjectTable objectTable)
    {
        this.pluginInterface = pluginInterface;
        this.framework = framework;
        this.clientState = clientState;
        this.chatGui = chatGui;
        this.partyList = partyList;
        this.dataManager = dataManager;
        this.textureProvider = textureProvider;
        this.commandManager = commandManager;
        this.condition = condition;
        this.pluginLog = pluginLog;
        this.objectTable = objectTable;

        // Initialize configuration
        configManager = new ConfigurationManager(pluginInterface, pluginLog);

        // Initialize data services
        actionDataService = new ActionDataService(dataManager);
        dutyDataService = new DutyDataService(dataManager);

        // Initialize core services
        playbackService = new TimelinePlaybackService();
        dutyDetectionService = new DutyDetectionService(clientState, configManager, playbackService);
        countdownService = new CountdownService(chatGui, playbackService, configManager.Configuration);
        recorderService = new ActionRecorderService(chatGui, clientState, objectTable, partyList, pluginInterface, pluginLog, configManager.Configuration);

        // Initialize UI windows
        windowSystem = new WindowSystem("Flowline");
        editorWindow = new TimelineEditorWindow(configManager, dutyDataService, actionDataService);
        editorWindow.SetTextureProvider(textureProvider);
        configWindow = new ConfigurationWindow(configManager, dutyDataService, editorWindow);
        timelineOverlay = new TimelineOverlay(playbackService, actionDataService, configManager, textureProvider, condition, configWindow, objectTable, clientState, recorderService);
        timelineOverlay.SetDutyDataService(dutyDataService);
        timelineOverlay.SetCountdownService(countdownService);
        reviewWindow = new RecordingReviewWindow(recorderService, actionDataService, dutyDataService, configManager);

        // Add windows to window system
        windowSystem.AddWindow(configWindow);
        windowSystem.AddWindow(editorWindow);
        windowSystem.AddWindow(reviewWindow);
        windowSystem.AddWindow(timelineOverlay);

        // Initialize commands
        commands = new FlowlineCommands(
            commandManager,
            pluginLog,
            timelineOverlay,
            configWindow,
            editorWindow,
            reviewWindow,
            playbackService,
            recorderService
        );

        // Subscribe to framework events
        framework.Update += OnFrameworkUpdate;
        pluginInterface.UiBuilder.Draw += windowSystem.Draw;
        pluginInterface.UiBuilder.OpenConfigUi += OnOpenConfigUi;
        pluginInterface.UiBuilder.OpenMainUi += OnOpenMainUi;

        // Set up event handlers for automatic recording
        dutyDetectionService.DutyEntered += OnDutyEntered;
        dutyDetectionService.DutyExited += OnDutyExited;
    }

    private void OnFrameworkUpdate(IFramework framework)
    {
        // Update timeline playback
        playbackService.Update();
    }

    private void OnOpenConfigUi()
    {
        configWindow.Toggle();
    }

    private void OnOpenMainUi()
    {
        timelineOverlay.Toggle();
    }

    private void OnDutyEntered(Timeline timeline)
    {
        // Optionally start auto-recording
        if (configManager.Configuration.RecordingMode == RecordingMode.Automatic ||
            configManager.Configuration.RecordingMode == RecordingMode.Both)
        {
            if (configManager.Configuration.AutoRecordInConfiguredDuties)
            {
                recorderService.StartAutomaticRecording(timeline.TerritoryId);
            }
        }
    }

    private void OnDutyExited()
    {
        // Stop recording if it was automatic
        if (recorderService.IsRecording)
        {
            recorderService.StopRecording();
        }
    }

    public void Dispose()
    {
        // Unsubscribe from events
        framework.Update -= OnFrameworkUpdate;
        pluginInterface.UiBuilder.Draw -= windowSystem.Draw;
        pluginInterface.UiBuilder.OpenConfigUi -= OnOpenConfigUi;
        pluginInterface.UiBuilder.OpenMainUi -= OnOpenMainUi;
        dutyDetectionService.DutyEntered -= OnDutyEntered;
        dutyDetectionService.DutyExited -= OnDutyExited;

        // Dispose services
        commands.Dispose();
        dutyDetectionService.Dispose();
        countdownService.Dispose();
        recorderService.Dispose();
        playbackService.Dispose();
        configManager.Dispose();
        timelineOverlay.Dispose();
        configWindow.Dispose();
        editorWindow.Dispose();
        reviewWindow.Dispose();

        windowSystem.RemoveAllWindows();
    }
}
