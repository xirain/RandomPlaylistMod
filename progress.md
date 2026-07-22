# RandomPlaylistMod - 进度追踪

> **开发目标（2026-07-19 决定）**：以 Beat Saber **1.44** 为主开发版本。`manifest` 游戏版本号维持 `1.44.0`，API 适配与兼容性修复优先针对 1.44。相关 fork 分支：`xirain/RandomPlaylistMod@1.44`、`xirain/PlaylistManager@1.44`。

## 项目状态：列表选择功能优化 ✅ 已完成

### 背景
基于对列表选择功能的深入分析，识别出以下核心问题：
1. 会话状态无事件推送 → UI 无法实时感知会话进度
2. `GetSongsFromSelectedPlaylists()` 无缓存 → 估算+启动重复全量遍历
3. 列表刷新采用全量重建 → 每次点击 O(n) 重建
4. 会话进行中无实时进度显示
5. 歌曲切换/会话结束无通知
6. TimeManager 缺少会话超时自动终止机制

---

## 任务计划与执行结果

### ✅ Task 1: PlaySessionManager 事件系统
- **状态**: 已完成
- **实现**: 添加 4 个事件
  - `SessionStarted(PlaySession)` — 会话启动时触发
  - `SessionEnded(PlaySession)` — 会话结束时触发（含已播放歌曲数）
  - `SongChanged(SongInfo, int, int)` — 歌曲切换时触发（当前歌、索引、总数）
  - `SongFailed(SongInfo, string)` — 歌曲加载失败时触发（歌、失败原因）
- **修改文件**: `RandomPlaylistMod/Managers/PlaySessionManager.cs`
- **关键决策**: 事件使用 `Action<>` 委托而非自定义 EventArgs，简化实现

### ✅ Task 2: PlaylistManager 歌曲缓存
- **状态**: 已完成
- **实现**:
  - 添加 `_songsCache` 和 `_songsCacheDirty` 标志
  - `InvalidateSongsCache()` 方法标记缓存失效
  - `TogglePlaylistSelection()` / `SelectAllPlaylists()` / `DeselectAllPlaylists()` / `LoadPlaylists()` 均触发缓存失效
  - `GetSongsFromSelectedPlaylists()` 首次调用构建缓存，后续调用直接返回
- **修改文件**: `RandomPlaylistMod/Managers/PlaylistManager.cs`
- **关键决策**: 采用懒加载缓存 + 脏标记模式，而非预计算；选择变更时只设标志不重建

### ✅ Task 3: UI 事件订阅与增量刷新
- **状态**: 已完成
- **实现**:
  - `RandomPlaylistUI.Construct()` 中订阅 4 个事件
  - 添加 `RefreshPlaylistCell(int index)` 增量刷新方法（单行更新）
  - `OnPlaylistClick` 使用增量刷新替代全量重建
  - `OnDestroy()` 取消订阅防止内存泄漏
- **修改文件**: `RandomPlaylistMod/UI/RandomPlaylistUI.cs`
- **技术难点**: BSML 的 CustomListTableData 不支持单行刷新 API，当前仍调用 `ReloadData()` 但只更新 Data[index]；若 BSML 未来支持可进一步优化

### ✅ Task 4: 实时会话进度显示
- **状态**: 已完成
- **实现**:
  - 添加 `SessionUpdateRoutine()` 协程，每 5 秒刷新会话状态
  - 显示格式: `▶ {歌名} | {当前}/{总数} | HH:MM elapsed`
  - `DidActivate()` 恢复进度显示并启动协程
  - `DidDeactivate()` 停止协程（会话仍在后台运行）
  - BSML 视图添加 `word-wrap="true"` 支持长状态文本换行
- **修改文件**: `RandomPlaylistMod/UI/RandomPlaylistUI.cs`, `RandomPlaylistMod/UI/Views/RandomPlaylistView.bsml`
- **关键决策**: 使用 Unity 协程而非静态更新，确保只在 UI 活跃时运行

### ✅ Task 5: 会话超时自动终止
- **状态**: 已完成
- **实现**: `OnSongFinished()` 中添加超时检查
  - 调用 `_timeManager.IsTimeUp(_currentSession.DurationMinutes)`
  - 超时则日志记录并 `EndSession()`
- **修改文件**: `RandomPlaylistMod/Managers/PlaySessionManager.cs`
- **关键决策**: 超时检查仅在歌曲切换时执行（非实时检测），这是合理的因为无法在歌曲播放中途强制退出

### ✅ Task 6: 集成测试与验证
- **状态**: 已完成 (34/34 通过)
- **新增测试**:
  - `PlaySessionManagerTests.cs` (8 个测试): 事件触发、会话启动/结束、歌曲切换、失败跳过、超时终止
  - `PlaylistManagerCacheTests.cs` (9 个测试): 选择切换、全选/全不选、缓存失效、去重
- **修复**: `SongSelectorTests.cs` 中 `PlaylistId` → `Id` 属性名对齐
- **修改文件**: `RandomPlaylistMod.Tests/PlaySessionManagerTests.cs`, `PlaylistManagerCacheTests.cs`, `TestModels/PlaylistInfo.cs`, `TestModels/PlaySession.cs`
- **技术难点**: 主项目依赖 Unity 类型无法直接测试，使用 `TestPlaySessionManager` / `TestPlaylistManager` 简化版模拟核心逻辑

---

## 变更文件清单

| 文件 | 变更类型 | 说明 |
|------|----------|------|
| `Managers/PlaySessionManager.cs` | 修改 | 事件系统 + 超时检查 |
| `Managers/PlaylistManager.cs` | 修改 | 歌曲缓存机制 |
| `UI/RandomPlaylistUI.cs` | 修改 | 事件订阅 + 增量刷新 + 进度协程 |
| `UI/Views/RandomPlaylistView.bsml` | 修改 | 状态文本换行 |
| `Tests/PlaySessionManagerTests.cs` | 新增 | 会话管理事件测试 |
| `Tests/PlaylistManagerCacheTests.cs` | 新增 | 缓存逻辑测试 |
| `Tests/TestModels/PlaylistInfo.cs` | 修改 | 对齐实际模型 |
| `Tests/TestModels/PlaySession.cs` | 修改 | 对齐实际模型 |
| `Tests/SongSelectorTests.cs` | 修改 | 属性名修复 |

