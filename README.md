# SuperClip

> **Windows 超级剪贴板** · Native WPF / .NET 8 desktop
> 一个常驻后台的剪贴板增强工具：自动记录、Excel 表格拆分、双粘贴模式、进程绑定、托盘热键。

[![Platform](https://img.shields.io/badge/platform-Windows%207%2B%20x64-0078d4?logo=windows)](https://www.microsoft.com/windows)
[![Framework](https://img.shields.io/badge/.NET-Core%203.1-512BD4?logo=dotnet)](https://dotnet.microsoft.com/)
[![UI](https://img.shields.io/badge/WPF-MVVM-6e3fe7)](https://learn.microsoft.com/dotnet/desktop/wpf/)
[![License](https://img.shields.io/badge/license-MIT-green)](LICENSE)
[![Version](https://img.shields.io/badge/version-2.0.0-blue)](CHANGELOG.md)
[![Offline](https://img.shields.io/badge/network-offline-success?logo=cloud-off)](#-隐私与离线保证)

[English](#english) | [中文](#中文)

---

## 中文

### ✨ 这是什么

SuperClip 是一个 Windows 桌面剪贴板增强工具，**常驻后台 + 托盘图标**运行。

- 复制任何文本，自动入列表
- 复制 Excel 表格，按行列自动拆成独立单元格
- 单击 / 双击 / 空格键，三种方式粘贴
- 跨进程精准粘贴：可绑定到 Excel / 浏览器 / 任意窗口
- 收藏置顶、搜索过滤、一键复位

完全本地运行，**零网络依赖**，可用防火墙验证。

### 🎯 核心特性

| 特性 | 描述 |
|---|---|
| **剪贴板历史** | 独立隐藏窗口后台监听（`WM_CLIPBOARDUPDATE`），不依赖主窗口显隐，**收起后仍持续工作** |
| **SHA-256 去重** | 相同内容只保留一条，时间戳更新 |
| **Excel 表格拆分** | 含 `\t` 的 TSV 自动按「行→列」拆成独立单元格，标注「来自表格：第 X 行 第 Y 列」 |
| **复制模式切换** | 一般复制（多行纯文本不拆）/ 表格复制（单列也按单元格拆） |
| **300ms 搜索防抖** | 顶部搜索框实时过滤内容 + 来源标注 |
| **类型筛选** | 全部 / 文本 / 表格 / 收藏 |
| **收藏 ★/☆** | 置顶分组、永久保留、不参与 500 条自动清理 |
| **双粘贴模式** | 普通模式（双击 = 粘贴，位置不变）；快速模式（单击 = 选中，空格 = 粘贴，沉底变灰，顶部自动选中下一条） |
| **进程绑定** | 工具栏靶心图标（🔴 红 = 未绑定，🟢 绿 = 已绑定），点击进入「点选模式」（十字光标 + 全局鼠标钩子） |
| **全局热键** | `Ctrl + \`` 呼出/隐藏 |
| **托盘常驻** | 收起后托盘图标保留；双击恢复、右键菜单「打开/退出」 |
| **悬浮置顶** | 窗口默认置顶，可关闭 |
| **单实例** | 全局命名 Mutex 防重复打开 |
| **新手引导** | 9 步帮助窗口，首次使用清晰 |
| **右键菜单** | 主窗口任意位置右键弹出 |
| **状态栏** | 左侧绑定进程名，右侧署名 |

### 📸 截图

> 启动后默认停靠在屏幕右侧

```
┌─ SuperClip ───────────────────┐
│ ─── [📌] [✕]                  │ ← 标题栏：收起 / 置顶 / 关闭
├───────────────────────────────┤
│ 请输入搜索内容                │ ← 搜索框（含占位提示）
│ [全部▾] [⊙][⊙] 清除  复位     │ ← 筛选 / 绑定 / 操作按钮
├───────────────────────────────┤
│ 1  cell-A1           [★]      │
│ 2  cell-B1           [☆]      │
│ 3  cell-A2 (粘贴过)   [☆]      │ ← 灰显已粘贴
│ 4  来自表格：第 2 行 第 2 列 [☆]│
│   excel_value                   │
├───────────────────────────────┤
│ EXCEL · …            Mr lin   │ ← 状态栏：绑定名 + 署名
└───────────────────────────────┘
```

### 🚀 快速开始

#### 用户（直接使用）

1. 在 [Releases](https://github.com/your-username/SuperClip/releases) 下载 `SuperClip_v2.0_便携版.zip`
2. 解压到任意目录
3. 右键 `install.bat` → 「以管理员身份运行」
4. 双击桌面「SuperClip」图标启动
5. 之后按 `Ctrl + \`` 随时呼出

#### 开发者（从源码构建）

```bash
git clone https://github.com/your-username/SuperClip.git
cd SuperClip
build.bat
```

产物：`bin\Release\netcoreapp3.1\win-x64\publish\SuperClip.exe`（单文件，约 130 MB）

> ⚠️ **不要加 `-p:PublishTrimmed=true`**。WPF 框架不支持 IL 剪裁（NETSDK1168）。
> ⚠️ **不要启用 `InvariantGlobalization`**。WPF TextBox 内部会查 `InputLanguageManager.CurrentInputLanguage`（LCID 2052 = zh-CN 等），会让中文/日文 IME 用户启动崩溃。

### 🛠️ 技术栈

| 层级 | 技术 |
|---|---|
| **语言** | C# 12（实际用 C# 9 语法以兼容 .NET Core 3.1） |
| **运行时** | .NET Core 3.1（自包含，目标机无需装运行时；选 3.1 是为了支持 Windows 7，.NET 5+ 仅支持 Win10/11） |
| **UI** | WPF + MVVM（CommunityToolkit.Mvvm 8.2.2） |
| **系统集成** | 纯 Win32 P/Invoke（剪贴板监听、托盘、热键、键盘模拟） |
| **持久化** | System.Text.Json + 本地文件（`%AppData%\SuperClip\history.json`） |
| **分发** | 单文件自包含 + ReadyToRun（启动快） |

### 🔒 隐私与离线保证

- **完全离线运行**：无网络请求、无遥测、无分析
- **可断网运行**：拔网线后所有功能正常
- **可防火墙验证**：把 SuperClip.exe 加入防火墙黑名单，所有功能照常
- **数据本地化**：历史记录仅存在 `%AppData%\SuperClip\history.json`

### 📁 项目结构

```
SuperClip/
├── SuperClip.csproj           # 项目文件（含发布配置）
├── App.xaml(.cs)              # 应用入口、单实例 Mutex
├── Models/                    # 数据模型
│   └── ClipItem.cs            # 单条剪贴板记录
├── Services/                  # 服务层
│   ├── StorageService.cs          # JSON 持久化（原子写）
│   ├── ClipboardMonitorService.cs # 剪贴板监听（WM_CLIPBOARDUPDATE）
│   ├── TableParser.cs             # Excel TSV 拆分
│   ├── PasteService.cs            # 模拟键入（写剪贴板 + Ctrl+V）
│   └── TrayService.cs             # 系统托盘
├── ViewModels/                # MVVM 视图模型
│   └── MainViewModel.cs        # 主逻辑、增量更新列表
├── Views/                     # 视图
│   ├── MainWindow.xaml(.cs)   # 主窗口
│   ├── HelpWindow.xaml(.cs)   # 新手引导
│   └── Converters.cs          # XAML 值转换器
├── Native/
│   └── WinApi.cs              # Win32 P/Invoke 封装
├── installer/                 # 便携安装包脚本
│   ├── install.bat            # 装到 %ProgramFiles% + 桌面/开始菜单快捷方式
│   └── uninstall.bat          # 逆向清理
├── IsExternalInit.cs          # 兼容 polyfill（.NET 5+ 框架自带，3.1 引用程序集缺失；用于 C# 9 init 访问器）
├── build.bat                  # 一键发布
├── CleanAndBuild.bat          # 清理缓存 + 发布
└── PackageRelease.bat         # 发布 + 打 ZIP 便携包
```

### ⌨️ 快捷键

| 快捷键 | 行为 |
|---|---|
| `Ctrl + \`` | 呼出 / 隐藏主窗口 |
| 普通模式 + 双击 | 粘贴到光标处（条目变灰，位置不变） |
| 快速模式 + 单击 | 选中条目（不粘贴） |
| 快速模式 + 空格 | 粘贴到光标处（条目沉底变灰，顶部自动选中下一条） |
| `Esc`（点选模式） | 取消点选窗口 |

### 🐛 故障排查

| 症状 | 原因 / 修复 |
|---|---|
| **启动报 `CultureNotFoundException`** | **别启用 `InvariantGlobalization=true`**！WPF TextBox 内部会查 `InputLanguageManager.CurrentInputLanguage`（LCID 2052 = zh-CN 等），invariant 模式会让中文/日文 IME 用户崩溃 |
| **编译报 `NETSDK1168`** | 删掉命令里的 `-p:PublishTrimmed=true`，WPF 不支持 IL 剪裁 |
| **编译报 `NETSDK1175`** | 项目不应使用 Windows Forms（已重构为纯 Win32） |
| **编译报 `CS0518` `IsExternalInit`** | polyfill 文件被删除，从 `IsExternalInit.cs` 恢复 |
| **剪贴板复制实时性差** | Excel 复制时剪贴板被源程序短暂锁住，代码已重试 6 次（每次 25ms） |
| **托盘图标消失** | 重启资源管理器（任务管理器 → 重新启动 explorer.exe） |

### 📜 更新日志

详见 [CHANGELOG.md](CHANGELOG.md)。

### 📄 许可证

[MIT](LICENSE) © Mr lin

---

## English

### What is this

SuperClip is a Windows desktop clipboard enhancer that runs in the background with a system tray icon.

- Automatically records anything you copy
- Splits Excel TSV tables into individual cells
- Three paste modes: single click, double click, spacebar
- Cross-process precise paste: bind to Excel / browser / any window
- Favorites, search filter, one-click reset

**100% local, zero network dependency** — verifiable with firewall.

### Tech Stack

| Layer | Tech |
|---|---|
| Language | C# 12 |
| Runtime | .NET Core 3.1 (self-contained; chosen for Windows 7 support — .NET 5+ only supports Win10/11) |
| UI | WPF + MVVM (CommunityToolkit.Mvvm 8.2.2) |
| System | Pure Win32 P/Invoke |
| Persistence | System.Text.Json + local file |
| Distribution | Single-file self-contained + ReadyToRun |

### Build

```bash
git clone https://github.com/your-username/SuperClip.git
cd SuperClip
build.bat
```

Output: `bin\Release\netcoreapp3.1\win-x64\publish\SuperClip.exe` (~130 MB)

> ⚠️ **Do NOT add `-p:PublishTrimmed=true`** — WPF doesn't support IL trimming (NETSDK1168).

### License

[MIT](LICENSE) © Mr lin
