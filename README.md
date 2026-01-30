# Flowline - FFXIV Timeline Overlay Plugin

A Dalamud plugin that displays a rhythm-game style timeline overlay for FFXIV encounters, showing when specific actions should be used during duty encounters.

## Features

- **Timeline Overlay**: Displays configured actions at specific timestamps during encounters
- **Multiple Display Modes**: Horizontal scrolling (rhythm game style), vertical list, or vertical scrolling
- **Automatic Duty Detection**: Automatically loads timelines when entering configured duties
- **Pull Timer Integration**: Starts timeline when in-game countdown begins
- **Action Recording**: Record your runs to review and convert to timelines
- **Party Tracking**: Record actions from all party members
- **Fully Configurable**: Customize what information is displayed (icons, names, countdowns)
- **JSON Import/Export**: Share timelines with others or edit them externally

## Building the Plugin

### Prerequisites

1. Install [Visual Studio 2022](https://visualstudio.microsoft.com/) (Community edition is fine)
2. Install the ".NET Desktop Development" workload
3. Install FFXIV with Dalamud (via XIVLauncher)

### Build Steps

1. Open a terminal in the `Flowline` directory
2. Run: `dotnet build`
3. The compiled plugin will be in `bin/Debug/net8.0-windows/`

### Installing for Development

1. Build the plugin
2. In Dalamud settings, go to: `Experimental > Dev Plugin Locations`
3. Add the path to your `Flowline/bin/Debug/net8.0-windows/` folder
4. The plugin will hot-reload when you rebuild

## Usage

### Commands

- `/flowline` - Open configuration window
- `/flowline editor` - Open timeline editor
- `/flowline recordings` - View recorded encounters
- `/flowline start` - Manually start current timeline
- `/flowline stop` - Stop current timeline
- `/flowline pause` - Pause current timeline
- `/flowline resume` - Resume paused timeline
- `/flowline record` - Toggle recording on/off

### Creating a Timeline

1. Use `/flowline editor` to open the timeline editor
2. Click "Create New Timeline"
3. Enter a name and select the duty
4. Set the total duration
5. Search for actions and add them at specific timestamps
6. Save the timeline

### Using Timelines in Duties

1. Create and save a timeline for a specific duty
2. Enter that duty
3. When someone uses `/countdown`, the timeline will automatically start
4. The overlay will show upcoming actions based on your configured display mode

### Recording Encounters

1. Configure recording mode in `/flowline` > Recording tab
2. Enter a duty (automatic mode) or use `/flowline record` (manual mode)
3. Complete the encounter
4. Review recordings in `/flowline recordings`
5. Convert recordings to timelines for future use

## Configuration

All settings are accessible via `/flowline`:

### Display Tab
- **Display Mode**: Choose between horizontal scroll, vertical list, or vertical scroll
- **Display Options**: Toggle icons, action names, player names, and countdown timers
- **Overlay Settings**: Adjust opacity and lock position
- **Timeline Settings**: Configure look-ahead time and auto-start behavior

### Timelines Tab
- View all configured timelines
- Enable/disable individual timelines
- Edit, export, or delete timelines
- Import timelines from JSON files

### Recording Tab
- Set recording mode (manual, automatic, or both)
- Enable party action recording
- Configure auto-cleanup of old recordings

## File Locations

Configuration and data files are stored in:
- `%APPDATA%\XIVLauncher\pluginConfigs\Flowline\`
- Timelines: `timelines/` subdirectory
- Recordings: `recordings/` subdirectory

## Troubleshooting

### Timeline doesn't start on countdown
- Verify the timeline is enabled in configuration
- Check that you're in the correct duty
- Ensure "Auto-start on Countdown" is enabled

### Icons not showing
- Make sure "Show Action Icons" is enabled
- Verify the action IDs are correct in your timeline
- Check that Dalamud has loaded game data

### Recording isn't working
- Enable debug mode to see if actions are being detected
- Chat-based recording only captures actions that appear in combat log
- Not all abilities appear in chat (some oGCDs may be missed)

## Future Enhancements

- Hook-based action tracking for more accurate recording
- Multi-language support for countdown detection
- Timeline sharing/import from URL
- Visual timeline scrubber in editor
- Audio cues for upcoming actions
- Job-specific color coding

## Technical Details

This plugin uses the following Dalamud services:
- `IClientState` - Territory/duty detection
- `IChatGui` - Countdown and action detection
- `IFramework` - Timeline progression
- `IDataManager` - Action and duty data
- `ITextureProvider` - Action icon loading
- `IPartyList` - Party member tracking
- `ICondition` - Combat state tracking

## License

This plugin is provided as-is for personal use with FFXIV via Dalamud.
