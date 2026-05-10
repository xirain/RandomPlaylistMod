# BSML 控件可见但在 VR 中无法操作：排查文档与本项目修复方案

## 背景
在 BSML 中把 `toggle` 改为 `checkbox-setting` 后，出现了「UI 能显示，但在 VR 里不能选中 `No Fail`」的问题。

本项目的核心问题是：`checkbox-setting` 被放在了 `horizontal` 布局容器内，且父容器高度/布局约束较紧，容易导致该控件的可交互区域（raycast/hitbox）异常。

## BSML 中“看得到但点不到”的常见原因

1. **Setting 类控件放在不合适的容器里**
   - `checkbox-setting`、`dropdown-list-setting` 这类 Setting 控件通常更适合直接作为一行布局，而不是塞进紧凑 `horizontal`。
   - 在复杂嵌套里可能出现视觉正常但事件区域不正确。

2. **父级布局裁切或尺寸不足**
   - `child-control-height='false'`、`preferred-width` 太小、`spacing/pad` 过紧，会让真实可点击区域被压缩。

3. **控件覆盖（Overlay）**
   - 同层或上层有 panel/list 覆盖到该区域时，激光射线命中的是覆盖物而不是控件本身。

4. **绑定字段与回调不一致**
   - `value="no-fail-enabled"` 对应的 `[UIValue]` 必须是 `bool`。
   - `on-change` 回调签名建议为 `void Method(bool value)`。

5. **初始化刷新时机不对**
   - 视图激活、重载数据时如果频繁重建，可能临时丢失交互状态。

## 参考实践（结合 CustomSabersLite 一类项目的常见写法）

- 将 `checkbox-setting` 作为单独一行（不要放入紧凑 `horizontal` 与文本并排）。
- 用 `text` 作为分组标题，Setting 控件独立摆放。
- 保持 `value + on-change` 的双向逻辑简单明确。

## 本项目已实施的修复

### 1) 布局修复（核心）
把 `No Fail` 从 `horizontal` 中移出，改为独立一行 `checkbox-setting`，减少布局冲突并扩大可交互区域。

### 2) 启动状态可视化（辅助）
在开始会话时将 `No Fail` 当前状态写入 `SessionStatus`，便于在 VR 内确认开关是否生效。

## 修复后验证步骤（VR）

1. 进入插件页面。
2. 使用手柄激光点选 `No Fail`。
3. 观察复选状态是否变化。
4. 点击 `Start` 后检查状态文案中是否显示 `No Fail: ON/OFF`。
5. 重复开/关并开始会话，确认每次都一致。

## 若仍有问题的进一步方案

- 把该行放到更上方，避免与 `list` 区域邻接。
- 给该区块增加更大 `pad`/`spacing`。
- 暂时改为普通 `button` 手动切换布尔值（交互最稳）。
- 在 `#post-parse` 和 `DidActivate` 打日志，确认视图未被重复覆盖。

## 结论
本次问题属于 BSML 布局与 Setting 控件组合导致的交互命中异常。将 `checkbox-setting` 独立成行后，通常可恢复 VR 中稳定可点击行为，同时保持与 `SessionSettings.NoFailEnabled` 的绑定一致。
