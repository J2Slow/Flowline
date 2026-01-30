# Building Flowline

## Prerequisites

### 1. Install .NET 8.0 SDK

Download and install the .NET 8.0 SDK from:
https://dotnet.microsoft.com/download/dotnet/8.0

Verify installation by running:
```bash
dotnet --version
```

### 2. Install Visual Studio 2022 (Recommended)

Download Visual Studio 2022 Community (free):
https://visualstudio.microsoft.com/downloads/

During installation, select the ".NET Desktop Development" workload.

### 3. Install FFXIV with Dalamud

1. Install [XIVLauncher](https://goatcorp.github.io/faq/dalamud)
2. Launch the game through XIVLauncher to install Dalamud
3. Dalamud DLLs should be located at: `%APPDATA%\XIVLauncher\addon\Hooks\dev\`

## Building

### Option 1: Using Visual Studio (Recommended)

1. Open `Flowline.sln` in Visual Studio 2022
2. Build > Build Solution (or press F6)
3. The compiled plugin will be in `Flowline\bin\Debug\net8.0-windows\`

### Option 2: Using Command Line

```bash
cd Flowline
dotnet build
```

The compiled plugin will be in `Flowline\bin\Debug\net8.0-windows\`

## Installing for Development

### Enable Hot-Reloading in Dalamud

1. Launch FFXIV with XIVLauncher
2. Type `/xldev` in-game to open the Dalamud dev menu
3. Go to: Dalamud Settings > Experimental > Dev Plugin Locations
4. Click "Add Dev Plugin Location"
5. Browse to: `C:\Users\Andi-\Documents\Claude\Flowline\Flowline\bin\Debug\net8.0-windows\`
6. The plugin will now load automatically and hot-reload when you rebuild

### Debugging

1. In Visual Studio, set breakpoints in your code
2. Launch FFXIV with XIVLauncher
3. In the Dalamud dev menu (`/xldev`), enable: Dalamud > Enable AntiDebug
4. In Visual Studio: Debug > Attach to Process
5. Select `ffxiv_dx11.exe`
6. Your breakpoints will now hit when the code executes

## Troubleshooting

### "Dalamud.dll not found" Error

Make sure the Dalamud dev DLLs exist at:
`%APPDATA%\XIVLauncher\addon\Hooks\dev\`

If they don't exist, you may need to:
1. Launch FFXIV through XIVLauncher at least once
2. Enable Dalamud in XIVLauncher settings
3. The dev folder should be created automatically

### Build Errors

If you get build errors about missing types or namespaces:
1. Ensure all Dalamud DLLs are present in the dev folder
2. Clean the solution: Build > Clean Solution
3. Rebuild: Build > Rebuild Solution

### Plugin Doesn't Load In-Game

1. Check `/xlplugins` to see if Flowline appears in the installed plugins list
2. Look for errors in the Dalamud log: `/xllog`
3. Verify the plugin DLL exists in the dev plugin location you added
4. Try removing and re-adding the dev plugin location

## Next Steps

After successfully building:
1. Load FFXIV with XIVLauncher
2. Type `/flowline` in-game to open the configuration
3. Create your first timeline using `/flowline editor`
4. Test the plugin in a duty with a countdown timer

## File Locations

Once installed, the plugin stores data in:
- Configuration: `%APPDATA%\XIVLauncher\pluginConfigs\Flowline\`
- Timelines: `%APPDATA%\XIVLauncher\pluginConfigs\Flowline\timelines\`
- Recordings: `%APPDATA%\XIVLauncher\pluginConfigs\Flowline\recordings\`
