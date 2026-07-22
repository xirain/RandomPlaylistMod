# RandomPlaylistMod

Beat Saber 随机播放列表模组 — 设定时长，从多个歌单中随机选歌连续播放。

[English](README.en.md)

> **当前状态**：v2.0 已发布，针对 Beat Saber 1.44 适配，核心功能与结束时信息展示完善。后续新功能在 1.44 上持续展开，欢迎提交 Issue 反馈。

## 测试环境

本模组在以下环境中开发和测试：

- **头显**：Pico 4 Ultra
- **串流**：Pico 串流软件（PC VR 串流）
- **游戏**：Steam 版 Beat Saber 1.44.0

> 如果你在其他环境（Quest / Index / 等其他头显）中测试成功，欢迎在 Issues 中反馈！

## 功能

- **时长设定**：预设 30 分钟 / 1 小时 / 2 小时，或自定义时长（1–120 分钟）
- **多歌单选择**：勾选多个 Playlist Manager 歌单作为歌曲池
- **官方歌曲（OST）**：内置虚拟播放列表 `🎼 官方歌曲 (OST)`，自动聚合 Beat Saber 内置的 OST 歌曲（无需 Playlist Manager 配置）
- **自定义歌曲**：内置虚拟播放列表 `🎮 所有自定义歌曲`，自动聚合所有已安装的自定义关卡
- **NPS 速度筛选**：按 Notes Per Second 过滤歌曲，体感对应 Hard/Expert 等难度（数据来自 SongDetailsCache）
- **智能随机**：Fisher-Yates 洗牌算法保证随机性，自动预估歌曲数量和总时长
- **连续播放**：歌曲结束后自动切换下一首，时间耗尽自动结束会话
- **容错跳过**：歌曲加载失败自动跳过，不影响会话继续
- **不死模式开关**：可在主界面设置中启用 No Fail

> 更多功能规划见 [TODO](TODO.md)
> v2 改进设计与任务拆解见 [docs/V2_IMPROVEMENT_PLAN.md](docs/V2_IMPROVEMENT_PLAN.md)

## 截图

> 模组入口位于主菜单左侧 MODS 面板中。

## 安装

### 前置要求

| 依赖 | 说明 |
|------|------|
| **Beat Saber 1.44.0** | 本模组针对此版本开发 |
| **BSIPA 4.1+** | 模组加载框架 |
| **Playlist Manager** | 歌单管理模组（必须预先安装） |
| **SongCore 3.9+** | 歌曲数据管理 |
| **SiraUtil 3.0+** | 核心工具库 |
| **BeatSaberMarkupLanguage 1.6+** | UI 框架 |
| **SongDetailsCache** | 歌曲元数据缓存（NPS 数据来源） |

### 安装步骤

1. 确保已安装上述所有前置依赖
2. 从 [Releases](../../releases) 页面下载最新版 `RandomPlaylistMod.dll`
3. 将 DLL 文件放入 Beat Saber 的 `Plugins` 文件夹中：
   ```
   <Beat Saber 安装目录>/Plugins/RandomPlaylistMod.dll
   ```
4. 启动游戏，模组会自动加载

## 使用方法

1. 在主菜单点击左侧 **MODS** 按钮
2. 找到 **Random Playlist** 面板
3. 选择 NPS 速度区间（Any / -6 / 6-9 / 9+）
4. 选择播放时长（预设按钮或输入自定义分钟数）
5. 在歌单列表中勾选想要的歌单（可选 `🎼 官方歌曲 (OST)` / `🎮 所有自定义歌曲` 这两个虚拟歌单）
6. 点击 **Start Session** 开始播放
6. 播放中可随时点击 **End Session** 提前结束

## 构建

需要 .NET Framework 4.8 开发环境。

1. 打开 `RandomPlaylistMod/RandomPlaylistMod.csproj`，将 `BeatSaberDir` 修改为你本地的 Beat Saber 安装目录：
   ```xml
   <BeatSaberDir>F:\paly\BSManager\BSInstances\1.44.0</BeatSaberDir>
   ```
2. 执行构建：
   ```bash
   dotnet build RandomPlaylistMod/RandomPlaylistMod.csproj -c Release
   ```
3. 构建产物位于 `bin/Release/RandomPlaylistMod.dll`

## 项目结构

```
RandomPlaylistMod/
├── Plugin.cs                     # 模组入口，Zenject 绑定
├── Managers/
│   ├── PlaySessionManager.cs     # 播放会话生命周期管理
│   ├── PlaylistManager.cs        # 歌单加载与选择
│   ├── SongSelector.cs           # 随机歌曲选择算法
│   └── TimeManager.cs            # 时长与倒计时
├── UI/
│   ├── RandomPlaylistUI.cs       # UI 控制器
│   ├── RandomPlaylistFlowCoordinator.cs
│   └── Views/
│       └── RandomPlaylistView.bsml  # BSML 布局
└── Models/
    ├── SongInfo.cs
    ├── PlaylistInfo.cs
    └── PlaySession.cs
```

## 技术细节

- **框架**：BSIPA 4.1 + Zenject 依赖注入
- **UI**：BeatSaberMarkupLanguage (BSML)
- **歌单读取**：通过 BeatSaberPlaylistsLib 获取 Playlist Manager 管理的歌单
- **歌曲加载**：通过 SongCore + MenuTransitionsHelper.StartStandardLevel 启动关卡
- **缓存**：歌曲池采用懒加载 + 脏标记缓存，避免重复遍历

## 许可证

[MIT](LICENSE)
