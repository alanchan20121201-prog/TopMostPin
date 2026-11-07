# TopMostPin

一个纯托盘的 Windows 小工具：把鼠标指向任意窗口，按一下热键就能把它**锁定置顶**，再按一次取消。

没有主界面，没有后台服务，不联网，不下载任何东西——点开就是托盘里一个图标。

## 特性

- **一个热键搞定**：默认 `Ctrl+Alt+L`，鼠标指哪个窗口就锁哪个，再按一次解锁
- **多窗口同时锁定**：可以同时锁好几个窗口，托盘图标右下角显示当前锁定数量
- **锁定反馈**：锁定瞬间闪一下蓝色边框（0.55 秒保持 + 0.35 秒淡出），告诉你锁的是哪个窗口，随后自动消失，不会长期遮挡窗口边缘
- **不干扰原生操作**：锁定后照样可以最小化、拖动、缩放，工具不会拦截你的任何操作
- **自动维持置顶**：窗口被别的程序抢了层级会自动提回来；被最小化后恢复也会自动归位
- **失败有提示**：管理员窗口、全屏应用、系统窗口、热键冲突都会给出明确原因，不会静默失败
- **提示即时出现**：用自绘浮窗而不是系统托盘气泡 —— 气泡在 Win11 上首次弹出常延迟 0.5~2 秒，且关掉系统通知后完全不显示
- **轻量**：单文件 194 KB（框架依赖版），无第三方 UI 框架

## 快速开始

直接用现成的 exe

仓库里的 `publish/TopMostPin.exe` 就是编译好的单文件版本，双击即可运行。

> ⚠️ 这是**框架依赖**版本，需要机器上装有 **.NET 8 Desktop Runtime**（不是 SDK，只有运行时即可）。
> 没装的话双击会提示找不到运行时，去这里下载：<https://dotnet.microsoft.com/download/dotnet/8.0> → ".NET Desktop Runtime 8.0.x"

## 使用说明

1. 双击 exe，托盘出现一个蓝色箭头图标（没有窗口，别怀疑它没启动）
2. 鼠标指向任意窗口，按 `Ctrl+Alt+L` → 该窗口置顶，同时边缘闪一下蓝色边框表示锁定成功
3. 再按一次 `Ctrl+Alt+L` → 取消锁定
4. 想锁多个窗口就重复第 2 步，托盘角标会显示数字
5. 退出：右键托盘图标 → 退出

### 托盘右键菜单

| 菜单项 | 说明 |
| --- | --- |
| 修改快捷键（Ctrl+Alt+L） | 打开对话框，直接按新的组合键即可录制 |
| 全部解锁（N） | 一次性取消所有锁定，没有锁定时灰显 |
| 开机自启 | 写 `HKCU\...\CurrentVersion\Run`，不需要管理员权限 |
| 退出 | 退出前会把所有窗口恢复成非置顶 |

双击托盘图标会弹一个提示浮窗，显示当前快捷键和用法。

## 工作原理

```
按下热键
  └─ RegisterHotKey 的 WM_HOTKEY
       └─ GetCursorPos → WindowFromPoint → GetAncestor(GA_ROOT)   取鼠标下的顶层窗口
            └─ 校验：系统窗口 / 全屏应用 / 管理员权限
                 └─ SetWindowPos(HWND_TOPMOST, SWP_NOACTIVATE)     置顶
                      └─ 闪一下高亮边框（0.9 秒后自动淡出）
```

**置顶维持**分两条触发路径，都是"先检测再动作"，不会无脑重排 Z 序：

- 事件驱动：`SetWinEventHook` 监听前台切换、最小化恢复、窗口销毁
- 低频心跳：每 1 秒检查一次（覆盖"别的程序偷偷调 SetWindowPos 但没切前台"的情况）

检测方式是从目标窗口沿 Z 序往上找（`GetWindow(GW_HWNDPREV)`），看有没有可见且矩形相交的窗口。只有真被盖住才重新置顶。

**两个锁定窗口重叠时不会互抢**：如果遮住 A 的是同样被锁定的 B，就直接跳过——否则两个窗口会每秒互相把对方顶下去，界面持续闪烁。

