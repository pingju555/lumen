# Lumen 弹窗设计系统

> 版本：v1.0 · 2026-07-18  
> 单一事实来源：`src/lumen/Ui/Theme.xaml`（XAML 令牌 + 全局隐式样式）  
> C# 访问入口：`src/lumen/Ui/Theme.cs`（Theme.xxx 静态属性，用于代码构建弹窗）

---

## 1 · 色彩令牌

共 16 个命名画刷，分为背景体系、边框体系、文本体系、语义体系四大类。

| 令牌 Key | 色值 | 用途 |
|---|---|---|
| **BgBase** | `#1E1E1E` | 窗口/卡片主底色 |
| **BgSurface** | `#2D2D30` | 标题栏、输入框、标签页底色 |
| **BgSunken** | `#252526` | 列表、页脚、组合弹出面板底色 |
| **BgHover** | `#3A3D41` | 鼠标悬停背景 |
| **BgActive** | `#007ACC` | 选中/激活态背景（VS Code 蓝色） |
| **BgActivePressed** | `#005A99` | 按钮按下态 |
| **BorderDefault** | `#3F3F46` | 窗口/卡片/列表 1px 边框 |
| **BorderSoft** | `#555555` | 输入框内边框 |
| **TextPrimary** | `#FFFFFF` | 标题、选中文本、白色按钮文字 |
| **TextSecondary** | `#E6E6E6` | 正文、输入文字、标签 |
| **TextTertiary** | `#9A9A9A` | 提示文案、次要标签 |
| **TextDisabled** | `#6A6A6A` | 禁用状态文字、极弱提示 |
| **Accent** | `#007ACC` | 强调色（同 BgActive） |
| **OkGreen** | `#6AD17A` | 公式求值成功 green |
| **ErrRed** | `#E54B4B` | 错误提示 red |
| **UsedGreen** | `#34D399` | 已使用函数标记 green |

> **收敛说明**：将此前 8 种灰阶文本（`#E6E6E6`/`#F0F0F0`/`#C8C8C8`/`#D4D4D4`/`#B0B0B0`/`#9A9A9A`/`#8A8A8A`/`#777777`）收敛为以上 TextPrimary/Secondary/Tertiary/Disabled 共 4 档。

---

## 2 · 圆角令牌

| 令牌 Key | 值 | 适用场景 |
|---|---|---|
| **RadiusOuter** | `8` | 窗口/弹窗外框圆角 |
| **RadiusCard** | `6` | 内部卡片/流程卡片圆角 |
| **RadiusControl** | `4` | 按钮/输入框/下拉框圆角 |

**标题栏圆角**：顶部分别为 RadiusOuter（8,8,0,0）。  
**页脚圆角**：底部分别为 RadiusOuter（0,0,8,8）。

**间距惯例**：
- 窗口内边距：14px（`PropertyEditorPanel`）/ 16px（`SettingsWindow`）
- 标题栏 padding：`10,6`
- 列表 item padding：`6,4`
- 按钮 padding：`10,4`

---

## 3 · 全局隐式样式

挂载在 `App.Resources → Theme.xaml` 中，作用于所有 Window/UserControl 下的控件（含代码构建的 `new Button()` 等）。

### 3.1 Button

- 常态：`BgHover` 背景 + `BorderDefault` 边框 + `TextSecondary` 文字
- 悬停：`BgActive` 背景 + 白色文字
- 按下：`BgActivePressed` 背景
- 禁用：`BgSunken` + `TextDisabled`
- 圆角：`RadiusControl`（4）
- 光标：`Hand`

### 3.2 TextBox

- 常态：`BgBase` 背景 + `BorderSoft` 边框 + `TextSecondary` 文字
- 聚焦：边框 → `BgActive`（蓝色高亮）
- 禁用：`BgSunken` + `TextDisabled`
- 多行：通过 `VerticalContentAlignment` 绑定保证公式框顶对齐
- 圆角：`RadiusControl`（4）

### 3.3 ComboBox

