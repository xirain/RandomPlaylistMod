# RandomPlaylistMod

A Beat Saber mod that lets you set a target duration and randomly plays songs from multiple playlists continuously.

> **Current Status**: v1.0 early release. Core features are functional, but some issues may exist. Improvements will follow — feedback via Issues is welcome.

## Features

- **Duration Setting**: Presets for 30 min / 1 hr / 2 hr, or custom duration (1–120 min)
- **Multi-Playlist Selection**: Check multiple Playlist Manager playlists as your song pool
- **Smart Randomization**: Fisher-Yates shuffle for true randomness, with automatic song count and duration estimation
- **Continuous Playback**: Auto-advances to the next song; session ends automatically when time runs out
- **Live Progress**: Shows current song name, progress (X/Y), and elapsed time during playback
- **Fault Tolerance**: Skips failed songs automatically without interrupting the session

## Screenshots

> The mod entry is located in the MODS panel on the left side of the main menu.

## Installation

### Prerequisites

| Dependency | Description |
|------------|-------------|
| **Beat Saber 1.40.8** | This mod is built for this version |
| **BSIPA 4.1+** | Mod loading framework |
| **Playlist Manager** | Playlist management mod (must be installed beforehand) |
| **SongCore 3.9+** | Song data management |
| **SiraUtil 3.0+** | Core utility library |
| **BeatSaberMarkupLanguage 1.6+** | UI framework |

### Installation Steps

1. Make sure all prerequisites above are installed
2. Download the latest `RandomPlaylistMod.dll` from the [Releases](../../releases) page
3. Place the DLL file in your Beat Saber `Plugins` folder:
   ```
   <Beat Saber Install Directory>/Plugins/RandomPlaylistMod.dll
   ```
4. Launch the game — the mod will load automatically

## Usage

1. Click the **MODS** button on the left side of the main menu
2. Find the **Random Playlist** panel
3. Select a duration (preset buttons or enter custom minutes)
4. Check the playlists you want in the playlist list
5. Click **Start Session** to begin
6. Click **End Session** at any time to stop early

## Building

Requires .NET Framework 4.8. Set `BeatSaberDir` in the `.csproj` to your Beat Saber install path:

```bash
dotnet build RandomPlaylistMod/RandomPlaylistMod.csproj -c Release
```

Output: `bin/Release/RandomPlaylistMod.dll`

## Project Structure

```
RandomPlaylistMod/
├── Plugin.cs                     # Entry point, Zenject bindings
├── Managers/
│   ├── PlaySessionManager.cs     # Session lifecycle management
│   ├── PlaylistManager.cs        # Playlist loading & selection
│   ├── SongSelector.cs           # Random song selection algorithm
│   └── TimeManager.cs            # Duration & countdown
├── UI/
│   ├── RandomPlaylistUI.cs       # UI controller
│   ├── RandomPlaylistFlowCoordinator.cs
│   └── Views/
│       └── RandomPlaylistView.bsml  # BSML layout
└── Models/
    ├── SongInfo.cs
    ├── PlaylistInfo.cs
    └── PlaySession.cs
```

## Technical Details

- **Framework**: BSIPA 4.1 + Zenject dependency injection
- **UI**: BeatSaberMarkupLanguage (BSML)
- **Playlist Access**: Reads playlists managed by Playlist Manager via BeatSaberPlaylistsLib
- **Song Loading**: Starts levels via SongCore + MenuTransitionsHelper.StartStandardLevel
- **Caching**: Lazy-load + dirty-flag cache for song pool to avoid repeated traversal

## License

[MIT](LICENSE)