---

## 任务计划与执行结果（续）

### ✅ Task 7: BPM 范围筛选
- **状态**: 已完成
- **实现**:
  - `SongInfo` 新增 `BPM` 属性
  - `PlaylistManager.GetSongsFromSelectedPlaylists()` 中从 `level.songBPM` 填充 BPM
  - `SongSelector.SelectSongsForDuration()` 新增 `minBPM`/`maxBPM` 参数，过滤歌曲池
  - `PlaySessionManager` 新增 `MinBPM`/`MaxBPM` 属性，由 UI 设置后传入 Selector
  - `RandomPlaylistUI` 新增 `MinBPM`/`MaxBPM` 属性（`[UIValue("min-bpm")]` / `[UIValue("max-bpm")]`）
  - `RandomPlaylistView.bsml` 新增 BPM 输入行（`increment-setting`）
  - `UpdateEstimates()` 加入 BPM 过滤，`SelectedInfo` 显示 BPM 范围
- **修改文件**: `Models/SongInfo.cs`, `Managers/PlaylistManager.cs`, `Managers/SongSelector.cs`, `Managers/PlaySessionManager.cs`, `UI/RandomPlaylistUI.cs`, `UI/Views/RandomPlaylistView.bsml`

### ✅ Task 8: 实时进度显示修复
- **状态**: 已完成
- **实现**:
  - `SessionUpdateRoutine()` 协程间隔从 5 秒改为 1 秒（`WaitForSeconds(1f)`）
  - `OnSongChanged` 事件触发时立即更新 `SessionStatus`，无需等待协程 tick
  - `StartSession()` 中在调用 `StartSession` 前将 BPM 范围传递到 `PlaySessionManager`
- **修改文件**: `UI/RandomPlaylistUI.cs`
- **关键决策**: 协程间隔 1 秒足以提供"实时"体验，同时不会造成性能压力

---

## 变更文件清单（续）

| 文件 | 变更类型 | 说明 |
|------|----------|------|
| `Models/SongInfo.cs` | 修改 | 新增 BPM 属性 |
| `Managers/PlaylistManager.cs` | 修改 | 填充 SongInfo.BPM |
| `Managers/SongSelector.cs` | 修改 | SelectSongsForDuration 支持 BPM 过滤 |
| `Managers/PlaySessionManager.cs` | 修改 | 新增 MinBPM/MaxBPM，传入 Selector |
| `UI/RandomPlaylistUI.cs` | 修改 | BPM UI 属性 + 协程间隔 1s + 传递 BPM |
| `UI/Views/RandomPlaylistView.bsml` | 修改 | 新增 BPM 输入行 |
| `TODO.md` | 修改 | 标记两项为已完成 |
| `restore_release.ps1` | 新增 | 一键还原 v1.0.0 稳定版脚本 |

---

## 任务计划与执行结果（续 2）

### ✅ Task 9: 修复 BPM 获取方式（编译错误修复）
- **状态**: 已完成
- **问题**: `BeatmapLevel` 没有 `songBPM` 或 `bpm` 属性，导致编译错误 CS1061
- **原因**: `BeatmapLevel` 和 `BeatmapLevelSO` 是不同类，无继承关系；`BeatmapLevelSO.beatsPerMinute` 在 `DataModels.dll` 中但需引用 `BGLib.UnityExtension.dll`
- **实现**:
  - 使用反射从 `BeatmapLevel.beatmapBasicData` 获取 `BeatmapBasicData`
  - 通过反射访问 `BeatmapBasicData.bpm` 属性（若存在）
  - 避免直接引用 `BeatmapLevelSO` 类型，防止编译错误
  - 若反射失败，BPM 默认为 0（不影响非 BPM 筛选场景）
- **修改文件**: `Managers/PlaylistManager.cs`
- **关键决策**: 使用反射而非直接类型引用，确保项目编译通过；BPM 获取失败时静默忽略，保证健壮性

---

## 变更文件清单（续 2）

| 文件 | 变更类型 | 说明 |
|------|----------|------|
| `Managers/PlaylistManager.cs` | 修改 | 使用反射获取 BPM，修复编译错误 |
| `ApiInspector/Program.cs` | 修改 | 更新 API 检查工具，用于排查类型属性 |

---

## 任务计划与执行结果（续 3）

### 🔧 Task 10: UI 布局修复 + 游戏内状态 HUD（进行中）

- **问题 1**: BPM increment-setting 控件在 VR 中无法操作 — 标签文字与 +/- 按钮距离过远，pointer 无法指向按钮区域
- **问题 2**: 会话状态仅在菜单设置页可见，游戏中看不到当前第几手歌曲名

#### 根因分析

**问题 1 根因**:
- BSML `increment-setting` 内部克隆自 `FormattedFloatListSettingsController`（VR Rendering Scale 控件），其 `LayoutElement.preferredWidth` 默认 90，内部按钮区 `sizeDelta = (40, 0)`
- `pref-width` 控制的是整个控件宽度，默认 90 过大导致标签和按钮间距远
- 用户在 VR 中用 pointer 指向按钮区域时，pointer 可能因为 hitbox/间距问题"滑走"
- `increment-setting` 是专为 Beat Saber 内建设置页面设计的窄控件，在 mod 设置面板中布局不同导致交互问题

**问题 2 根因**:
- 当前 `RandomPlaylistUI` 只在菜单的 `FlowCoordinator` 中显示，游戏开始打歌后切到 `GameplayCore` 场景，菜单 UI 完全不可见
- 项目没有 `Location.Game` 安装器，没有任何组件注入到游戏关卡场景

#### 参考方案

- **CustomSabersLite (qqrz997)**: 使用 `toggleable-slider`（slider + toggle 组合），所有设置都带 `apply-on-change`、`bind-values`，控件自适应宽度。关键：**slider-setting 比 increment-setting 更适合 VR 交互**，因为它有明确的滑块轨道，pointer 可以精确操作
- **Enhancements (Auros)**: 游戏内 HUD 通过以下架构实现：
  1. `XGameInstaller` 使用 `SiraUtil` 的 `Location.Game` 注入
  2. `BasicClockView` 继承 `BSMLAutomaticViewController`，实现 `IInitializable`
  3. 通过 `FromNewComponentAsViewController()` 创建 ViewController 自动加入游戏场景
  4. BSML 文件使用 `<clickable-text>` 显示 HUD 信息
  5. 使用 `ZFixTextShader` 解决 VR 中文本 Z-fighting 问题

