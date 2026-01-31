# Flowline - FFXIV Timeline Overlay

A Dalamud plugin that displays a rhythm-game style timeline overlay for FFXIV encounters.

## What it does

Flowline shows you when to use specific actions during duty encounters. Create timelines with action markers at specific timestamps, and the overlay will display upcoming actions as you progress through the fight.

## Features

- **Timeline Overlay**: Displays configured actions with icons at specific timestamps
- **Multiple Display Modes**: Horizontal scrolling, vertical list, or vertical scroll
- **Pull Timer Integration**: Automatically starts when the in-game countdown begins
- **Duty Detection**: Loads the correct timeline when entering configured duties
- **Recording**: Record your runs to review and convert into timelines
- **Import/Export**: Share timelines with others via JSON (copy to clipboard)

## Commands

- `/flowline` - Open configuration window
- `/flowline editor` - Open timeline editor
- `/flowline recordings` - View recorded encounters

## Installation

Add this custom repository URL in Dalamud settings (Experimental > Custom Plugin Repositories):

```
https://raw.githubusercontent.com/J2Slow/Flowline/main/repo.json
```

Then search for "Flowline" in the plugin installer.
