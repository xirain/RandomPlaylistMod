## v2.3.0

### 核心新增：AutoBS 集成（自动播放 90° 摆头谱）

本版本让 RandomPlaylistMod 的自动播放流程直接借力 [AutoBS](https://github.com/procedure1/AutoBS) 模组，
在有限活动空间里也能舒适游玩——无需手动翻找 90° 谱面，任意普通 Standard 歌都能自动生成 90° 摆头版本播放。

**前置条件**：需另行安装并启用 AutoBS（`Enable Generated 90 Maps` 开关打开），RandomPlaylistMod 只负责选
`90Degree` 特征起播，真正的旋转/弧线/墙体增强由 AutoBS 在播放时完成。

### UI 新增：AutoBS 模式开关

播放面板在 NPS 行下方新增 **AutoBS 模式行**，四档按钮：

| 按钮 | 行为 |
| --- | --- |
| `Standard` | 不指定特征，回退标准播放（不使用 AutoBS） |
| `45°` | 选 90° 特征起播，并令 AutoBS 以 **±45°** 摆幅生成 |
| `60°` | 选 90° 特征起播，并令 AutoBS 以 **±60°** 摆幅生成（默认） |
| `90°` | 选 90° 特征起播，并令 AutoBS 以 **±90°** 摆幅生成 |
| `360°` | 选 Generated360Degree 特征起播（不适合有限空间，保留选项） |

- 45°/60°/90° 三档通过反射写入已安装 AutoBS 的 `Config.Generated90SwingRange`（不硬引用 AutoBS 程序集；
  AutoBS 未安装时仅记日志，不影响 RandomPlaylistMod 自身播放）。
- 摆幅换算：每格 15°，45°→±45°、60°→±60°、90°→±90°（不含转身，适合运动场地）。

### 行为修正：不再因缺 90° 特征跳过歌曲

早期版本在歌曲菜单特征列表里没有 `90Degree` 时会整首跳过。现改为**回退 Standard 播放**——
AutoBS 在歌曲数据加载阶段即生成 90° 特征，歌曲照常播放且下一轮即可命中 90° 生成，避免大量歌被静默跳过。

### 其他

- 目标 Beat Saber 1.44.0，manifest 版本 2.3.0。
- 运行时硬依赖不变：BSIPA ^4.1.0、SiraUtil ^3.0.0、SongCore ^3.16.0、BeatSaberMarkupLanguage ^1.6.0、
  SongDetailsCache ^1.0.0、PlaylistManager ^1.0.0。
- 向后兼容 2.2.0 的播单 / 会话 / NPS 筛选逻辑。

> 基于分支 `local/level-filter-2.2.0`，目标 Beat Saber 1.44.0。