- 按钮区：同 TextBox 风格的 ToggleButton（`BgSurface` + 箭头 Path）
- 弹出面板：`BgSunken` 背景 + `BorderDefault` 边框
- 选项悬停：`BgHover`
- 选项选中：`BgActive` + 白色文字
- 圆角：`RadiusControl`（4）

### 3.4 ListBox

- 背景：`BgSunken` · 边框：`BorderDefault`
- 选项悬停：`BgHover`
- 选项选中：`BgActive` + 白色文字

### 3.5 TabControl / TabItem

- 内容区：`BgBase` 底色 · 监听项：`BgSurface`
- 选中项：`BgActive` + 白色文字
- 悬停项：`BgHover`
- 圆角：顶标签 6,6,0,0 · 内容区底部 6,6

### 3.6 TreeView / TreeViewItem

- 背景：`BgSunken` · 选中：`BgActive` + 白色文字
- 悬停：`BgHover`

### 3.7 CheckBox / RadioButton

- 方块：`BgBase` + `BorderDefault` 边框
- 选中：`BgActive` 填充 + 白色勾/点
- 悬停：边框 → `BgActive`

---

## 4 · 窗口结构约定

所有弹窗采用统一的**自定义标题栏 · 无边框**模式。

### 4.1 窗口 Shell 模板

```xml
<Window ... WindowStyle="None" AllowsTransparency="True" Topmost="True"
        ShowInTaskbar="False" ResizeMode="NoResize"
        Background="{StaticResource BgBase}"
        Foreground="{StaticResource TextSecondary}">
  <Border BorderBrush="{StaticResource BorderDefault}" BorderThickness="1"
          CornerRadius="{StaticResource RadiusOuter}">
    <Grid>
      <Grid.RowDefinitions>
        <RowDefinition Height="Auto"/>     <!-- 标题栏 -->
        <RowDefinition Height="*"/>        <!-- 主体 -->
        <RowDefinition Height="Auto"/>     <!-- 页脚（可选） -->
      </Grid.RowDefinitions>

      <!-- 标题栏 -->
      <Border Grid.Row="0" Background="{StaticResource BgSurface}"
              CornerRadius="8,8,0,0" Padding="10,6"
              MouseLeftButtonDown="Header_MouseDown">
        <Grid>
          <Grid.ColumnDefinitions>
            <ColumnDefinition Width="*"/>
            <ColumnDefinition Width="Auto"/>   <!-- ✕ 关闭按钮（可选） -->
          </Grid.ColumnDefinitions>
          <TextBlock Text="..." FontSize="13" FontWeight="Bold"
                     Foreground="{StaticResource TextPrimary}"/>
        </Grid>
      </Border>
    </Grid>
  </Border>
</Window>
```

### 4.2 窗口类型一览

| 窗口 | 文件 | 标题栏 |
|---|---|---|
| PropWindow | `Ui/PropWindow.xaml` | BgSurface + Clear Btn |
| SettingsWindow | `Ui/SettingsWindow.xaml` | 无（原生标题栏） |
| TreeWindow | `Ui/TreeWindow.xaml` | BgSurface + Header_MouseDown |
| ProfileWindow | `Ui/ProfileWindow.xaml` | 无标题栏（卡片风格） |
| PageGridBgWindow | `Ui/PageGridBgWindow.xaml` | BgSurface + ✕ 关闭 |
| GvManagerPanel | `Ui/GvManagerPanel.xaml` | BgSurface + ✕ 关闭 |
| PropertyEditorPanel | `Ui/PropertyEditorPanel.xaml` | 无标题栏（内嵌卡片） |
| ColorPickerWindow | `Ui/ColorPickerWindow.xaml.cs` | BgSurface（代码构建） |
| InputBox | `Ui/InputBox.cs` | 无标题栏（迷你卡片） |
| FilePickerWindow | `Ui/FilePickerWindow.cs` | BgSurface + 拖拽（代码构建） |

> SettingsWindow 延续原生标题栏（未改造为自定义），其余均统一为自定义标题栏。

### 4.3 代码构建弹窗的 Theme 引用

代码中引用令牌：

```csharp
// 正确：从 Theme 类获取画刷（Theme.cs 从 Application.Resources 读取）
BorderBrush = Theme.BorderDefault;
Foreground = Theme.TextPrimary;

// 错误：直接 new SolidColorBrush(...)
```

