
# RandomPlaylistMod

A Beat Saber mod that allows users to set a specific duration and randomly select songs from multiple playlists for continuous play.

## Features

- **Duration Settings**: Set custom play session duration (15 minutes, 30 minutes, 1 hour, or custom)
- **Playlist Selection**: Select multiple playlists to include in your random session
- **NPS Speed Filter**: Filter songs by Notes Per Second, roughly mapping to Hard/Expert difficulty (powered by SongDetailsCache)
- **Smart Random Algorithm**: Intelligent song selection that avoids repeating artists consecutively
- **Seamless Song Switching**: Automatically transitions to the next song when one finishes
- **Time Management**: Tracks elapsed time and remaining time during sessions

## Installation

### Requirements

- Beat Saber 1.29.4 or later
- BSIPA 4.1.4 or later
- SiraUtil 3.0.0 or later
- SongCore 3.9.3 or later
- BeatSaberMarkupLanguage 1.6.3 or later
- PlaylistCore 1.1.0 or later
- BS_Utils 1.11.0 or later

### Manual Installation

1. Download the latest release from GitHub
2. Extract the zip file
3. Copy `RandomPlaylistMod.dll` to your Beat Saber `Plugins` folder
4. Launch Beat Saber

## Usage

1. In the main menu, click the "Random Playlist" button
2. Select your desired play duration (preset options or custom)
3. Check the playlists you want to include
4. Click "Start Session" to begin

## Building from Source

### Prerequisites

- .NET Framework 4.7.2 SDK
- Visual Studio 2022 or Rider
- Beat Saber game installation

### Setup

1. Clone the repository
2. Open the solution in Visual Studio or Rider
3. Set the `BeatSaberDir` environment variable to your Beat Saber installation path
4. Build the project

## Configuration

The mod does not require any configuration files. All settings are managed through the in-game UI.

## Troubleshooting

### Issues

- **Mod not loading**: Ensure all dependencies are installed and up to date
- **No playlists showing**: Make sure PlaylistCore is installed and playlists exist in your `Playlists` folder
- **Songs not playing**: Check that SongCore is properly installed and songs are correctly formatted

### Logs

Logs can be found in `Beat Saber\Logs\_latest.log`. If you encounter issues, include this file when reporting bugs.

## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## Acknowledgments

- Thanks to the Beat Saber Modding Group for their documentation and support
- Inspired by existing mods like PlaylistManager and Shaffuru
