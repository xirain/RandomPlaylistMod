# BSML 开发避坑指南

> 本文档总结了开发 RandomPlaylistMod 过程中遇到的所有 BSML 相关问题，以便后续开发快速排查和避免重复踩坑。

---

## 1. BSML 文件必须包含根元素

### 问题
BSML 文件如果只有裸标签（如单独一个 `<text>`），解析时会报 `Invalid BSML` 错误。

### 正确写法
每个 `.bsml` 文件**必须**有根元素 `<bg>`，且包含 XML 声明和命名空间：

```xml
<?xml version="1.0" encoding="utf-8"?>
<bg xmlns:xsi='http://www.w3.org/2001/XMLSchema-instance' xsi:schemaLocation='https://monkeymanboy.github.io/BSML-Docs/ https://raw.githubusercontent.com/monkeymanboy/BSML-Docs/gh-pages/BSMLSchema.xsd'>
    <text text="hello" font-size="5"/>
</bg>
```

### 错误写法
```xml
<!-- ❌ 缺少根元素 bg -->
<text text="hello" font-size="5"/>
```

### 规则
- 根元素通常是 `<bg>`
- 必须有 `<?xml version="1.0" encoding="utf-8"?>` 声明
- 必须有 `xmlns:xsi` 和 `xsi:schemaLocation` 命名空间声明

---

## 2. BSML 属性名必须用连字符（kebab-case）

### 问题
BSML 的属性名统一使用 **kebab-case**（连字符分隔），而不是 PascalCase 或 camelCase。用错大小写或分隔符会导致属性被忽略或报 Invalid BSML。

### 对照表

| C# 代码中的名称 | BSML 属性名 | 说明 |
|---|---|---|
| `FontSize` | `font-size` | ✅ 正确 |
| `FontColor` | `font-color` | ✅ 正确 |
| `PreferredSize` | `pref-width` / `pref-height` | 使用别名 |
| `WordWrapping` | `word-wrapping` | ✅ 正确 |
| `ApplyOnChange` | `apply-on-change` | ✅ 正确 |
| `IntegerOnly` | `integer-only` | ✅ 正确 |
| `HoverHint` | `hover-hint` | ✅ 正确 |
| `AnchorPosX` | `anchor-pos-x` | ✅ 正确 |
| `RichText` | `rich-text` | ✅ 正确 |
| `OverflowMode` | `overflow-mode` | ✅ 正确 |

### 别名快捷方式

部分属性有短别名，两套写法等价：

| 完整属性 | 别名 |
|---|---|
| `preferred-width` | `pref-width` |
| `preferred-height` | `pref-height` |
| `font-align` | `align` |
| `font-color` | `color` |
| `local-scale` | `scale` |
| `anchored-position-x` | `anchor-pos-x` |
| `anchored-position-y` | `anchor-pos-y` |

---

## 3. 数据绑定语法：`~` vs `{}`

### 问题
BSML 有两种绑定语法，用途不同，混用会出错。

### 规则

| 语法 | 用途 | 示例 |
|---|---|---|
| `~name` | 绑定 `[UIValue("name")]` 属性 | `text="~hud-text"` |
| `{name}` | 内插替换（只读显示） | `text="{session-status}"` |

### 区别
- **`~` 绑定**：双向绑定，值变化时自动更新 UI，适合可编辑控件（slider、input 等）
- **`{}` 内插**：单向显示，适合纯文本标签
- **不可混用**：`text="~{name}"` 是错误的

---

## 4. slider-setting 与 increment-setting 的选择

### 问题
`increment-setting` 在 VR 中的 +/- 按钮距离文字很远，无法用 VR 指针点击。

### 对比

| 特性 | `slider-setting` | `increment-setting` |
|---|---|---|
| VR 交互 | ✅ 滑动条易于操作 | ❌ +/- 按钮间距过大 |
| 数值调整 | 拖动滑块 | 点击 +/- |
| 推荐场景 | 所有 VR 设置面板 | 仅 PC 端 |