#### 完整方案

**Phase 1: 替换 BPM 控件为 slider-setting**
- 将 `increment-setting` 替换为 `slider-setting`（滑块控件）
- `slider-setting` 有明确的滑块轨道，VR pointer 可以精确拖动
- 支持属性: `value`, `text`, `min`, `max`, `increment`, `apply-on-change`, `bind-values`, `integer-only`

**Phase 2: 新增 GameInstaller + 游戏内 HUD**
- 新增 `GameInstaller`（使用 `zenjector.Install<Location.Game>`）
- 新增 `SessionHUDView`（继承 `BSMLAutomaticViewController, IInitializable`）
- 新增 `session-hud.bsml` 布局文件
- HUD 显示: `#2/15 歌曲名 | 3min` — 简洁一行，位于屏幕上方
- 游戏内不需要手动操作 HUD，纯信息展示

**Phase 3: PlaySessionManager 共享状态**
- `PlaySessionManager` 已绑定为 `AsSingle()` 在 `AppInstaller`，在 App/Menu/Game 三个场景容器中都可访问
- `SessionHUDView` 注入 `PlaySessionManager`，订阅 `SongChanged`/`SessionEnded` 事件
- 使用协程每秒更新经过时间

**Phase 4: 注册 BSML 嵌入资源**
- 将 `session-hud.bsml` 添加到 csproj 的 EmbeddedResource

#### 修改文件清单

| 文件 | 变更类型 | 说明 |
|------|----------|------|
| `UI/Views/RandomPlaylistView.bsml` | 修改 | increment-setting → slider-setting |
| `UI/RandomPlaylistUI.cs` | 修改 | 适配 slider-setting 的属性类型 |
| `Plugin.cs` | 修改 | 新增 GameInstaller 注册 |
| `UI/SessionHUDView.cs` | 新增 | 游戏内 HUD 视图控制器 |
| `UI/Views/SessionHudView.bsml` | 新增 | HUD 布局文件 |
| `RandomPlaylistMod.csproj` | 修改 | 添加 BSML 嵌入资源 |
| `docs/bsml_troubleshooting.md` | 新增 | BSML 开发避坑指南 |

#### Bug 修复记录

- **SessionHudView.bsml Invalid BSML**: .bsml 文件缺少 `<bg>` 根元素和 XML 声明，只有裸 `<text>` 标签 → 补充完整 XML 结构
- **integer-only 属性误判**: 曾尝试移除 `integer-only="true"` 来修 Invalid BSML，但经查官方文档确认该属性合法 → 恢复。实际 Invalid BSML 根因是 SessionHudView.bsml 缺少根元素
- **font-color 颜色名问题**: `font-color="white"` 改为 `font-color="#FFFFFF"` 十六进制格式更安全

---

## 任务计划与执行结果（续 4）

### ✅ Task 11: 修复 No Fail 功能不生效 + BSML 交互问题
- **状态**: 已完成
- **问题**:
  1. `checkbox-setting` 放在 `horizontal` 容器内导致 VR 中无法点击
  2. 改为 `vertical` 独立行后，下方内容消失
  3. `NoFailEnabled` 设置后游戏内不生效（仍然失败跳过）
- **根因分析**:
  - BSML `checkbox-setting` 与 `horizontal` 布局容器冲突，导致交互区域异常
  - `GameplayModifiers.noFailOn0Energy` 属性是 `init-only`（CanWrite: False）
  - 正确的字段名是 `_noFailOn0Energy`（带下划线前缀），代码搜索的是错误名称
- **实现**:
  - 将 `checkbox-setting` 改为普通 `button`（VR 交互最稳定）
  - 添加 `ToggleNoFail()` 方法切换布尔值
  - 添加 `NoFailButtonText` 属性动态显示 "No Fail: ON/OFF"
  - 修复 `TryEnableNoFailModifier()`，优先搜索 `_noFailOn0Energy` 字段
  - 调整 UI padding（`pad="1"`、`pad-left/right="2"`），优化显示
- **修改文件**: 
  - `UI/Views/RandomPlaylistView.bsml`
  - `UI/RandomPlaylistUI.cs`
  - `Managers/PlaySessionManager.cs`
  - `docs/BSML_INTERACTION_TROUBLESHOOTING.md`（新增故障排除文档）
- **关键决策**: 
  - 放弃 `checkbox-setting`，改用 `button` + 文本绑定（文档推荐的最稳定方案）
  - 反射设置字段时优先尝试带下划线的私有字段名（Beat Saber 1.40.8 实际结构）
- **验证步骤**:
  1. 按钮可以正常点击切换 ON/OFF
  2. 日志显示 `No Fail enabled via _noFailOn0Energy field`
  3. 游戏内开启 No Fail 后，能量耗尽不会失败跳过

---

## 变更文件清单（续 3）

| 文件 | 变更类型 | 说明 |
|------|----------|------|
| `UI/Views/RandomPlaylistView.bsml` | 修改 | checkbox-setting → button，调整 padding |
| `UI/RandomPlaylistUI.cs` | 修改 | 新增 ToggleNoFail() + NoFailButtonText |
| `Managers/PlaySessionManager.cs` | 修改 | 修复 TryEnableNoFailModifier 字段名 |
| `docs/BSML_INTERACTION_TROUBLESHOOTING.md` | 新增 | BSML 交互故障排除文档 |

---

## 任务计划与执行结果（续 5）

### ✅ Task 12: 修复难度选择逻辑（NPS 范围匹配）
- **状态**: 已完成
- **问题**: `StartLevel()` 中固定选择 `difficulties[0]`（最简单难度），导致 NPS 过滤后播放的是最简单 level，不符合筛选意图
- **实现**:
  - 添加 `SelectBestDifficulty()` 方法：通过 `SongDetailsCache` 查询每个难度的 NPS，选择符合 `[MinNPS, MaxNPS]` 范围的最难难度
  - 若 `SongDetailsCache` 不可用或无匹配难度，fallback 到最高可用难度
  - 添加 `GetSongDetails()` 静态方法缓存 `SongDetails` 实例
  - 修改 `StartLevel()` 调用 `SelectBestDifficulty()` 替代 `difficulties[0]`
