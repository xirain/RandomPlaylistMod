# RandomPlaylistMod B站口播稿（v2.1.0，纯口播版）

> 时长约 2 分 45 秒，语速适中可直接照读。括号内为停顿/动作提示，不念出。

---

玩 Beat Saber，你是不是也这样——点一首、打完、回菜单、再点下一首，反反复复，挑歌比打歌还累？今天给你安利一个插件：RandomPlaylistMod。一句话，它能把你的 Beat Saber 变成一台「随机音游电台」，设好时长往那一站，歌自己连着播。

（停顿）

这插件解决的就是「选歌疲劳」。你不用再一首首挑，它从你指定的多个歌单里随机抽歌、连续打，还能按难度筛选。而且现在已适配 Beat Saber 1.44，依赖清单也帮你列得明明白白。

（停顿）

核心就这么三样。第一，设时长，比如想练 30 分钟就填 30，时间一到自动结算；第二，按 NPS、也就是每秒音符数筛难度，新手卡个低区间，大佬直接拉满；第三，勾选多个歌单一起随机，等于把你的曲库混着播。点一下 Start Session，游戏就一首接一首给你上歌，中间完全不用人管。

（停顿）

最爽的是打歌过程中的操作。现在用手柄 B 键——注意是右手 B、左手 Y——短按一下，当前这首歌直接收藏进「RandomPlaylist Favorites」歌单；长按大概 0.7 秒，直接退出这次随机会话。提示就弹在屏幕上方，清清楚楚。这里我特意修过一笔：之前长按偶尔不灵，是 OpenXR 手柄按键会抖动，现在加了防抖，稳得很。

（停顿）

会话结束还有结算页，打了几首、练了多久、得分概览一目了然，历史也能回看。甚至能一键生成分享图，发个动态装个杯。

（停顿）

安装很简单，把 dll 和 manifest 丢进 Plugins 下的 RandomPlaylistMod 文件夹就行，记得先装好 PlaylistManager 这些依赖。链接放评论区。觉得有用点个赞，关注我，下期聊怎么用歌单管理你的整库。

---

## 相关链接（建议放评论区 / 简介置顶）

- RandomPlaylistMod 主仓库：https://github.com/xirain/RandomPlaylistMod
- RandomPlaylistMod 发布页（下载 DLL + manifest）：https://github.com/xirain/RandomPlaylistMod/releases
- 本期版本直链 v2.1.0：https://github.com/xirain/RandomPlaylistMod/releases/tag/v2.1.0
- PlaylistManager 1.44 适配 fork（必需依赖，推荐用此分支）：https://github.com/xirain/PlaylistManager
  - 1.44 分支：https://github.com/xirain/PlaylistManager/tree/1.44
  - 说明：官方 PlaylistManager 可能未适配 1.44， 请从该 fork 的 Release 下载或自行构建后放入 Plugins。
- 其余运行时依赖（通过BSManager安装即可）：BSIPA `^4.1.0`、SiraUtil `^3.0.0`、SongCore `^3.16.0`、BeatSaberMarkupLanguage `^1.6.0`、SongDetailsCache `^1.0.0`，`BeatSaberPlaylistsLib`，搭配CustomSabersLite和Counters更完美。
