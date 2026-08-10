# RandomPlaylistMod B站视频脚本（v2.1.0）

> 时长目标：约 2 分 45 秒（口播约 650-720 字，语速适中）
> 风格：音游宅向、轻松种草、少术语多演示
> 适用版本：Beat Saber 1.44，插件 v2.1.0

---

## 标题建议
用 RandomPlaylistMod 把 Beat Saber 变成你的私人随机音游电台｜1.44 可用

## 简介/封面文案
挑歌比打歌还累？这个插件帮你设好时长、按难度筛、多歌单混着随机连播，打歌时手柄 B 键短按收藏、长按退出。安装一行搞定。

---

## 分镜脚本

### [0:00 - 0:20] 开场钩子
- **画面**：游戏内连续切歌快剪（3-4 首歌的副歌片段）+ 插件名卡「RandomPlaylistMod」
- **口播**：
  玩 Beat Saber，你是不是也这样——点一首、打完、回菜单、再点下一首，反反复复，挑歌比打歌还累？今天给你安利一个插件：RandomPlaylistMod。一句话，它能把你的 Beat Saber 变成一台「随机音游电台」，设好时长往那一站，歌自己连着播。

### [0:20 - 0:50] 痛点 + 插件简介
- **画面**：左侧默认菜单选歌界面（疯狂划拉），右侧插件设置界面（干净的几项）
- **口播**：
  这插件解决的就是「选歌疲劳」。你不用再一首首挑，它从你指定的多个歌单里随机抽歌、连续打，还能按难度筛选。而且现在已适配 Beat Saber 1.44，依赖清单也帮你列得明明白白。

### [0:50 - 1:30] 核心功能演示
- **画面**：设置面板实操——拖时长滑块、设 NPS 区间、勾选多个歌单、点「Start Session」，然后看游戏自动连播
- **口播**：
  核心就这么三样。第一，设时长，比如想练 30 分钟就填 30，时间一到自动结算；第二，按 NPS、也就是每秒音符数筛难度，新手卡个低区间，大佬直接拉满；第三，勾选多个歌单一起随机，等于把你的曲库混着播。点一下 Start Session，游戏就一首接一首给你上歌，中间完全不用人管。

### [1:30 - 2:00] 手柄 B 键操作（本期重点新功能）
- **画面**：手柄特写标出 B 键 / 左手 Y 键；游戏内演示短按 B 弹「★ 已收藏」、长按 B 弹「已退出随机会话」
- **口播**：
  最爽的是打歌过程中的操作。现在用手柄 B 键——注意是右手 B、左手 Y——短按一下，当前这首歌直接收藏进「RandomPlaylist Favorites」歌单；长按大概 0.7 秒，直接退出这次随机会话。提示就弹在屏幕上方，清清楚楚。这里我特意修过一笔：之前长按偶尔不灵，是 OpenXR 手柄按键会抖动，现在加了防抖，稳得很。

### [2:00 - 2:20] 结算 / 历史 / 分享图
- **画面**：Session Summary 结算页、History 历史列表、生成的分享图
- **口播**：
  会话结束还有结算页，打了几首、练了多久、得分概览一目了然，历史也能回看。甚至能一键生成分享图，发个动态装个杯。

### [2:20 - 2:45] 安装与结尾 CTA
- **画面**：把 dll + manifest 拖进 Plugins/RandomPlaylistMod 目录；切到 GitHub Release 页面；结尾关注按钮动画
- **口播**：
  安装很简单，把 dll 和 manifest 丢进 Plugins 下的 RandomPlaylistMod 文件夹就行，记得先装好 PlaylistManager 这些依赖。链接放评论区。觉得有用点个赞，关注我，下期聊怎么用歌单管理你的整库。

---

## 拍摄/剪辑备注
- 实机画面优先，设置面板那一段最好录真人操作，比截图更有说服力。
- B 键演示务必录「屏幕上方弹提示」的特写，这是区别于旧版的关键卖点。
- BGM 用插件里随机到的曲库歌（注意版权），若无版权用无版权电音垫底。
- 结尾「链接放评论区」对应 GitHub Release：https://github.com/xirain/RandomPlaylistMod/releases
- 完整链接汇总（含 PlaylistManager 1.44 fork）见同目录口播稿 `bilibili_script_v2.1.0_口播稿.md` 末尾「相关链接」：
  - RandomPlaylistMod 仓库 / Release / v2.1.0 直链
  - PlaylistManager 1.44 适配 fork：https://github.com/xirain/PlaylistManager （分支 1.44：https://github.com/xirain/PlaylistManager/tree/1.44）
  - 其余依赖：BSIPA、SiraUtil、SongCore、BSML、SongDetailsCache（BeatMods / ModAssistant 安装）推荐使用BSManager安装，搭配RandomSaberLite更完美。

## 可选标题备选
1. 别再一首首点歌了！Beat Saber 随机连播插件上手
2. 手柄按一下就收藏？RandomPlaylistMod 1.44 实机演示
3. 把 Beat Saber 当电台听：RandomPlaylistMod 使用指南
