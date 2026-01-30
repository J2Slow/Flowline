# Flowline Project Overview

## What is Flowline?

Flowline is a Dalamud plugin for FFXIV that displays a rhythm-game style timeline overlay during duty encounters. It shows when specific actions should be used, helping players execute coordinated strategies and mitigation plans.

## Project Structure

```
Flowline/
├── README.md                          # Main project documentation
├── BUILD_INSTRUCTIONS.md              # How to build the plugin
├── USAGE_GUIDE.md                     # How to use the plugin
├── PROJECT_OVERVIEW.md                # This file
├── example_timeline.json              # Example timeline configuration
├── .gitignore                         # Git ignore file
├── Flowline.sln                       # Visual Studio solution file
│
└── Flowline/                          # Main plugin project
    ├── Flowline.csproj                # C# project file
    ├── FlowlinePlugin.json            # Dalamud plugin manifest
    ├── Plugin.cs                      # Main plugin entry point
    │
    ├── Configuration/                 # Configuration and data models
    │   ├── ActionMarker.cs           # Individual action on timeline
    │   ├── ConfigurationManager.cs   # Config persistence and JSON handling
    │   ├── FlowlineConfiguration.cs  # Plugin settings
    │   └── Timeline.cs               # Timeline data model
    │
    ├── Services/                      # Core game event services
    │   ├── ActionRecorderService.cs  # Records player/party actions
    │   ├── CountdownService.cs       # Detects pull timer from chat
    │   ├── DutyDetectionService.cs   # Monitors territory changes
    │   └── TimelinePlaybackService.cs # Manages timeline state/progression
    │
    ├── Data/                          # Game data access and recording
    │   ├── ActionDataService.cs      # Access action names/icons
    │   ├── DutyDataService.cs        # Access duty information
    │   ├── RecordedAction.cs         # Single recorded action
    │   └── RecordedEncounter.cs      # Complete recorded encounter
    │
    ├── Rendering/                     # Timeline rendering implementations
    │   ├── ITimelineRenderer.cs      # Renderer interface
    │   ├── HorizontalScrollRenderer.cs # Rhythm-game style display
    │   ├── VerticalListRenderer.cs   # List with countdowns
    │   └── VerticalScrollRenderer.cs # Vertical scrolling display
    │
    ├── UI/                            # ImGui windows
    │   ├── ConfigurationWindow.cs    # Main settings window
    │   ├── RecordingReviewWindow.cs  # Review recorded encounters
    │   ├── TimelineEditorWindow.cs   # Create/edit timelines
    │   └── TimelineOverlay.cs        # Main overlay renderer
    │
    └── Commands/                      # Slash commands
        └── FlowlineCommands.cs       # /flowline command handler
```

## Key Components

### Plugin.cs
Main entry point that:
- Initializes all services via dependency injection
- Subscribes to Dalamud framework events
- Manages plugin lifecycle
- Coordinates between services and UI

### Services Layer

**TimelinePlaybackService**
- State machine for timeline playback (Idle, Running, Paused, etc.)
- Tracks current position in timeline
- Calculates which markers are visible
- Fires events for state changes

**DutyDetectionService**
- Monitors `IClientState.TerritoryChanged` events
- Loads appropriate timeline when entering configured duty
- Unloads timeline when leaving duty

**CountdownService**
- Listens to `IChatGui.ChatMessage` events
- Parses countdown messages ("Battle commencing in X seconds!")
- Triggers timeline start with countdown offset

**ActionRecorderService**
- Records actions during encounters
- Supports manual and automatic recording modes
- Saves recordings as JSON files
- Tracks party member actions

### Data Services

**ActionDataService**
- Caches action data from Lumina Excel sheets
- Provides fast lookup for action names and icon IDs
- Supports action search by name

**DutyDataService**
- Caches duty/territory data
- Provides duty name lookup
- Supports duty search

### Rendering System

**ITimelineRenderer Interface**
- Common interface for all display modes
- Allows switching between renderers at runtime

**Three Renderer Implementations**:
1. **HorizontalScrollRenderer**: Actions flow right-to-left (default)
2. **VerticalListRenderer**: Text list with countdowns
3. **VerticalScrollRenderer**: Actions flow top-to-bottom

Each renderer:
- Loads action icons via `ITextureProvider`
- Draws markers based on configuration flags
- Handles smooth animations

### UI Windows

**TimelineOverlay**
- Main overlay displayed during combat
- Uses configured renderer
- Shows markers from `TimelinePlaybackService`
- Supports repositioning and resizing

**ConfigurationWindow**
- Tabbed interface for all settings
- Display, Timeline, Recording, and Advanced tabs
- Saves configuration on changes

**TimelineEditorWindow**
- Create and edit timelines
- Search and add actions
- Manage markers
- Save to JSON

**RecordingReviewWindow**
- List all recordings
- View recorded actions
- Filter by player
- Convert recordings to timelines

## Data Flow

### Timeline Activation Flow
```
Player enters duty
  → DutyDetectionService detects territory change
  → ConfigurationManager checks for timeline
  → Timeline loaded into TimelinePlaybackService
  → DutyEntered event fired
```

### Timeline Start Flow
```
Player uses /countdown
  → Chat message appears
  → CountdownService parses message
  → Countdown value extracted
  → TimelinePlaybackService starts with offset
  → Overlay begins displaying markers
```