- **修改文件**: `Managers/PlaySessionManager.cs`
- **关键决策**: 优先精确匹配 NPS 范围，fallback 到最高难度保证可用性

---

## 变更文件清单（续 4）

| 文件 | 变更类型 | 说明 |
|------|----------|------|
| `Managers/PlaySessionManager.cs` | 修改 | 新增 SelectBestDifficulty + GetSongDetails，修复难度选择逻辑 |
| `UI/SessionHUDView.cs` | 修改 | VR HUD 改为 WorldSpace Canvas，修复编译错误 |
| `manifest.json` | 修改 | 版本号更新至 1.3.0 |
| `release-notes-v1.3.0.md` | 新增 | v1.3.0 发布说明 |

---

## 🚀 Release v1.3.0

**发布日期**: 2026-05-13

**变更摘要**:
- ✅ 智能难度选择：自动选 NPS 范围内的最高难度
- ✅ VR HUD 修复：ScreenSpaceOverlay → WorldSpace，HUD 在 VR 中可见
- ✅ 修复 MapDifficulty → BeatmapDifficulty 转换编译错误

**Commit**: `Release v1.3.0`
**Tag**: `v1.3.0`

---

## 任务计划与执行结果（续 5）

### ✅ Task 13: VR HUD 位置与朝向修复
- **状态**: 已完成
- **问题**:
  1. HUD 在头顶正上方，仰头 90° 看导致文字上下翻转
  2. 字体太小（0.05f = 5cm），远处看不清
  3. HUD 区域太小（1.5×0.15m），信息显示局促
- **实现**:
  - 位置改为**前方 6 米 + 上方 2.5 米**（仰头约 22° 可见，不反）
  - 字体从 0.05f → **0.18f**（18cm 高，远处清晰）
  - Canvas 从 1.5×0.15 → **3.0×0.35**（更宽更高）
  - 提取 `UpdateHudPosition()` 方法，`LateUpdate` 每帧更新位置 + Billboard 朝向
- **修改文件**: `UI/SessionHudView.cs`
- **关键决策**: 放到前方偏上而非头顶正上方，避免 90° 仰头导致文字翻转

### ✅ Task 14: 选歌逻辑修复（队列总时长允许超过目标）
- **状态**: 已完成
- **问题**: 用户选择 120 分钟，但只有 10 首歌（总时长 ~30 分钟）会话就结束了
- **根因**: `SelectSongsForDuration` 在总时长超过目标时停止选歌，导致队列歌曲太少
- **实现**:
  - 第一轮：凑满目标时长（允许超过），避免连续同一作者
  - 第二轮：继续添加剩余所有歌曲
  - 队列总时长可以超过目标，由计时器控制会话结束
- **修改文件**: `Managers/SongSelector.cs`
- **关键决策**: 让队列歌曲多于目标时长所需；计时器是会话结束的最终仲裁者

### ✅ Task 15: 队列耗尽时自动重新打乱
- **状态**: 已完成
- **问题**: 队列歌曲播放完后即使计时器未到期，会话也立即结束
- **实现**:
  - `OnSongFinished()` 中，当 `HasNextSong()` 为 false 但计时器未到期时，重新打乱队列继续播放
  - `_currentSongQueue = _songSelector.ShuffleSongs(_currentSongQueue); _currentSongIndex = 0;`
- **修改文件**: `Managers/PlaySessionManager.cs`
- **关键决策**: 队列耗尽后重新打乱继续播放，适合歌曲总数少于目标时长的场景

---

## 变更文件清单（续 5）

| 文件 | 变更类型 | 说明 |
|------|----------|------|
| `UI/SessionHudView.cs` | 修改 | HUD 位置修复 + 字体放大 + Canvas 放大 |
| `Managers/SongSelector.cs` | 修改 | `SelectSongsForDuration` 允许超过目标时长 |
| `Managers/PlaySessionManager.cs` | 修改 | `OnSongFinished` 队列耗尽时重新打乱 |
| `Tests/SongSelectorTests.cs` | 修改 | 新增 2 个测试用例 |

---

## 测试记录

### SongSelectorTests 新增用例
1. `SelectSongsForDuration_SelectsAllSongs_WhenSongsFitWithinTarget` — 验证当歌曲总时长小于目标时所有歌曲都被选中
2. `SelectSongsForDuration_QueueTotalDurationExceedsTarget` — 验证队列总时长可以超过目标时长

**测试结果**: 编译通过，无错误。

---

## 🚀 Release v1.4.0

**发布日期**: 2026-05-27

**变更摘要**:
- ✅ HUD 显示优化：固定世界坐标，字体放大到 36cm，透明背景
- ✅ 修复会话提前结束：队列耗尽时自动重新打乱继续播放
- ✅ 修复选歌逻辑：`SelectSongsForDuration` 允许超过目标时长
- ✅ 新增 2 个 `SongSelectorTests` 测试用例

**Commit**: `Release v1.4.0`
**Tag**: `v1.4.0`
**GitHub Release**: https://github.com/xirain/RandomPlaylistMod/releases/tag/v1.4.0

**修改文件清单**:
| 文件 | 变更类型 | 说明 |
|------|----------|------|
| `UI/SessionHudView.cs` | 修改 | HUD 固定位置 + 字体 0.36f + 透明背景 |
| `Managers/SongSelector.cs` | 修改 | `SelectSongsForDuration` 允许超过目标时长 |
| `Managers/PlaySessionManager.cs` | 修改 | `OnSongFinished` 队列耗尽时重新打乱 |
| `Tests/SongSelectorTests.cs` | 修改 | 新增 2 个测试用例 |
| `manifest.json` | 修改 | 版本号 1.3.1 → 1.4.0 |
| `RandomPlaylistMod.csproj` | 修改 | 版本号 1.3.1 → 1.4.0 |
| `progress.md` | 修改 | 更新进度记录 |

