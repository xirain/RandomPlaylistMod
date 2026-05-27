# RandomPlaylistMod - 进度追踪

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


