## RandomPlaylistMod v2.0.0 Release Notes

### 🎯 Target: Beat Saber 1.44
- v2.0 is built and tested against **Beat Saber 1.44.0**. Development continues on 1.44 (1.40 is no longer maintained).
- Runtime dependencies (manifest `dependsOn`): BSIPA `^4.1.0`, SiraUtil `^3.0.0`, SongCore `^3.16.0`, BeatSaberMarkupLanguage `^1.6.0`, SongDetailsCache `^1.0.0`, PlaylistManager `^1.0.0`.
- **PlaylistManager 需 1.44 兼容版本**（推荐使用 1.44 适配 fork `xirain/PlaylistManager@1.44`）。

### 🚀 New: Beat Saber 1.44 Compatibility (core startup fix)
- **Problem fixed**: On 1.44, clicking **Start Session** froze / immediately ended because `MenuTransitionsHelper.StartStandardLevel` was called with a non-null `beatmapLevelData` while `_beatmapLevelsModel` is injected (the two are mutually exclusive in `GameplayCoreSceneSetupData`).
- **New behavior**: `PlaySessionManager.StartLevel` now passes `beatmapLevelData = null` and lets the game load via `BeatmapLevelsModel` — the standard menu-launch path. Custom songs **and** OST official songs both work.
- Removed the now-unneeded `IBeatmapLevelLoader`/`ResolveBeatmapLevelData` workaround that masked the real exception.

### 📊 New: Session Summary (end-of-session info display)
- When a session ends (time up or **End Session**), a **Session Summary** screen shows the results: total songs played, total play time, score/exercise summary, and the list of songs played.
- Includes `ExerciseSummary` / `PlayerProfile` / `SessionRecord` / `SongResult` models backing the summary.

### 🕘 New: Session History
- Added a **History** view listing past sessions, so you can review what you played and how each session went.
- Backed by `HistoryManager` (session records persisted) and `HistoryView`.

### 🖼️ New: Share Image Generator
- `ShareImageGenerator` produces a shareable summary image of a finished session (for social sharing).

### 🧹 Code Quality
- Removed an unused `BS_Utils` project reference (no code usage).
- Updated `manifest.json` author to `xirain` and `gameVersion` to `1.44.0`.

### 📝 Contributors
- @xirain

---
**Installation**: Copy `RandomPlaylistMod.dll` and `manifest.json` to `Beat Saber/Plugins/RandomPlaylistMod/`. Make sure PlaylistManager (1.44-compatible), SongCore, SiraUtil, BSML and SongDetailsCache are installed.