### slider-setting 完整有效属性

```xml
<slider-setting
    text="标签"
    value="binding-name"        <!-- UIValue 绑定 -->
    min="0"                     <!-- 最小值 -->
    max="300"                   <!-- 最大值 -->
    increment="10"              <!-- 步进值 -->
    integer-only="true"         <!-- 是否整数模式 -->
    apply-on-change="true"      <!-- 变化时立即应用 -->
    show-buttons="true"         <!-- 是否显示两侧 +/- 按钮 -->
    hover-hint="提示文字"
/>
```

### ⚠️ 关键注意事项
- **`integer-only="true"` 是合法属性**（官方文档确认），如果添加后报 Invalid BSML，问题不在该属性本身，而是其他语法错误
- `value` 绑定的 C# 属性类型：
  - `integer-only="true"` 时 → 用 `int` 或 `float`（BSML 会自动转换）
  - 不设置时 → 必须用 `float`
- `min`/`max`/`increment` 值类型与 `integer-only` 匹配即可

---

## 5. font-color 必须用十六进制色值

### 问题
`font-color` 使用 CSS 颜色名（如 `white`、`red`）可能不被 BSML 解析。

### 正确写法
```xml
<text text="hello" font-color="#FFFFFF"/>  <!-- ✅ 白色 -->
<text text="hello" font-color="#FF0000"/>  <!-- ✅ 红色 -->
<text text="hello" font-color="#000000FF"/>  <!-- ✅ 带透明度 ARGB -->
```

### 错误写法
```xml
<text text="hello" font-color="white"/>   <!-- ❌ 可能无效 -->
<text text="hello" font-color="red"/>     <!-- ❌ 可能无效 -->
```

---

## 6. 新增 .bsml 文件必须注册为 EmbeddedResource

### 问题
新增了 `.bsml` 文件但忘记在 `.csproj` 中注册，运行时找不到资源，报 Invalid BSML 或空视图。

### 操作步骤
1. 创建 `.bsml` 文件
2. 在 `.csproj` 的 `<ItemGroup>` 中添加：
   ```xml
   <EmbeddedResource Include="UI\Views\YourView.bsml" />
   ```
3. C# 中用 `[ViewDefinition]` 引用，路径与命名空间对应：
   ```csharp
   [ViewDefinition("RandomPlaylistMod.UI.Views.YourView.bsml")]
   ```

### 命名规则
- 文件路径：`UI/Views/YourView.bsml`
- 嵌入资源名：`RandomPlaylistMod.UI.Views.YourView.bsml`（命名空间 + 文件夹 + 文件名）
- `csproj` 中用反斜杠：`UI\Views\YourView.bsml`

---

## 7. 布局容器标签清单

### 可用的根/容器标签

| 标签 | 用途 | 关键属性 |
|---|---|---|
| `<bg>` | 根容器/背景 | `background`, `bg` |
| `<vertical>` | 垂直布局 | `spacing`, `child-control-height`, `child-expand-height` |
| `<horizontal>` | 水平布局 | `spacing`, `pad-left`, `pad-right` |
| `<grid-layout>` | 网格布局 | `cell-size-x`, `cell-size-y`, `spacing` |
| `<scroll-view>` | 滚动视图 | `preferred-height` |
| `<scrollable-0-container>` | 可滚动设置容器 | — |

### 常用布局属性

```xml
<vertical spacing="2" child-control-height="true" child-expand-height="false">
```
- `child-control-height="true"` → 子元素高度由内容决定
- `child-expand-height="false"` → 子元素不撑满剩余空间
- `spacing="2"` → 子元素间距

---

## 8. Invalid BSML 排查流程

遇到 `Invalid BSML` 错误时，按以下顺序排查：