### Frame Update Flow
```
Every frame (IFramework.Update)
  → TimelinePlaybackService.Update() advances time
  → Calculate visible markers based on look-ahead window
  → TimelineOverlay.Draw() called
  → Renderer draws visible markers
```

### Recording Flow
```
Recording starts (auto or manual)
  → ActionRecorderService subscribes to chat
  → Combat actions detected in chat
  → RecordedAction created with timestamp
  → Added to RecordedEncounter
  → On stop, saved to JSON
```

## Configuration Files

### Plugin Configuration
Stored by Dalamud at:
`%APPDATA%\XIVLauncher\pluginConfigs\Flowline.json`

Contains:
- Display preferences
- Overlay position/size
- Recording settings
- Timeline ID references

### Timeline Files
Stored in:
`%APPDATA%\XIVLauncher\pluginConfigs\Flowline\timelines\`

Format: `{TimelineName}_{GUID}.json`

Each timeline contains:
- Duty/territory ID
- Duration in seconds
- List of action markers with timestamps
- Metadata (name, description, enabled state)

### Recording Files
Stored in:
`%APPDATA%\XIVLauncher\pluginConfigs\Flowline\recordings\`

Format: `{RecordingName}_{GUID}.json`

Each recording contains:
- Territory ID and timestamp
- All recorded actions with timestamps
- Party member list
- Clear status

## Dalamud API Integration

### Services Used

- **IClientState**: Territory detection, player info
- **IFramework**: Frame updates for timeline progression
- **IChatGui**: Countdown and action detection via chat
- **IPartyList**: Party member information
- **IDataManager**: Access to game Excel sheets (Action, ContentFinderCondition)
- **ITextureProvider**: Loading action icons
- **ICommandManager**: Registering /flowline commands
- **ICondition**: Combat state tracking
- **IDalamudPluginInterface**: Configuration persistence, UI integration

### Event Subscriptions

- `IClientState.TerritoryChanged` → Duty detection
- `IChatGui.ChatMessage` → Countdown and action detection
- `IFramework.Update` → Timeline progression
- `IUiBuilder.Draw` → Overlay rendering
- `IUiBuilder.OpenConfigUi` → Config window shortcut

## Extending the Plugin

### Adding New Display Modes

1. Create a new class implementing `ITimelineRenderer`
2. Implement the `Render` method with your display logic
3. Add the renderer to `TimelineOverlay.cs`
4. Add enum value to `TimelineDisplayMode`

### Adding Hook-Based Action Tracking

1. Use `IGameInteropProvider` to create hooks
2. Find the signature for `UseAction` or `ActionEffect` functions
3. Replace chat-based tracking in `ActionRecorderService`
4. Capture actual action IDs instead of parsing names

### Supporting Additional Languages

1. Add regex patterns for other languages in `CountdownService`
2. Detect client language via `IClientState.ClientLanguage`
3. Use appropriate patterns based on language

### Adding Audio Alerts

1. Add `ISoundProvider` or similar service
2. Add audio file references to configuration
3. Play sound when marker timestamp is reached
4. Add configuration option to enable/disable audio

## Performance Considerations

### Optimization Points

- **Action Icons**: Cached after first load
- **Game Data**: Cached on plugin initialization
- **Visible Markers**: Only markers in look-ahead window are processed
- **Draw Calls**: Minimized by batching ImGui operations

### Potential Bottlenecks

- Loading many action icons at once
- Very long timelines with hundreds of markers
- Frequent configuration saves

### Best Practices

- Limit look-ahead window to reduce draw calls
- Use icon caching (already implemented)
- Avoid creating timelines with thousands of markers

## Known Limitations

1. **Language-Dependent**: Countdown detection only works with English client
2. **Chat-Based Tracking**: Recording may miss some actions that don't appear in chat
3. **Single Timeline**: Only one timeline can be active per duty
4. **No Persistence**: Timeline state resets on plugin reload

## Future Roadmap

**Priority 1 (Core Improvements):**
- Hook-based action tracking for accurate recording
- Multi-language support
- Better error handling and validation

**Priority 2 (UX Enhancements):**
- Visual timeline scrubber in editor
- Drag-and-drop marker placement
- Action icons in search results
- Timeline preview without entering duty

**Priority 3 (Advanced Features):**
- Audio alerts
- Timeline templates
- Sharing/import from URL
- ACT integration
- Multiple timelines per duty (with switching)

## Development Notes

### Code Style

- Null-safety enabled (`<Nullable>enable</Nullable>`)
- XML documentation on public members
- Service-based architecture for testability
- Events for loose coupling between components

### Testing Strategy

Since Dalamud plugins run in-game:
1. Use hot-reloading for rapid iteration
2. Test in actual duties with countdown timers
3. Use debug mode to verify state
4. Create test timelines with short durations

### Debugging

1. Enable AntiDebug in Dalamud (`/xldev`)
2. Attach Visual Studio debugger to `ffxiv_dx11.exe`
3. Set breakpoints in your code
4. Use `PluginLog` for logging

## Credits

Built using:
- [Dalamud](https://github.com/goatcorp/Dalamud) - Plugin framework
- [ImGui.NET](https://github.com/ImGuiNET/ImGui.NET) - UI rendering
- [Lumina](https://github.com/NotAdam/Lumina) - Game data access
- [Newtonsoft.Json](https://www.newtonsoft.com/json) - JSON serialization

## Contributing

Feel free to:
- Report bugs or request features
- Submit pull requests with improvements
- Share your timelines with the community
- Provide translations for other languages
