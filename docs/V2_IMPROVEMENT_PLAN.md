# RandomPlaylistMod v2 改进设计与任务拆解

## 1. 目标

基于 v1 可用版本，v2 聚焦三件事：

1. **主界面可读性与可操作性提升**（信息分区更清晰，状态反馈更直接）。
2. **新增“不死模式（No Fail）”可配置能力**。
3. **建立可扩展设置结构**，后续新增设置时不需要重构核心会话逻辑。

---

## 2. 现状（来自 README 与代码）

- 已有基础能力：时长、歌单多选、NPS 筛选、会话启动/结束、自动切歌、失败跳过。
- 设置入口集中在 `RandomPlaylistUI`，当前设置主要是“即时字段”，扩展新选项时容易让 UI/会话参数耦合增加。
- 会话启动时目前 `GameplayModifiers` 固定为默认值，未暴露 No Fail 开关。

---

## 3. 详细设计

## 3.1 设置域模型（可扩展基础）

新增 `SessionSettings` 作为“单一会话设置快照”：

- `DurationMinutes`
- `MinNps`
- `MaxNps`
- `NoFailEnabled`

设计原则：

- UI 层可继续保留输入控件状态，但**启动会话时统一组装为 SessionSettings**。
- `PlaySessionManager` 仅接收设置对象，不再依赖零散参数，降低未来新增设置时的改动面。

## 3.2 UI 改进

主界面按区块组织：

1. Session（时长 + Start/End）
2. Filter（NPS 快捷）
3. Gameplay Settings（No Fail Toggle）
4. Status（当前会话状态 + 估算）
5. Playlists（列表与全选控制）

No Fail 用 toggle 暴露，文案清晰，便于用户理解该选项影响的是关卡失败判定。

## 3.3 会话管理改进

- 新增 `StartSession(SessionSettings settings)`。
- 兼容旧入口 `StartSession(int durationMinutes)`，内部转发到新入口，避免已有调用点一次性破坏。
- 启动关卡时根据 `NoFailEnabled` 构造 `GameplayModifiers`。

---

## 4. 任务拆解（执行顺序）

1. **Task A - 设计文档落地**：新增本设计文档。
2. **Task B - 设置模型**：新增 `SessionSettings`。
3. **Task C - PlaySessionManager 接口升级**：支持设置对象启动；在 `StartLevel` 应用 No Fail。
4. **Task D - UI 调整**：新增 No Fail toggle，启动时通过设置对象传递。
5. **Task E - 回归检查**：运行现有单元测试，确认未破坏测试工程。

---

## 5. 后续可扩展建议（v2.x）

- 增加 `SessionSettingsVersion`，为未来配置持久化做兼容准备。
- 将 UI 的设置控件映射抽象成设置项注册表（如 key + getter/setter）。
- 增加“保存默认设置”与“按难度模板恢复”的 UX 能力。