```
1. 检查根元素
   └─ 是否有 <bg> 根元素？
   └─ 是否有 XML 声明和命名空间？

2. 检查属性名
   └─ 是否使用了 PascalCase？（应改为 kebab-case）
   └─ 是否使用了不存在的属性？（对照官方文档）

3. 检查绑定语法
   └─ ~ 是绑定，{} 是内插，不可混用
   └─ C# 侧 [UIValue] 名必须与 BSML 中一致

4. 检查颜色值
   └─ font-color 是否用了颜色名？（应改为 #RRGGBB）

5. 检查资源注册
   └─ .csproj 中是否有对应的 EmbeddedResource？
   └─ [ViewDefinition] 路径是否与资源名一致？

6. 检查标签嵌套
   └─ <list> 必须在 <vertical> 等容器内
   └─ <slider-setting> 等设置控件建议放在 <scrollable-settings-container> 内

7. 查看游戏日志
   └─ `_latest.log` 中搜索 "BSML" 或 "Parse" 相关错误
   └─ 通常会指出具体哪一行、哪个属性出错
```

---

## 9. 常用标签属性速查

### `<text>` 标签

| 属性 | 别名 | 类型 | 说明 |
|---|---|---|---|
| `text` | — | string | 显示文本 |
| `font-size` | — | float | 字体大小 |
| `font-color` | `color` | #RRGGBB | 字体颜色 |
| `font-align` | `align` | TextAlignmentOptions | 对齐方式 |
| `bold` | — | bool | 加粗 |
| `italics` | — | bool | 斜体 |
| `word-wrapping` | — | bool | 自动换行 |
| `rich-text` | — | bool | 富文本 |
| `id` | — | string | 组件 ID（配合 [UIComponent]） |

### `<button>` 标签

| 属性 | 类型 | 说明 |
|---|---|---|
| `text` | string | 按钮文字 |
| `on-click` | string | [UIAction] 绑定名 |
| `pref-width` | float | 偏好宽度 |
| `hover-hint` | string | 悬停提示 |

### `<list>` 标签

| 属性 | 类型 | 说明 |
|---|---|---|
| `id` | string | [UIComponent] 绑定名 |
| `list-style` | ListStyle | `List` 或 `Box` |
| `select-cell` | string | 点击回调 [UIAction] |
| `preferred-height` | float | 列表高度 |
| `min-height` | float | 最小高度 |

---

## 10. 实际踩坑记录

### 2025-05-09: SessionHudView.bsml 缺少根元素
- **现象**: Invalid BSML
- **原因**: `.bsml` 文件只有裸 `<text>` 标签，缺少 `<bg>` 根元素和命名空间
- **修复**: 补充完整的 XML 结构

### 2025-05-09: integer-only 属性误判为无效
- **现象**: 尝试移除 `integer-only="true"` 来修复 Invalid BSML
- **原因**: 实际问题不在 `integer-only`（该属性官方确认合法），而是同一文件的其他 BSML 语法错误
- **教训**: 排查 Invalid BSML 时，不要盲目移除属性，应逐项对照官方文档确认

### 2025-05-09: increment-setting VR 不可用
- **现象**: VR 中无法点击 +/- 按钮（按钮与文字间距过大）
- **原因**: `increment-setting` 的按钮区域 `sizeDelta=(40,0)` 导致 VR 指针难以命中
- **修复**: 替换为 `slider-setting`（滑动条更适合 VR 交互）

---

## 参考链接

- [BSML 官方文档](https://monkeymanboy.github.io/BSML-Docs/)
- [BSML Tags 索引](https://monkeymanboy.github.io/BSML-Docs/Tags/)
- [SliderSettingTag 文档](https://monkeymanboy.github.io/BSML-Docs/Tags/SliderSettingTag/)
- [IncrementSettingTag 文档](https://monkeymanboy.github.io/BSML-Docs/Tags/IncrementSettingTag/)
- [TextTag 文档](https://monkeymanboy.github.io/BSML-Docs/Tags/TextTag/)
- [BSML GitHub](https://github.com/monkeymanboy/BeatSaberMarkupLanguage)