Theme 类使用 `Application.Current.TryFindResource` 从合并的 Theme.xaml 读取令牌，组件未加载时回退到硬编码 RGB。

---

## 5 · 组件映射

| 原子类型 | 属性编辑器 Tab | 核心字段 |
|---|---|---|
| Text | 文本/位置/动画/流程 | 内容、字体、大小、颜色、对齐 |
| Shape | 形状/位置/动画/流程 | 类型、宽高、圆角、描边、质感 |
| Icon | 图标/位置/动画/流程 | 名称、大小、颜色 |
| Image | 图像/位置/动画/流程 | 路径、缩放模式 |
| Progress | 进度/位置/动画/流程 | 值、最大值、样式 |
| StackGroup | 布局/位置/动画/流程 | 方向、间距、对齐 |
| OverlapGroup | 布局/位置/动画/流程 | 叠层顺序 |
| SeriesGroup | 布局/位置/动画/流程 | 序列数据源 |

---

## 6 · 设计决策记录

| 决策 | 结论 | 日期 |
|---|---|---|
| 圆角统一 | RadiusOuter=8 / RadiusCard=6 / RadiusControl=4 | 2026-07-18 |
| 文本灰阶 | 8 种收敛为 4 档令牌 | 2026-07-18 |
| 全局样式 | 隐式 TargetType 挂 App.Resources，不设 x:Key | 2026-07-18 |
| 代码弹窗 | Theme.cs 从 Application.TryFindResource 读取 | 2026-07-18 |
| 控件按钮 | 不保留步进按钮，仅输入框 + 拖拽条 | 2026-07-17 |
| FilePicker | 从 ToolWindow 改为自定义标题栏（None + 拖拽） | 2026-07-18 |
| 锚点体系 | 保留九宫格锚点 + XY 偏移 | 2026-07-17 |
| 公式选择器 | 二级菜单体系 + 顶部输入预览 + 已用函数重组 | 2026-07-18 |

---

## 7 · 文件索引

```
src/lumen/Ui/Theme.xaml            ← 令牌 + 全局隐式样式
src/lumen/Ui/Theme.cs               ← C# 画刷访问器
src/lumen/App.xaml                   ← 合并 Theme.xaml
src/lumen/Ui/PropWindow.xaml         ← 属性窗口
src/lumen/Ui/SettingsWindow.xaml     ← 设置窗口
src/lumen/Ui/TreeWindow.xaml         ← 原子树窗口
src/lumen/Ui/ProfileWindow.xaml      ← 配置档窗口
src/lumen/Ui/PageGridBgWindow.xaml   ← 页面/网格/背景/场景
src/lumen/Ui/GvManagerPanel.xaml     ← 全局变量管理器
src/lumen/Ui/PropertyEditorPanel.xaml ← 属性编辑器面板
src/lumen/Ui/ColorPickerWindow.xaml.cs ← 取色器
src/lumen/Ui/InputBox.cs              ← 模态输入框
src/lumen/Ui/FilePickerWindow.cs      ← 文件选择器
src/lumen/Ui/ActionEditor.cs          ← 动作编辑器
```

---

## 附录：扩展指南

### 添加新弹窗

1. 新建 XAML Window 文件，参考第 4 节的窗口 Shell 模板
2. 所有 Button/TextBox/ComboBox 继承全局样式，无需重复定义
3. 颜色引用 `{StaticResource Token}`，代码中引用 `Theme.xxx`
4. 如果是纯代码 Window，建完后检查硬编码色是否可替换为 `Theme.xxx`

### 修改令牌

- 仅改 `Theme.xaml` 中 `{x:Key}` 的 `Color` 值
- `Theme.cs` 属性自动从 Application.Resources 读取，无需改 C#
- 所有弹窗（XAML + 代码）同步生效

### 添加新控件类型

- 在 `Theme.xaml` 中新增 `Style TargetType="..."`（无 x:Key）
- 确保与现有令牌体系一致
- 可能需要更新 `Theme.cs`（如无代码弹窗使用，可不更新）
