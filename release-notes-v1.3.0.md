## RandomPlaylistMod v1.3.0 Release Notes

### ✅ New: Smart Difficulty Selection (NPS Matching)
- **Problem fixed**: Songs were filtered by NPS range, but the easiest difficulty was always selected for playback — not matching the filter intent.
- **New behavior**: `SelectBestDifficulty()` now selects the **hardest difficulty within the NPS range**. If no difficulty matches, falls back to the hardest available difficulty.
- Uses `SongDetailsCache` to query per-difficulty NPS data.

### 🎨 VR HUD Fix (SessionHUDView)
- **Problem fixed**: HUD was invisible in VR because `ScreenSpaceOverlay` does not render in VR.
- **New behavior**: HUD now uses `WorldSpace` Canvas, positioned 2.5m in front of the player's view, 0.35m below eye level.
- Canvas is parented to the VR camera and follows head movement.
- Removed non-existent `renderOnTopOfEverything` property that caused compile errors.
- Removed unnecessary `FixTextShader` method (not needed for WorldSpace).

### 🔧 Code Quality
- Fixed `MapDifficulty` → `BeatmapDifficulty` cast: replaced incorrect `Enum.TryParse` with direct cast.
- Added `GetSongDetails()` static method to cache `SongDetails` instance.
- Cleaned up unused `SongCore` using directive.

### 📝 Contributors
- @xirain

---
**Installation**: Copy `RandomPlaylistMod.dll` and `manifest.json` to `Beat Saber/Plugins/RandomPlaylistMod/`