---

## 🚀 Release v1.5.0

**发布日期**: 2026-06-05

**变更摘要**:
- ✅ 官方歌曲支持：Beat Saber 官方歌曲作为虚拟播放列表加入可选列表
- ✅ HUD 世界空间渲染：HUD 固定在 VR 世界中，不再跟随头部
- ✅ HUD 显示开关：新增 HUD 显示/隐藏切换功能
- ✅ 修复计时显示问题

**Commit**: `Release v1.5.0: official levels, world-space HUD, HUD toggle, timer fix`
**Tag**: `v1.5.0`
**GitHub Release**: https://github.com/xirain/RandomPlaylistMod/releases/tag/v1.5.0

**修改文件清单**:
| 文件 | 变更类型 | 说明 |
|------|----------|------|
| `Managers/PlaylistManager.cs` | 修改 | 新增官方歌曲虚拟播放列表 + Loader API 适配 |
| `UI/SessionHudView.cs` | 修改 | HUD 世界空间渲染 + 显示开关 + 计时修复 |
| `manifest.json` | 修改 | 版本号 1.4.0 → 1.5.0 |
| `RandomPlaylistMod.csproj` | 修改 | 版本号 1.4.0 → 1.5.0 |

---

## 🚀 Release v1.6.0

**发布日期**: 2026-06-09

**变更摘要**:
- ✅ 官方歌曲(OST)虚拟播放列表：正式支持 Beat Saber 官方原声歌曲

**Commit**: `Release v1.6.0: official OST virtual playlist`
**Tag**: `v1.6.0`
**GitHub Release**: https://github.com/xirain/RandomPlaylistMod/releases/tag/v1.6.0

**修改文件清单**:
| 文件 | 变更类型 | 说明 |
|------|----------|------|
| `Managers/PlaylistManager.cs` | 修改 | 新增官方歌曲(OST)虚拟播放列表 |
| `manifest.json` | 修改 | 版本号 1.5.0 → 1.6.0 |
| `RandomPlaylistMod.csproj` | 修改 | 版本号 1.5.0 → 1.6.0 |

---

## 🚀 Release v1.7.0

**发布日期**: 2026-06-12

**变更摘要**:
- ✅ 修复歌单选择后列表自动滚动回顶部的问题
- ✅ 新增已选歌单数量实时显示
- ✅ 提升歌单列表高度，减少滚动需求
- ✅ 优化布局，改进选择体验

**Commit**: `Release v1.7.0: UI interaction optimization`
**Tag**: `v1.7.0`
**GitHub Release**: https://github.com/xirain/RandomPlaylistMod/releases/tag/v1.7.0

**修改文件清单**:
| 文件 | 变更类型 | 说明 |
|------|----------|------|
| `UI/RandomPlaylistUI.cs` | 修改 | 使用 ReloadDataKeepingPosition() 保留滚动位置 + 新增已选数量显示 |
| `UI/Views/RandomPlaylistView.bsml` | 修改 | 新增已选数量文本组件 + 提升列表高度 |
| `manifest.json` | 修改 | 版本号 1.6.0 → 1.7.0 |
| `RandomPlaylistMod.csproj` | 修改 | 版本号 1.6.0 → 1.7.0 |

---

## 🚀 Phase 2: 游戏数据持久化 & 分享功能 (v2.0.0)

**开发日期**: 2026-06-23
**版本号**: 2.0.0

### 新增功能
- ✅ 会话结束后自动保存游玩记录到本地 JSON（`UserData/RandomPlaylistMod/History/`）
- ✅ 记录每首歌的得分、连击、失误、精度等运动数据（基于 `LevelCompletionResults`）
- ✅ 玩家聚合档案（`profile.json`）：总会话数、总时长、连续天数等
- ✅ 会话结束自动弹出总结面板（`SessionSummaryView`）
- ✅ 生成分享 HTML 文件（`UserData/RandomPlaylistMod/Share/`）
- ✅ 旧记录自动清理（保留最近 90 天）

### 数据模型
| 模型 | 文件 | 说明 |
|------|------|------|
| `SessionRecord` | `Models/SessionRecord.cs` | 单次会话完整记录（13字段） |
| `SongResult` | `Models/SongResult.cs` | 单首歌结果（14字段，含 Failed 标志） |
| `ExerciseSummary` | `Models/ExerciseSummary.cs` | 运动数据汇总（10字段） |
| `PlayerProfile` | `Models/PlayerProfile.cs` | 玩家聚合档案（11字段） |
| `SessionSettingsSnapshot` | `Models/SessionRecord.cs` | 设置快照（6字段） |

### 新增类
| 类 | 文件 | 说明 |
|------|------|------|
| `HistoryManager` | `Managers/HistoryManager.cs` | 持久化读写（AppInstaller 注册为 AsSingle） |
| `ShareImageGenerator` | `Managers/ShareImageGenerator.cs` | 分享 HTML 模板生成 |
| `SessionSummaryView` | `UI/SessionSummaryView.cs` + `.bsml` | 会话结束总结面板 |
| `HistoryView` | `UI/HistoryView.cs` + `.bsml` | 历史记录浏览面板（列表+详情+删除+分享） |

### 修改文件
| 文件 | 变更类型 | 说明 |
|------|----------|------|
| `Models/SongResult.cs` | 新增 | 歌曲结果模型（含 Failed 标志 + 属性安全读取） |
| `Models/ExerciseSummary.cs` | 新增 | 运动数据汇总模型（10字段） |
| `Models/PlayerProfile.cs` | 新增 | 玩家档案模型（11字段 + 增量更新） |
| `Models/SessionRecord.cs` | 新增 | 会话记录 + SessionSettingsSnapshot 快照模型 |
| `Managers/HistoryManager.cs` | 新增 | 数据持久化管理器（读写/清理/Profile维护） |
| `Managers/ShareImageGenerator.cs` | 新增 | 分享 HTML 模板生成器 |
| `UI/SessionSummaryView.cs` | 新增 | 会话总结面板（4指标+分享+历史入口） |
| `UI/Views/SessionSummaryView.bsml` | 新增 | 总结面板布局 |
| `UI/HistoryView.cs` | 新增 | 历史列表+详情+选择导航+删除/分享 |
| `UI/Views/HistoryView.bsml` | 新增 | 历史面板布局 |
| `Managers/PlaySessionManager.cs` | 修改 | 捕获 LevelCompletionResults + 构建 SessionRecord + 失败记录 |
| `UI/RandomPlaylistFlowCoordinator.cs` | 修改 | 注入 HistoryView + ShowHistoryView/DismissHistoryView |
| `Plugin.cs` | 修改 | AppInstaller/MenuInstaller 注册全部新服务 |
| `RandomPlaylistMod.csproj` | 修改 | 嵌入全部 BSML + 版本号 1.7.0 → 2.0.0 |
| `manifest.json` | 修改 | 版本号 1.7.0 → 2.0.0 |

