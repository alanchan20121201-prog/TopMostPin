using System.Runtime.InteropServices;
using System.Text;

namespace TopMostPin.Interop;

/// <summary>
/// 所有 Win32 API 声明集中在这里，方便后续排查/替换。
/// 注释里标注了每个 API 的作用，改动时注意结构体的字段顺序不能变。
/// </summary>
internal static class NativeMethods
{
    // ================= 消息 =================
    /// <summary>注册的全局热键被按下时，系统会向注册窗口发送这条消息（只有"按下"，没有"松开"）。</summary>
    public const int WM_HOTKEY = 0x0312;

    // ================= 热键修饰键 =================
    public const uint MOD_ALT = 0x0001;
    public const uint MOD_CONTROL = 0x0002;
    public const uint MOD_SHIFT = 0x0004;
    public const uint MOD_WIN = 0x0008;

    /// <summary>按住不放时不重复触发 WM_HOTKEY（避免长按时反复弹提示）</summary>
    public const uint MOD_NOREPEAT = 0x4000;

    // ================= SetWindowPos 参数 =================
    public const uint SWP_NOSIZE = 0x0001;
    public const uint SWP_NOMOVE = 0x0002;
    public const uint SWP_NOACTIVATE = 0x0010;
    public const uint SWP_SHOWWINDOW = 0x0040;
    public const uint SWP_NOOWNERZORDER = 0x0200;

    /// <summary>置顶插入位置：HWND_TOPMOST = -1</summary>
    public static readonly IntPtr HWND_TOPMOST = new(-1);
    /// <summary>取消置顶插入位置：HWND_NOTOPMOST = -2</summary>
    public static readonly IntPtr HWND_NOTOPMOST = new(-2);

    // ================= GetAncestor =================
    /// <summary>GA_ROOT = 2：取根（顶层）父窗口</summary>
    public const uint GA_ROOT = 2;

    // ================= 窗口样式 =================
    public const int GWL_STYLE = -16;
    public const int GWL_EXSTYLE = -20;
    public const int WS_VISIBLE = 0x10000000;
    /// <summary>弹出式窗口样式：没有标题栏、没有最小尺寸限制（0x0 就是真的 0x0）</summary>
    public const int WS_POPUP = unchecked((int)0x80000000);
    public const int WS_CAPTION = 0x00C00000;          // 标题栏（含 ✕）
    public const int WS_SYSMENU = 0x00080000;          // 系统菜单（✕ 按钮的来源）
    public const int WS_THICKFRAME = 0x00040000;       // 可拖拽边框
    public const int WS_MINIMIZEBOX = 0x00020000;
    public const int WS_MAXIMIZEBOX = 0x00010000;
    public const int WS_MAXIMIZE = 0x01000000;         // 窗口处于最大化状态
    public const int WS_EX_TRANSPARENT = 0x00000020; // 鼠标穿透（WindowFromPoint 会跳过它）
    public const int WS_EX_TOOLWINDOW = 0x00000080;  // 不出现在 Alt+Tab
    public const int WS_EX_TOPMOST = 0x00000008;     // 窗口当前是置顶状态
    public const int WS_EX_NOACTIVATE = 0x08000000;  // 点击/显示不抢焦点

    /// <summary>ShowWindow 命令：隐藏窗口</summary>
    public const int SW_HIDE = 0;

    // ================= 窗口事件钩子（SetWinEventHook）=================
    /// <summary>前台窗口发生变化（层级被别的程序改变时会触发）</summary>
    public const uint EVENT_SYSTEM_FOREGROUND = 0x0003;
    /// <summary>窗口从最小化恢复</summary>
    public const uint EVENT_SYSTEM_MINIMIZEEND = 0x0017;
    /// <summary>窗口（或子对象）被销毁，用于清理锁定列表</summary>
    public const uint EVENT_OBJECT_DESTROY = 0x8001;

    public const uint WINEVENT_OUTOFCONTEXT = 0x0000;
    public const uint WINEVENT_SKIPOWNPROCESS = 0x0002;

    // ================= 显示器 =================
    public const uint MONITOR_DEFAULTTONEAREST = 0x00000002;

    // ================= 虚拟键码 =================
    public const int VK_SHIFT = 0x10;
    public const int VK_CONTROL = 0x11;
    public const int VK_MENU = 0x12;   // Alt
    public const int VK_LWIN = 0x5B;
    public const int VK_RWIN = 0x5C;

