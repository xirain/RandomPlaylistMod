## RandomPlaylistMod v2.1.0 Release Notes

### 🎯 Target: Beat Saber 1.44
- Built and tested against **Beat Saber 1.44.0**. Development continues on 1.44 (1.40 is no longer maintained).
- Runtime dependencies (manifest `dependsOn`): BSIPA `^4.1.0`, SiraUtil `^3.0.0`, SongCore `^3.16.0`, BeatSaberMarkupLanguage `^1.6.0`, SongDetailsCache `^1.0.0`, PlaylistManager `^1.0.0`.
- **PlaylistManager 需 1.44 兼容版本**（推荐使用 1.44 适配 fork `xirain/PlaylistManager@1.44`）。

### 🎮 New: In-Gameplay B Button Controls (手柄/控制器)
- **短按 B（或左手 Y）**：把当前正在播放的歌曲收藏到 `RandomPlaylist Favorites` 歌单（写入 Beat Saber 的 PlaylistManager 共享歌单目录 `.../Playlists/RandomPlaylist_Favorites.bplist`）。
- **长按 B 约 0.7 秒**：直接退出当前随机会话。
- 基于 Unity XR `InputDevices` + `CommonUsages.secondaryButton` 检测，后端无关 —— Pico / Index / Quest 等 OpenXR 串流手柄均可使用。
- 新增 `GameplayFavoriteInput`（关卡场景内挂载）与 `FavoriteManager`（收藏逻辑，依赖 PlaylistManager 的 `BeatSaberPlaylistsLib`）。

### 🛠️ Fix: 收藏/退出提示可见性
- 原提示用 `WorldSpace` Canvas 挂在相机下，被 Beat Saber gameplay 的 HUD（分数/能量条，`ScreenSpaceOverlay` 层）完全遮挡，导致"已收藏"其实成功却看不到。
- 改为 `ScreenSpaceOverlay` + `sortingOrder = 1000`（与 HUD 同层且置顶），现在短按收藏会明确在屏幕上方弹出「★ 已收藏「曲名」」，长按退出弹出「已退出随机会话」。

### 🛠️ Fix: 长按 B 抖动导致几乎不触发退出
- OpenXR 手柄按住 B 期间 `secondaryButton` 状态会抖动（部分帧轮询为 false），旧逻辑每次抖动伪边沿都重置长按计时，导致 0.7s 阈值永远累积不到，长按几乎不触发。
- 修复：长按计时仅在"全新一次按下"时启动，抖动伪边沿不再打断累计；短按加 4 帧（约 0.066s）防抖，避免按住期间抖动误触发收藏。

### 🧹 Code Quality
- 更新 `.gitignore`，排除 sibling mod 源码目录（`PlaylistManager/`、`BeatSaber_BetterSongSearch/` 等）、构建/部署日志与本地工具目录。
- 版本号 2.0.0 → 2.1.0（manifest + csproj 同步）。

### 📝 Contributors
- @xirain

---
**Installation**: Copy `RandomPlaylistMod.dll` and `manifest.json` to `Beat Saber/Plugins/RandomPlaylistMod/`. Make sure PlaylistManager (1.44-compatible), SongCore, SiraUtil, BSML and SongDetailsCache are installed.
