using System.Runtime.InteropServices;
using System.Windows.Interop;
using TopMostPin.Interop;
using TopMostPin.Services;

namespace TopMostPin.Hotkey;

/// <summary>
/// 全局快捷键管理：用 RegisterHotKey 注册，用隐藏消息窗口接收 WM_HOTKEY。
/// 目前只有一个快捷键（锁定置顶），按下即触发。
/// </summary>
public sealed class HotkeyManager : IDisposable
{
    private const int HotkeyId = 0x5450;

    /// <summary>接收 WM_HOTKEY 的隐藏窗口</summary>
    private readonly HwndSource _messageSink;

    private bool _disposed;

    /// <summary>当前已注册的快捷键，未注册成功时为 null</summary>
    public HotkeyDefinition? Current { get; private set; }

    /// <summary>快捷键被按下</summary>
    public event Action? Pressed;

    public HotkeyManager()
    {
        // 创建一个隐藏窗口，仅用来接收 WM_HOTKEY。
        // 用它而不是复用 WPF Window，是为了让快捷键和 UI 生命周期解耦。
        var parameters = new HwndSourceParameters("TopMostPin.HotkeyMessageSink")
        {
            Width = 0,
            Height = 0,
        };

        _messageSink = new HwndSource(parameters);

        // 重要：HwndSource 默认会按"普通窗口"创建，0x0 尺寸时在桌面上就是
        // 一条白色细长小窗（带 □ 和 ✕），非常影响观感。
        // 这里彻底去掉标题栏样式并隐藏，此后它只存在于消息循环里。
        HideSinkWindow(_messageSink.Handle);

        _messageSink.AddHook(WndProc);
    }

    /// <summary>把消息窗口彻底隐藏：去掉可视样式 + 移出 Alt+Tab + ShowWindow(SW_HIDE)。</summary>
    private static void HideSinkWindow(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero) return;

        int style = NativeMethods.GetWindowLong(hwnd, NativeMethods.GWL_STYLE);
        style &= ~(NativeMethods.WS_VISIBLE | NativeMethods.WS_CAPTION | NativeMethods.WS_THICKFRAME
                   | NativeMethods.WS_SYSMENU | NativeMethods.WS_MAXIMIZEBOX | NativeMethods.WS_MINIMIZEBOX);
        NativeMethods.SetWindowLong(hwnd, NativeMethods.GWL_STYLE, style);

        int exStyle = NativeMethods.GetWindowLong(hwnd, NativeMethods.GWL_EXSTYLE);
        NativeMethods.SetWindowLong(hwnd, NativeMethods.GWL_EXSTYLE, exStyle | NativeMethods.WS_EX_TOOLWINDOW);

        // 双保险：即使样式没生效，也强制隐藏（隐藏窗口照样能收 WM_HOTKEY）
        NativeMethods.ShowWindow(hwnd, NativeMethods.SW_HIDE);
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == NativeMethods.WM_HOTKEY && wParam.ToInt32() == HotkeyId)
        {
            Pressed?.Invoke();
            handled = true;
        }

        return IntPtr.Zero;
    }

    /// <summary>
    /// 注册（或更换）快捷键。失败时会自动回滚到原来可用的组合。
    /// </summary>
    /// <param name="definition">要注册的组合</param>
    /// <param name="error">失败原因，成功时为空</param>
    public bool TryRegister(HotkeyDefinition definition, out string error)
    {
        error = string.Empty;
        HotkeyDefinition? previous = Current;

        // 先注销旧的，否则重复注册同一组合会被系统拒绝
        if (previous != null)
        {
            NativeMethods.UnregisterHotKey(_messageSink.Handle, HotkeyId);
            Current = null;
        }

        // MOD_NOREPEAT：按住不放时系统只发一次 WM_HOTKEY，不会连发
        bool ok = NativeMethods.RegisterHotKey(_messageSink.Handle, HotkeyId,
            definition.Modifiers | NativeMethods.MOD_NOREPEAT, definition.VirtualKey);

        if (ok)
        {
            Current = definition;
            return true;
        }

        int lastError = Marshal.GetLastWin32Error();

        // 统一文案：对外只说"冲突，请去托盘菜单更换"，具体错误码留给调试输出
        error = Messages.HotkeyConflict;
        System.Diagnostics.Debug.WriteLine(
            $"[TopMostPin] RegisterHotKey({definition}) 失败，Win32 错误码 {lastError}");

        // 回滚：尽量恢复到原来的快捷键，避免用户改失败后彻底没有热键
        if (previous != null &&
            NativeMethods.RegisterHotKey(_messageSink.Handle, HotkeyId,
                previous.Modifiers | NativeMethods.MOD_NOREPEAT, previous.VirtualKey))
        {
            Current = previous;
        }

        return false;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        NativeMethods.UnregisterHotKey(_messageSink.Handle, HotkeyId);
        Current = null;

        _messageSink.RemoveHook(WndProc);
        _messageSink.Dispose();
    }
}