### UI 导航关系
```
菜单按钮 → RandomPlaylistFlowCoordinator
  ├── RandomPlaylistUI (主界面)
  │     └── [Start Session] 进入游戏
  ├── SessionSummaryView (会话结束后自动弹出)
  │     ├── [Generate Share] 生成分享 HTML
  │     ├── [View History] → HistoryView
  │     └── [Close] 关闭
  └── HistoryView (历史记录浏览)
        ├── ◀ Prev / ▶ Next 选择会话
        ├── [Share This Session] 生成分享 HTML
        ├── [Delete This Session] 删除记录
        ├── [Back to Summary] 返回总结面板
        └── [Close] 全部关闭
```

### 存储路径
```
UserData/RandomPlaylistMod/
├── History/           ← 一条会话一个 JSON
│   └── 20260623-143052-a1b2c3d4.json
├── Share/             ← 分享 HTML 文件
│   └── 20260623-143052-a1b2c3d4.html
├── profile.json       ← 玩家聚合档案
└── settings.json      ← （预留）模组设置

---

## 任务计划与执行结果（续 7）

### Task 17: 修复 SessionSummaryView / HistoryView 布局重叠 + 按钮无文字（2026-07-08）

#### 现象（用户反馈）
- 结束 session 后总结面板排版乱、元素大量重叠
- 底部按钮（Generate Share Image / View History / Close）看不到文字

#### 根因（反编译 BSML.dll 字符串表确认）
1. **重叠**：BSML `<vertical>`/`<horizontal>` 映射到 Unity LayoutGroup，`childControlHeight` 默认 true。旧 BSML 未显式设置，导致文本子元素高度被压成 0 → 重叠。能正常工作的 `RandomPlaylistView.bsml` 每个布局组都显式写了 `child-control-height="false"`。
2. **按钮无文字**：
   - `min-height` 在 BSML 中 **不支持**（dll 中 0 次出现），按钮高度被忽略，文字被压缩/不可见。
   - 按钮 `class="action-button"/"secondary-button"/"close-button"/"delete-button"` 在工程中从未定义（无 styles.xml），属无效引用。
   - 按钮文字含 emoji，TextMeshPro 可能无对应字形导致渲染异常。

#### 实现
- 两个 BSML 统一改为：根 `<bg>` + 内层 `<vertical>`，所有布局组显式加 `child-control-height="false" child-expand-height="false"`（对齐可用视图模式）。
- 按钮：移除未定义 class、移除 emoji；用 BSML 支持的 `pref-height`/`pref-width` 设定尺寸（替代无效的 `min-height`）。
- 添加 `xmlns:xsi` 命名空间声明。

#### 修改文件清单
| 文件 | 说明 |
|------|------|
| `UI/Views/SessionSummaryView.bsml` | 布局修复 + 按钮修复 |
| `UI/Views/HistoryView.bsml` | 同样修复 |

#### 验证
- 编译 0 错误 0 警告；部署 1.40.8 / 1.42.2
- DLL 内嵌 BSML 校验：`min-height=False`、`child-control-height=false=True`、`uses-class=False` ✅

---

## 任务计划与执行结果（续 8）

### Task 18: 修复 SessionSummaryView 数据全空（仅标题显示）（2026-07-14）

#### 现象（用户反馈）
- 结束 session 后界面只有 "Session Complete!" 标题，所有数据（Duration / Songs Played / Score 等）全空
- 日志确认：session 正常结束、record 已保存（8 首、17 分钟）、`SessionEndedWithRecord` 事件触发、`Summary view presented` 正常，无 BSML 报错

#### 根因分析
- 标题 `~summary-title` 是常量绑定 → 能显示；数据项 `~duration-text` 等依赖 `_currentRecord` → 全空。说明非常量绑定在渲染/刷新时失效，但绑定引用本身未变（与能显示数据的旧版 BSML 字节级一致）。
- 对比差异：旧版（数据显示正常）**无** `xmlns:xsi` 声明；本次布局修复（Task 17）新增了 `xmlns:xsi="..."` + `xsi:noNamespaceSchemaLocation="..."`。这是两版间唯一影响解析/属性通知的结构性新增。`RandomPlaylistView.bsml` 虽有该声明但其数据多为展示即定值、未暴露此问题。
- 结论：`xmlns:xsi` + schema 定位声明干扰了 BSML 对非常量绑定的属性变更通知，导致 `_currentRecord` 赋值后的 `NotifyPropertyChanged` 无法刷新文本。

#### 实现
1. `SessionSummaryView.bsml` / `HistoryView.bsml`：移除 `xmlns:xsi` 与 `xsi:noNamespaceSchemaLocation` 两行（回到已知能显示数据的结构），保留 `child-control-height="false"` 布局修复。
2. `SessionSummaryView.cs`：`SetSessionRecord` 末尾追加 `NotifyPropertyChanged("")` 全量刷新，作为属性名匹配的兜底。

#### 修改文件清单
| 文件 | 说明 |
|------|------|
| `UI/Views/SessionSummaryView.bsml` | 移除 xmlns:xsi，保留布局修复 |
| `UI/Views/HistoryView.bsml` | 同上 |
| `UI/SessionSummaryView.cs` | SetSessionRecord 全量刷新 |

#### 验证
- 编译 0 错误 0 警告；部署 1.40.8 / 1.42.2
- DLL 内嵌 BSML 校验：`xmlns:xsi=False`、`child-control-height=True`、`duration-text=True` ✅
- 待用户实机验证：标题 + 四项数据 + 详情 + 三按钮文字均应正常显示

## 任务计划与执行结果（续 9）

### Task 19: 移植到 Beat Saber 1.44.0（点击 Start 卡死修复）（2026-07-19）

#### 背景
- 将 RandomPlaylistMod 与 PlaylistManager 移植到 BS 1.44.0 / SongCore 3.16.0（BSManager 实例路径 `F:\paly\BSManager\BSInstances\1.44.0`）。
- 前期已修复：PlaylistManager 黑屏（`IPlatformUser` 改为 `[InjectOptional]`）、`MenuTransitionsHelper.StartStandardLevel` 签名变更（新增 `GameplayAdditionalInformation` 与 `IBeatmapLevelData beatmapLevelData` 参数，移除旧参数），csproj/manifest 已指向 1.44.0。

#### 现象（用户反馈）
- 点击 Start 后画面直接卡住（主线程冻结），并非无响应。

#### 根因分析
- 日志确认：`SongCore.Loader.CustomLevelLoader.LoadBeatmapLevelData(beatmapLevel)` 对**每一首歌都返回 null**（SongCore 3.16.0 从该 fork 构建，`LoadBeatmapLevelData` 构造函数体读取 `CustomLevelLoader._loadedBeatmapSaveData` 缓存并用 `CreateBeatmapLevelDataFromV3/V4` 重建 `IBeatmapLevelData`，该路径在运行时返回 null）。
- 旧逻辑：`beatmapLevelData == null` 时调用 `OnLevelCompleted(null, null)` → `OnSongFinished` → `AdvanceToNextSong` → `PlayNextSong` → `StartLevel`，**在同一毫秒内同步递归遍历全部 332 首歌**，主线程被淹没 → 游戏卡死。

#### 实现
1. `PlaySessionManager.cs` 新增 `ResolveBeatmapLevelData(BeatmapLevel)`：
   - 优先调用官方 `LoadBeatmapLevelData`；
   - 返回 null 时，通过反射读取 `CustomLevelLoader._loadedBeatmapSaveData` 缓存，用 `CreateBeatmapLevelDataFromV4`/`CreateBeatmapLevelDataFromV3` 直接重建 `IBeatmapLevelData`（可绕过官方方法的异常吞没，暴露真实错误）；
   - 输出**一次性**诊断日志（`[DataDiag]`），记录缓存命中、样例 key、LoadedSaveData 字段情况。
2. `StartLevel` 中：取到数据失败后累计 `_consecutiveNullData`，连续 ≥10 首仍失败时调用 `EndSession()` 优雅停止，避免同步递归卡死。
3. 新增 `using System.Reflection;`。

#### 修改文件清单
| 文件 | 说明 |
|------|------|
| `Managers/PlaySessionManager.cs` | 新增 `ResolveBeatmapLevelData` + 连续失败保护；`StartStandardLevel` 调用改用新方法 |

#### 验证
- 编译 0 错误 0 警告；已部署到 `1.44.0\Plugins`。
- 待用户重启游戏实机验证：如仍失败，新日志中的 `[DataDiag]` 行会精确指出缓存 key 不匹配 / V3/V4 构造返回 null / 异常类型，据此二次修复。

### Task 20: 点击 Start 直接显示结束画面修复（OST 官方歌无法加载）（2026-07-19）

#### 现象（用户反馈）
- 加了连续失败保护后，点 Start **不再卡死**，但**直接进入结束画面**（连续 ≥10 首取不到 beatmap 数据 → `EndSession()`）。

#### 根因分析（借助 TypeProbe 反查 1.44.0 程序集）
- `PlaylistManager` 含「🎼 官方歌曲 (OST)」虚拟歌单，`AddOfficialLevelSongs` 会把 `StartMeUp` 等 OST 官方歌加入队列。
- `SongCore.Loader.CustomLevelLoader.LoadBeatmapLevelData(beatmapLevel)` **只认 `CustomLevelLoader._loadedBeatmapSaveData` 缓存（key 全是 `custom_level_<hash>`）**，OST 官方歌的数据在 AssetBundle 里、不在该缓存 → 永远返回 null。
- 因此一旦歌单包含/选中 OST 官方歌，`ResolveBeatmapLevelData` 持续返回 null → 连续失败保护触发 → 直接结束画面。
- 1.44.0 关键事实（已反查确认）：无 `IBeatmapLevel` 接口；`BeatmapLevel` 为普通类；`IBeatmapLevelData` 由 `BeatmapLevelDataSO` / `FileSystemBeatmapLevelData` / `CustomFileBeatmapLevelData` 实现；`SongCore.Hooks.BeatmapLevelCache.BeatmapJsonCacheHooks` 把自定义关卡数据注入游戏自带 `BeatmapLevelLoader`；`IBeatmapLevelLoader.LoadBeatmapLevelDataAsync(BeatmapLevel, BeatmapLevelDataVersion, CancellationToken)` 返回 `Task<LoadBeatmapLevelDataResult>`（`beatmapLevelData` 字段 / `isError` 属性），`BeatmapLevelDataVersion` 枚举仅 `Original`、`NoEnvironmentKeywords`。**该接口是游戏统一加载入口，自定义歌与 OST 官方歌都能加载。**

#### 实现
1. `PlaySessionManager.cs` 构造函数注入 `[InjectOptional] IBeatmapLevelLoader`（全局命名空间，定义于 `AdditionalContentModel.Interfaces`）。
2. `ResolveBeatmapLevelData` 重写为两路：
   - ① 优先 `SongCore.Loader.CustomLevelLoader.LoadBeatmapLevelData`（自定义歌最快）；
   - ② 失败则用 `_beatmapLevelLoader.LoadBeatmapLevelDataAsync(beatmapLevel, BeatmapLevelDataVersion.Original, default).GetAwaiter().GetResult()`，取 `result.beatmapLevelData`（覆盖 OST 官方歌）。
   - 兜底：若 App 容器未注入 `IBeatmapLevelLoader`（可能只绑在 Menu/Game 场景容器），运行时从当前场景 `SceneContext` 的 `Container.TryResolve<IBeatmapLevelLoader>()` 解析。
3. `RandomPlaylistMod.csproj` 新增引用 `AdditionalContentModel.Interfaces.dll`。
4. `LoadBeatmapLevelDataResult` 为 struct，按值处理（不判 null）。`using System.Threading;` 已加。

#### 修改文件清单
| 文件 | 说明 |
|------|------|
| `Managers/PlaySessionManager.cs` | 注入 `IBeatmapLevelLoader`；`ResolveBeatmapLevelData` 改为 SongCore 快速路径 + `IBeatmapLevelLoader` 统一兜底 + 场景容器兜底获取 |
| `RandomPlaylistMod.csproj` | 新增 `AdditionalContentModel.Interfaces` 引用 |

#### 验证
- 编译 0 错误 0 警告；已部署到 `1.44.0\Plugins`。
- 待用户重启游戏实机验证：点 Start 应正常进歌（含 OST 官方歌）。如 `[DataDiag]` 出现「IBeatmapLevelLoader 未注入」「返回错误」等，按日志二次处理。

### Task 21: 点 Start 画面卡死/直接结束 —— beatmapLevelData 与 _beatmapLevelsModel 互斥（2026-07-19）

#### 现象（用户反馈）
- 部署 Task 20 后点 Start：**画面卡住**（后续日志显示异常被 catch，整张歌单快速遍历完 → 结束画面）。
- 日志关键报错：`GameplayCoreSceneSetupData: When the beatmapLevelData is provided, there is no need to provide _beatmapLevelsModel. (Parameter 'beatmapLevelData')`，抛出于 `StartLevel` 调用 `MenuTransitionsHelper.StartStandardLevel`。

#### 根因分析（反查 1.44.0 程序集 + 日志栈）
- 游戏 `GameplayCoreSceneSetupData` 构造函数硬性规定：**`beatmapLevelData` 与 `_beatmapLevelsModel` 互斥**（两者都非 null 即抛 `ArgumentException`）。
- `_beatmapLevelsModel` 由 Zenject 注入进 `StandardLevelScenesTransitionSetupDataSO`，**永远非 null**。
- Task 20 引入的 `IBeatmapLevelLoader` 兜底成功返回了非 null 的 `beatmapLevelData`，于是 `StartStandardLevel` 同时带上 `beatmapLevelData`（非 null）和注入的 `_beatmapLevelsModel`（非 null）→ 必然抛异常 → catch → `PlayNextSong` → 每首都抛 → 快速遍历完歌单 → 结束画面。
- **进一步回溯**：最初「点 Start 直接结束画面」的根因也是同一处——`ResolveBeatmapLevelData` 对自定义歌也返回非 null（SongCore 缓存里有），所以**所有歌**都触发此互斥异常，而非「OST 无法加载」。Task 20 的方向（给 OST 补数据）是错判，真正问题在调用参数。

#### 实现（正确修复）
- `PlaySessionManager.StartLevel`：调用 `StartStandardLevel` 时 **`beatmapLevelData` 参数传 `null`**，让游戏走正常的 `BeatmapLevelsModel` 路径加载关卡数据（这是游戏从菜单启动关卡的标准方式，`Loader.GetLevelById` 返回的关卡已注册进 model，自定义歌与 OST 官方歌均支持）。
- 移除 Task 20 误引入的全部相关代码：`ResolveBeatmapLevelData` 方法、`IBeatmapLevelLoader` 字段与构造注入、`_consecutiveNullData` / `_dataDiagnosticLogged` 字段、基于 `ResolveBeatmapLevelData` 的连续失败保护 gating 块（该保护本身也掩盖了真实异常，应移除）。
- 保留 `try/catch` 以兜底异常不崩溃。

#### 修改文件清单
| 文件 | 说明 |
|------|------|
| `Managers/PlaySessionManager.cs` | `StartStandardLevel` 的 `beatmapLevelData` 改为 `null`；删除 `ResolveBeatmapLevelData` 方法、相关字段与注入、连续失败 gating 块 |

#### 验证
- 编译 0 错误；已部署到 `1.44.0\Plugins`。
- 待用户重启游戏实机验证：点 Start 应正常进入第一首歌并可持续播放。若仍有异常，`StartLevel` 的 catch 会输出 `Error starting level ...` + 栈，据此定位。

### Task 22: 发布 RandomPlaylistMod 2.0（目标 1.44.0）+ 开发基线确立（2026-07-22）

#### 决策（用户确认）
- **发布 2.0**：本地 1.44 实验已结束，功能完善（含结束时信息展示），准备发布 2.0。
- **开发基线**：**只在 Beat Saber 1.44 继续开发，不再维护 1.40**。后续新功能在 1.44 上展开。

#### 依赖信息总结（发布用，来自 `manifest.json` dependsOn + 实例核验）
- 目标游戏版本：`gameVersion = 1.44.0`；插件版本 `2.0.0`。
- 运行时硬依赖 `dependsOn`：
  - `BSIPA ^4.1.0`（实例实为 4.2.0 ✓）
  - `SiraUtil ^3.0.0`
  - `SongCore ^3.16.0`
  - `BeatSaberMarkupLanguage ^1.6.0`（BSML，UI 用）
  - `SongDetailsCache ^1.0.0`（NPS 筛选用）
  - `PlaylistManager ^1.0.0`（实例实为 1.7.3 ✓；使用 1.44 适配 fork `xirain/PlaylistManager@1.44`）
- 代码静态检查：`using BS_Utils` 0 命中 → `csproj` 中的 `BS_Utils` 引用是多余的（未使用，无害，可清理）。
- 游戏内置程序集（非用户安装依赖，由 BS 1.44 提供）：UnityEngine / Zenject / HMUI / VRUI / BeatmapCore / GameplayCore / MenuSystem / Core / DataModels / Colors / BeatSaber.ViewSystem 等。

#### 发布待办（提醒）
- `manifest.json` 的 `author` 仍为占位 `"Developer"`，发布前应改为实际作者（如 `xirain`）。
- `PlaylistManager` 依赖需 1.44 兼容版本；BeatMods 发布时 `dependsOn` 约束保持 `^1.0.0` 即可覆盖 1.7.x。

#### 修改文件清单
- 仅文档/记忆更新；源码无改动（依赖声明已就绪）。

















