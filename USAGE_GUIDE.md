# Flowline Usage Guide

## Quick Start

1. Build and install the plugin (see BUILD_INSTRUCTIONS.md)
2. In FFXIV, type `/flowline` to open the configuration
3. Go to the "Timelines" tab and click "Create New Timeline"
4. Configure your timeline and save it
5. Enter the duty you configured
6. When someone uses `/countdown`, the timeline will start automatically

## Creating Your First Timeline

### Step 1: Open the Timeline Editor

Type `/flowline editor` in-game to open the timeline editor.

### Step 2: Set Basic Information

- **Name**: Give your timeline a descriptive name (e.g., "P1S Mitigation Plan")
- **Duty**: Select the duty from the dropdown
- **Duration**: Set the total length of the encounter in seconds

### Step 3: Add Action Markers

1. Click "Add New Marker" to expand the add section
2. Set the timestamp (in seconds) when the action should appear
3. Optionally enter the player name who should use this action
4. Search for the action by name in the search box
5. Click on the action from the search results to add it to the timeline

### Step 4: Save the Timeline

Click "Save Timeline" to save your configuration. The timeline will now be active when you enter that duty.

## Display Modes

Flowline supports three display modes:

### Horizontal Scroll (Default)
- Actions flow from right to left like a rhythm game
- Red line indicates the "hit" point (current time)
- Actions appear from the right and flow toward the indicator

### Vertical List
- Shows upcoming actions as a vertical list
- Each action displays its countdown timer
- Simple and easy to read

### Vertical Scroll
- Actions flow from bottom to top
- Similar to horizontal but oriented vertically
- Good for narrow screen spaces

Change the display mode in `/flowline` > Display tab.

## Display Options

You can configure what information is shown for each action:

- **Show Action Icons**: Display the game's icon for each action
- **Show Action Names**: Display the text name of the action
- **Show Player Names**: Show which player should use the action
- **Show Countdown Timer**: Display time remaining until the action

All options are configurable via checkboxes in the Display tab.

## Using Timelines in Combat

### Automatic Start (Recommended)

1. Ensure "Auto-start on Countdown" is enabled (Display tab)
2. Enter a duty with a configured timeline
3. When someone uses `/countdown` or the duty pull timer starts, the timeline begins automatically

### Manual Start

Use `/flowline start` to manually start the timeline at any time.

### Manual Control

- `/flowline pause` - Pause the timeline
- `/flowline resume` - Resume from pause
- `/flowline stop` - Stop and reset the timeline

## Recording Encounters

Recording allows you to capture what happened during an actual encounter and review it later.

### Recording Modes

Set in `/flowline` > Recording tab:

- **Manual**: Use `/flowline record` to start/stop recording
- **Automatic**: Automatically records when entering configured duties
- **Both**: Supports both manual and automatic recording

### Recording a Run

**Automatic Mode:**
1. Enable "Auto-record in Configured Duties"
2. Enter a duty with a configured timeline
3. Recording starts automatically
4. Complete the encounter
5. Recording stops when you leave the duty

**Manual Mode:**
1. Type `/flowline record` to start recording
2. Complete your encounter
3. Type `/flowline record` again to stop

### Reviewing Recordings

1. Type `/flowline recordings` to open the review window
2. Click on a recording to view details
3. See all actions from all party members (if enabled)
4. Filter by player name to focus on specific players

### Converting Recordings to Timelines

1. Select a recording in the review window
2. Click "Convert to Timeline"
3. A new timeline will be created based on the recorded actions
4. Edit the timeline as needed in the timeline editor

## Configuration Options

### Display Settings

- **Display Mode**: Choose horizontal, vertical list, or vertical scroll
- **Opacity**: Adjust overlay transparency (0.1 to 1.0)
- **Lock Overlay Position**: Prevent accidental movement of the overlay
- **Look Ahead**: How many seconds into the future to display (5-30 seconds)
- **Action Display Duration**: How long actions remain visible after their timestamp

### Recording Settings

- **Recording Mode**: Manual, automatic, or both
- **Record Party Actions**: Include actions from all party members
- **Auto-record in Configured Duties**: Automatically start recording in duties with timelines
- **Max Recordings Per Duty**: Auto-delete old recordings (0 = keep all)