    // ================= 进程 / 令牌 =================
    public const uint PROCESS_QUERY_LIMITED_INFORMATION = 0x1000;
    public const uint TOKEN_QUERY = 0x0008;
    public const int TokenElevation = 20;     // TOKEN_INFORMATION_CLASS.TokenElevation
    public const int ERROR_ACCESS_DENIED = 5;
    public const int ERROR_HOTKEY_ALREADY_REGISTERED = 1409;

    // ================= 结构体 =================
    [StructLayout(LayoutKind.Sequential)]
    public struct POINT
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;

        public readonly int Width => Right - Left;
        public readonly int Height => Bottom - Top;
    }

    /// <summary>显示器信息，用于判断"全屏应用"。</summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct MONITORINFO
    {
        public int cbSize;
        public RECT rcMonitor; // 显示器完整区域（物理像素）
        public RECT rcWork;    // 去掉任务栏后的工作区
        public uint dwFlags;
    }

    /// <summary>WinEvent 回调委托。必须保存成字段，否则会被 GC 回收导致回调崩溃。</summary>
    public delegate void WinEventDelegate(IntPtr hWinEventHook, uint eventType, IntPtr hwnd,
        int idObject, int idChild, uint idEventThread, uint ms);

    // ================= user32.dll =================

    /// <summary>注册全局热键。<paramref name="hWnd"/> 会收到 WM_HOTKEY。</summary>
    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    /// <summary>取屏幕坐标点下方最内层的子窗口（可能是某个控件的子窗口，需要再 GetAncestor 提升到顶层）。</summary>
    [DllImport("user32.dll")]
    public static extern IntPtr WindowFromPoint(POINT point);

    /// <summary>取指定窗口的祖先窗口，GA_ROOT 表示顶层窗口。</summary>
    [DllImport("user32.dll")]
    public static extern IntPtr GetAncestor(IntPtr hWnd, uint gaFlags);

    /// <summary>改变窗口的 Z 序，用于置顶/取消置顶。</summary>
    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint flags);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool GetWindowRect(IntPtr hWnd, out RECT rect);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool GetCursorPos(out POINT point);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool IsWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool IsWindowVisible(IntPtr hWnd);

    [DllImport("user32.dll")]
    public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

    /// <summary>读取按键当前状态，最高位为 1 表示按键正被按住。用于检测热键"松开"。</summary>
    [DllImport("user32.dll")]
    public static extern short GetAsyncKeyState(int vKey);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    public static extern int GetClassName(IntPtr hWnd, StringBuilder className, int maxCount);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongW")]
    public static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongW")]
    public static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

    /// <summary>显示/隐藏窗口。SW_HIDE 用于彻底隐藏消息窗口。</summary>
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool DestroyIcon(IntPtr hIcon);

    // ---- 窗口状态 / 全屏判断 ----

    /// <summary>GetWindow 命令：取 Z 序中位于当前窗口上方的窗口</summary>
    public const uint GW_HWNDPREV = 3;

    [DllImport("user32.dll")]
    public static extern IntPtr GetWindow(IntPtr hWnd, uint uCmd);

    /// <summary>窗口是否处于最小化状态。</summary>
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool IsIconic(IntPtr hWnd);

    [DllImport("user32.dll")]
    public static extern IntPtr MonitorFromWindow(IntPtr hWnd, uint dwFlags);

    /// <summary>取指定屏幕坐标点所在的显示器（用于浮窗定位）。</summary>
    [DllImport("user32.dll")]
    public static extern IntPtr MonitorFromPoint(POINT pt, uint dwFlags);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, EntryPoint = "GetMonitorInfoW")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

    // ---- 全局窗口事件钩子 ----

    /// <summary>注册窗口事件钩子，用于监听目标窗口被销毁 / 恢复 / 层级变化。</summary>
    [DllImport("user32.dll", SetLastError = true)]
    public static extern IntPtr SetWinEventHook(uint eventMin, uint eventMax, IntPtr hmodWinEventProc,
        WinEventDelegate lpfnWinEventProc, uint idProcess, uint idThread, uint dwFlags);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool UnhookWinEvent(IntPtr hWinEventHook);

    // ================= kernel32.dll =================

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern IntPtr OpenProcess(uint desiredAccess, [MarshalAs(UnmanagedType.Bool)] bool inheritHandle, uint processId);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool CloseHandle(IntPtr handle);

    // ================= advapi32.dll =================

    [DllImport("advapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool OpenProcessToken(IntPtr processHandle, uint desiredAccess, out IntPtr tokenHandle);

    [DllImport("advapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool GetTokenInformation(IntPtr tokenHandle, int tokenInformationClass,
        out uint tokenInformation, uint tokenInformationLength, out uint returnLength);
}