**高亮是一次性闪光，不是常驻边框**：锁定瞬间弹出一个边框窗口，保持 0.55 秒后 0.35 秒淡出，然后立即回收。用 `SetWindowPos` 的 `hWndInsertAfter` 参数把它插到自己目标窗口的**正上方**，所以 A 被 B 盖住时闪光的框也会被 B 正常遮住，不会出现"蓝框飘在别的窗口上"。

## 项目结构

```
TopMostPin/
├─ App.xaml(.cs)              # 纯托盘入口：单实例、ShutdownMode、异常兜底
├─ app.manifest               # PerMonitorV2 DPI 感知（保证坐标是物理像素）
├─ Interop/NativeMethods.cs   # 所有 Win32 API 声明集中处
├─ Hotkey/
│  ├─ HotkeyDefinition.cs     # 快捷键模型 + "Ctrl+Alt+L" 字符串互转
│  └─ HotkeyManager.cs        # RegisterHotKey + 隐藏消息窗口收 WM_HOTKEY
├─ Services/
│  ├─ WindowPinner.cs         # 核心：取窗口、置顶、维持、高亮调度、权限判断
│  ├─ Messages.cs             # 所有提示文案（改文案只改这里）
│  ├─ AppSettings.cs          # %APPDATA%\TopMostPin\settings.json
│  └─ StartupManager.cs       # 开机自启（注册表）
├─ Tray/
│  ├─ TrayIconController.cs   # 托盘图标 + 右键菜单
│  └─ IconFactory.cs          # 运行时用 GDI+ 画图标（含数量角标），不打包 .ico
└─ UI/
   ├─ HighlightWindow.xaml    # 锁定闪光边框（穿透点击、不抢焦点、不进 Alt+Tab）
   ├─ ToastWindow.xaml        # 轻量提示浮窗（鼠标附近弹出，2.5s 自动淡出）
   └─ HotkeySettingsWindow.xaml # 修改快捷键对话框
```

## 打包成单文件 exe

### 框架依赖版（推荐，体积 194 KB）

```bash
dotnet publish -c Release -r win-x64 --self-contained false \
  -p:PublishSingleFile=true -p:DebugType=None -p:DebugSymbols=false -o publish
```

体积小，但目标机器必须装 .NET 8 Desktop Runtime。

### 自包含版（约 60 MB）

```bash
dotnet publish -c Release -r win-x64 --self-contained true \
  -p:PublishSingleFile=true -p:EnableCompressionInSingleFile=true \
  -p:DebugType=None -p:DebugSymbols=false -o publish-self
```

零依赖，拷到任何 Windows 机器都能跑，代价是体积。

> 说明：WPF 不支持 IL Trimming（`PublishTrimmed` 会破坏 XAML 和反射），NativeAOT 也不支持 WPF，
> 所以自包含版压不到 20 MB 以内。想要小体积只能用框架依赖版。

## 配置与日志

| 路径 | 内容 |
| --- | --- |
| `%APPDATA%\TopMostPin\settings.json` | 当前快捷键 |
| `%APPDATA%\TopMostPin\error.log` | 未处理异常（仅出错时生成，不会弹窗） |

删掉 `settings.json` 即可恢复默认快捷键 `Ctrl+Alt+L`。

## 已知限制

- **管理员权限窗口**：从普通权限进程无法置顶（UIPI 限制）。请右键 exe → 以管理员身份运行
- **独占全屏应用**：DirectX 独占全屏会绕过 DWM 合成，此时置顶在视觉上无效，工具会提示而不是硬顶
- **系统窗口**：桌面（Progman/WorkerW）、任务栏等不允许置顶
- **平台**：仅 Windows 10 / 11，发布目标为 x64
- **热键冲突**：如果 `Ctrl+Alt+L` 被别的程序占用，托盘菜单对应项会显示"（未注册）"，点进去换一个即可

## 许可证

本项目采用 [Apache License 2.0](LICENSE)。

```
Copyright 2026 Alan

Licensed under the Apache License, Version 2.0 (the "License");
you may not use this file except in compliance with the License.
You may obtain a copy of the License at

    http://www.apache.org/licenses/LICENSE-2.0

Unless required by applicable law or agreed to in writing, software
distributed under the License is distributed on an "AS IS" BASIS,
WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
See the License for the specific language governing permissions and
limitations under the License.
```

简单说：可自由使用、修改、分发、闭源商用，**只需保留版权声明和许可证副本**；同时包含明确的专利授权条款，比 MIT 更适合担心专利风险的项目。