## Tips and Best Practices

### Creating Effective Timelines

1. **Focus on Critical Actions**: Don't add every ability, only important cooldowns
2. **Use Player Names**: Assign specific actions to specific players for clarity
3. **Test and Refine**: Record actual runs and adjust your timelines based on reality
4. **Share with Your Team**: Export timelines as JSON and share with party members

### Positioning the Overlay

1. Disable "Lock Overlay Position" in configuration
2. Drag the overlay to your preferred location
3. Resize it to fit your screen layout
4. Enable "Lock Overlay Position" to prevent accidental movement

### Recording Strategy

1. **Record First**: Do a few runs with recording enabled
2. **Review Patterns**: Look for consistent timing in your recordings
3. **Create Timeline**: Convert the best recording to a timeline
4. **Refine**: Edit the timeline to remove unnecessary actions
5. **Test**: Use the timeline in actual combat and adjust as needed

### Managing Multiple Timelines

- Create separate timelines for different strategies (e.g., "P1S Week 1" vs "P1S Farm")
- Disable timelines you're not currently using
- Use descriptive names to easily identify timelines

## Keyboard Shortcuts

Currently, there are no default keyboard shortcuts. You can use commands:
- `/flowline` - Quick access to configuration
- `/flowline record` - Quick recording toggle

## Troubleshooting

### Timeline doesn't appear in duty

- Verify the timeline is enabled (green checkbox in Timelines tab)
- Check that the Territory ID matches your current duty
- Try `/flowline start` to manually start

### Countdown doesn't trigger timeline

- Ensure "Auto-start on Countdown" is enabled
- The plugin looks for "Battle commencing in X seconds!" messages
- Language must be set to English (other languages not yet supported)

### Actions not recording

- Chat-based recording only captures actions that appear in combat log
- Some abilities (especially oGCDs) may not appear in chat
- Enable "Record Party Actions" to capture team actions
- Future versions may use hooks for more accurate tracking

### Overlay is too transparent

- Increase opacity in Display tab
- The overlay uses the configured alpha value

### Performance issues

- Reduce "Look Ahead" time to show fewer actions
- Disable action icons if experiencing lag
- Use "Vertical List" mode which is more performant

## Advanced Usage

### Manual Timeline JSON Editing

Timelines are stored as JSON files in:
`%APPDATA%\XIVLauncher\pluginConfigs\Flowline\timelines\`

You can edit these files directly with a text editor:

```json
{
  "Id": "unique-guid-here",
  "Name": "My Timeline",
  "TerritoryId": 968,
  "DurationSeconds": 660.0,
  "Markers": [
    {
      "TimestampSeconds": 30.0,
      "ActionId": 7533,
      "PlayerName": "Tank Name",
      "JobId": 32,
      "IconId": 2901,
      "CustomLabel": "Use mitigation"
    }
  ],
  "IsEnabled": true,
  "Description": "Notes about this timeline"
}
```

### Finding Action IDs

1. Use the in-game search in the timeline editor
2. Or reference community resources for action ID lists
3. Action IDs are the same as in the game's Excel sheets

### Sharing Timelines

1. Export timeline from the Timelines tab
2. Share the JSON file with your team
3. Others can import it using "Import Timeline"

## Limitations

### Current Limitations

- **Language Support**: Countdown detection only works with English client
- **Action Detection**: Chat-based recording misses some abilities
- **No Audio Cues**: Visual-only alerts (audio may be added later)
- **Single Timeline Per Duty**: Can only have one active timeline per territory

### Planned Features

- Hook-based action tracking for accurate recording
- Multi-language countdown detection
- Audio alerts for upcoming actions
- Timeline templates library
- Better action search with icons in editor
- Drag-and-drop timeline editing
- Timeline preview/playback in editor

## Getting Help

If you encounter issues:
1. Check the troubleshooting section above
2. Enable Debug Mode in Advanced tab to see detailed information
3. Check the Dalamud log (`/xllog`) for error messages

## Examples

See `example_timeline.json` in the plugin directory for a sample timeline configuration.
