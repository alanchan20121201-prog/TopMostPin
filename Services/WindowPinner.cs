using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Threading;
using TopMostPin.Interop;
using TopMostPin.UI;

namespace TopMostPin.Services;

/// <summary>
/// 核心逻辑：锁定置顶。
///
/// 【锁定置顶】按一下快捷键（默认 Ctrl+Alt+L）
///   按下 -> ToggleLockUnderCursor()：不在列表里就置顶并加入，已在列表里就移出
///   锁定期间由 SetWinEventHook 监听：窗口被销毁 / 从最小化恢复 / 层级被别的程序改变，
///   都会自动重新置顶，但不拦截用户对窗口本身的最小化、拖动、缩放。
///
/// 【置顶维持】两种触发方式，都是"先检测再动作"，不盲目重排 Z 序：
///   1) 事件驱动：前台窗口变化 / 从最小化恢复时立即检查一次；
///   2) 低频心跳：每 1 秒检查一次（覆盖"别的程序偷偷调用 SetWindowPos 但没切前台"的情况）。
///   检查方式是从目标窗口沿 Z 序往上找，看有没有可见且与它矩形相交的窗口。
///   只有真的被盖住才重新 SetWindowPos(HWND_TOPMOST, SWP_NOACTIVATE)；
///   如果盖住它的是独占全屏应用，则不再硬顶，改为提示用户"暂时无法保持置顶"。
///
/// 【高亮反馈】锁定成功时闪一下蓝色边框（保持 0.55s + 淡出 0.35s），随后自动回收，
/// 不在窗口上留常驻边框 —— 避免长期遮挡窗口边缘，也避免多窗口时一堆框打架。
/// </summary>
public sealed class WindowPinner : IDisposable
{
    /// <summary>置顶维持心跳间隔（毫秒）。低频，只在有锁定窗口时运行。</summary>
    private const int HeartbeatIntervalMs = 1000;

    /// <summary>同一个全屏窗口的"无法保持置顶"提示最小间隔（毫秒），避免每秒刷屏</summary>
    private const int FullscreenNoticeCooldownMs = 15000;

    /// <summary>高亮边框向外扩展的像素数</summary>
    private const int OverlayPadding = 3;

    /// <summary>高亮闪光的保持时长（毫秒）</summary>
    private const int HighlightHoldMs = 550;

    /// <summary>高亮闪光的淡出时长（毫秒）</summary>
    private const int HighlightFadeMs = 350;

    // 系统桌面 / 任务栏等保护窗口，这些窗口不允许置顶
    private static readonly string[] SkipWindowClasses =
    {
        "Progman", "WorkerW", "Shell_TrayWnd", "Shell_SecondaryTrayWnd",
        "Windows.UI.Core.CoreWindow", "SysShadow", "TaskListThumbnailWnd"
    };

    /// <summary>本进程是否以管理员身份运行（只计算一次）</summary>
    private static readonly Lazy<bool> SelfElevated = new(() => IsProcessElevated((uint)Environment.ProcessId));

    // ==================== 锁定置顶状态 ====================
    /// <summary>按锁定顺序保存的句柄列表（用于"全部解锁"和数量统计）</summary>
    private readonly List<IntPtr> _lockedWindows = new();

    /// <summary>同一批句柄的哈希集合，用于 O(1) 判断是否已锁定</summary>
    private readonly HashSet<IntPtr> _lockedSet = new();

    /// <summary>WinEvent 回调委托，必须保持强引用，否则 GC 回收后会崩溃</summary>
    private readonly NativeMethods.WinEventDelegate _winEventCallback;

    /// <summary>已注册的钩子句柄，退出时释放</summary>
    private readonly List<IntPtr> _winEventHooks = new();

    /// <summary>置顶维持心跳，有锁定窗口时才启动</summary>
    private DispatcherTimer? _heartbeatTimer;

    /// <summary>
    /// 每个正在闪光的窗口对应一个高亮边框窗口（窗口句柄 -> 边框）。
    /// 闪光是一次性的：淡出结束后立即回收，不会常驻贴在窗口上。
    /// </summary>
    private readonly Dictionary<IntPtr, HighlightWindow> _overlays = new();

    // 全屏覆盖提示的节流状态
    private IntPtr _lastFullscreenNoticeWindow = IntPtr.Zero;
    private DateTime _lastFullscreenNoticeUtc = DateTime.MinValue;

    /// <summary>需要提示用户时触发（第二个参数为 true 表示警告/失败）</summary>
    public event Action<string, bool>? Notify;

    /// <summary>锁定数量变化（托盘角标据此刷新）</summary>
    public event Action? LockedCountChanged;

    /// <summary>当前锁定窗口数量</summary>
    public int LockedCount => _lockedWindows.Count;

    public WindowPinner()
    {
        _winEventCallback = OnWinEvent;
        InstallWindowEventHooks();
    }

    // ==================== 锁定置顶 ====================

    /// <summary>快捷键按下时调用：切换鼠标下窗口的锁定状态。</summary>
    public void ToggleLockUnderCursor()
    {
        // 鼠标下就是已经锁定的窗口 -> 直接解锁（不必再做合法性校验）
        IntPtr underCursor = GetTopWindowUnderCursor();
        if (underCursor != IntPtr.Zero && _lockedSet.Contains(underCursor))
        {
            RemoveLocked(underCursor, keepTopmost: false);
            Notify?.Invoke(Messages.Unlocked, false);
            return;
        }

        if (!TryResolveTarget(out IntPtr target, out string error))
        {
            Notify?.Invoke(error, true);
            return;
        }

        if (!ApplyTopmost(target, out error))
        {
            Notify?.Invoke(error, true);
            return;
        }

        AddLocked(target);
        Notify?.Invoke(string.Format(Messages.Locked, _lockedWindows.Count), false);
    }

    /// <summary>取消全部锁定（托盘菜单"全部解锁"）。</summary>
    public void UnlockAll()
    {
        if (_lockedWindows.Count == 0)
        {
            Notify?.Invoke(Messages.NothingLocked, false);
            return;
        }

        foreach (IntPtr window in _lockedWindows.ToArray())
        {
            if (NativeMethods.IsWindow(window))
            {
                NativeMethods.SetWindowPos(window, NativeMethods.HWND_NOTOPMOST, 0, 0, 0, 0,
                    NativeMethods.SWP_NOMOVE | NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOACTIVATE);
            }
        }

        _lockedWindows.Clear();
        _lockedSet.Clear();
        ClearHighlights();
        StopHeartbeat();
        LockedCountChanged?.Invoke();
        Notify?.Invoke(Messages.UnlockedAll, false);
    }

    private void AddLocked(IntPtr window)
    {
        if (_lockedSet.Add(window))
        {
            _lockedWindows.Add(window);
            StartHeartbeat();      // 有锁定窗口才开始心跳，无锁定时不占任何资源
            FlashHighlight(window); // 闪一下边框，确认锁的是哪个窗口
            LockedCountChanged?.Invoke();
        }
    }

    /// <summary>从锁定列表移除。keepTopmost=true 时不动窗口的 Z 序（窗口已销毁的场景）。</summary>
    private void RemoveLocked(IntPtr window, bool keepTopmost)
    {
        if (!_lockedSet.Remove(window)) return;

        _lockedWindows.Remove(window);

        if (!keepTopmost && NativeMethods.IsWindow(window))
        {
            NativeMethods.SetWindowPos(window, NativeMethods.HWND_NOTOPMOST, 0, 0, 0, 0,
                NativeMethods.SWP_NOMOVE | NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOACTIVATE);
        }

        // 解锁就销毁对应边框（句柄可能被系统复用，不能留着复用）
        HideHighlight(window, destroy: true);

        if (_lockedWindows.Count == 0)
        {
            StopHeartbeat();
        }

        LockedCountChanged?.Invoke();
    }

    // ==================== 窗口事件监听（锁定期间保持置顶）====================

    private void InstallWindowEventHooks()
    {
        // 前台窗口变化：说明有别的程序把层级顶上来了 -> 检查一次
        AddHook(NativeMethods.SetWinEventHook(
            NativeMethods.EVENT_SYSTEM_FOREGROUND, NativeMethods.EVENT_SYSTEM_FOREGROUND,
            IntPtr.Zero, _winEventCallback, 0, 0, NativeMethods.WINEVENT_OUTOFCONTEXT));

        // 窗口从最小化恢复 -> 检查一次
        AddHook(NativeMethods.SetWinEventHook(
            NativeMethods.EVENT_SYSTEM_MINIMIZEEND, NativeMethods.EVENT_SYSTEM_MINIMIZEEND,
            IntPtr.Zero, _winEventCallback, 0, 0, NativeMethods.WINEVENT_OUTOFCONTEXT));

        // 窗口被销毁 -> 从锁定列表移除，避免句柄泄漏和误操作
        AddHook(NativeMethods.SetWinEventHook(
            NativeMethods.EVENT_OBJECT_DESTROY, NativeMethods.EVENT_OBJECT_DESTROY,
            IntPtr.Zero, _winEventCallback, 0, 0,
            NativeMethods.WINEVENT_OUTOFCONTEXT | NativeMethods.WINEVENT_SKIPOWNPROCESS));
    }

    private void AddHook(IntPtr hook)
    {
        if (hook != IntPtr.Zero) _winEventHooks.Add(hook);
    }

    /// <summary>
    /// 全局窗口事件回调。注意：这里只做极轻量的判断，避免影响系统整体流畅度。
    /// </summary>
    private void OnWinEvent(IntPtr hWinEventHook, uint eventType, IntPtr hwnd,
        int idObject, int idChild, uint idEventThread, uint ms)
    {
        if (_lockedWindows.Count == 0 || hwnd == IntPtr.Zero) return;

        if (eventType == NativeMethods.EVENT_OBJECT_DESTROY)
        {
            // idObject != 0 表示是窗口内部的子对象（按钮、菜单等）被销毁，忽略
            if (idObject != 0) return;
            if (_lockedSet.Contains(hwnd)) RemoveLocked(hwnd, keepTopmost: true);
            return;
        }

        // 前台切换 / 从最小化恢复：检查一次是否被盖住
        MaintainTopmost();
    }

    /// <summary>
    /// 置顶维持：逐个检查锁定窗口，只有当它被别的可见窗口盖住时才重新提到最前。
    ///
    /// 为什么不无脑重排 Z 序：
    ///   1) 无意义的 SetWindowPos 会造成窗口闪烁、打断用户正在做的操作；
    ///   2) 被独占全屏应用覆盖时，强行置顶也没有视觉效果（DWM 合成被绕过），
    ///      还会让窗口在后面疯狂抢 Z 序，所以这种情况只提示，不硬顶。
    /// </summary>
    private void MaintainTopmost()
    {
        foreach (IntPtr window in _lockedWindows.ToArray())
        {
            if (!NativeMethods.IsWindow(window))
            {
                RemoveLocked(window, keepTopmost: true);
                continue;
            }

            // 最小化中的窗口不处理，否则用户按最小化会被强行拉回来；
            // 若此刻正好在闪光，顺手收掉，免得边框停在旧位置
            if (NativeMethods.IsIconic(window))
            {
                HideHighlight(window, destroy: true);
                continue;
            }

            IntPtr coverer = FindTopmostCoveringWindow(window);

            // coverer 为 null：没被盖住，什么都不做（不打断用户操作）
            // coverer 是本程序自己的窗口：不可能（已过滤）
            if (coverer != IntPtr.Zero && !_lockedSet.Contains(coverer))
            {
                if (IsFullscreenWindow(coverer))
                {
                    // 独占全屏：系统层面压不住，只提示
                    NotifyFullscreenOverlap(coverer);
                }
                else
                {
                    // 重新提回最前。SWP_NOACTIVATE 保证不抢焦点。
                    NativeMethods.SetWindowPos(window, NativeMethods.HWND_TOPMOST, 0, 0, 0, 0,
                        NativeMethods.SWP_NOMOVE | NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOACTIVATE);
                }
            }

            // 若 coverer 是另一个"已锁定"的窗口：什么都不做。
            // 这是两个锁定窗口重叠时的关键：否则 A 顶掉 B、B 又顶掉 A，
            // 心跳每秒一次会变成持续的层级乱跳（闪烁）。同组窗口之间保持用户当前的相对顺序。
        }
    }

    /// <summary>
    /// 从目标窗口沿 Z 序（GW_HWNDPREV 方向）往上走，返回"位置最高的那个遮挡窗口"。
    /// 之所以要最高的而不是第一个：判断要不要重新置顶时，需要知道挡在最上面的是谁 ——
    /// 如果最上面那个是本程序锁定的另一个窗口，就不该抢（否则两个窗口会互顶）。
    /// 返回 IntPtr.Zero 表示没有被任何窗口遮挡。
    /// </summary>
    private static IntPtr FindTopmostCoveringWindow(IntPtr window)
    {
        if (!NativeMethods.GetWindowRect(window, out var targetRect)) return IntPtr.Zero;
        if (targetRect.Width <= 0 || targetRect.Height <= 0) return IntPtr.Zero;

        IntPtr coverer = IntPtr.Zero;
        IntPtr candidate = NativeMethods.GetWindow(window, NativeMethods.GW_HWNDPREV);

        while (candidate != IntPtr.Zero)
        {
            if (IsCoveringCandidate(candidate, targetRect)) coverer = candidate;

            candidate = NativeMethods.GetWindow(candidate, NativeMethods.GW_HWNDPREV);
        }

        return coverer;
    }

    private static bool IsCoveringCandidate(IntPtr candidate, NativeMethods.RECT targetRect)
    {
        if (!NativeMethods.IsWindowVisible(candidate)) return false;

        // 本程序自己的窗口（设置对话框等）不算遮挡物
        NativeMethods.GetWindowThreadProcessId(candidate, out uint processId);
        if (processId == (uint)Environment.ProcessId) return false;

        if (!NativeMethods.GetWindowRect(candidate, out var candidateRect)) return false;

        // 矩形相交即视为遮挡（部分遮挡也算）
        return targetRect.Left < candidateRect.Right
               && candidateRect.Left < targetRect.Right
               && targetRect.Top < candidateRect.Bottom
               && candidateRect.Top < targetRect.Bottom;
    }

    /// <summary>同一个全屏窗口的提示做节流，避免心跳每秒弹一次气泡。</summary>
    private void NotifyFullscreenOverlap(IntPtr fullscreenWindow)
    {
        DateTime now = DateTime.UtcNow;

        if (fullscreenWindow == _lastFullscreenNoticeWindow &&
            (now - _lastFullscreenNoticeUtc).TotalMilliseconds < FullscreenNoticeCooldownMs)
        {
            return;
        }

        _lastFullscreenNoticeWindow = fullscreenWindow;
        _lastFullscreenNoticeUtc = now;

        Notify?.Invoke(Messages.FullscreenCoversLocked, true);
    }

    // ==================== 置顶维持心跳 ====================

    private void StartHeartbeat()
    {
        _heartbeatTimer ??= new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromMilliseconds(HeartbeatIntervalMs)
        };

        if (_heartbeatTimer.IsEnabled) return;

        _heartbeatTimer.Tick -= OnHeartbeatTick;
        _heartbeatTimer.Tick += OnHeartbeatTick;
        _heartbeatTimer.Start();
    }

    private void StopHeartbeat()
    {
        if (_heartbeatTimer is { IsEnabled: true })
        {
            _heartbeatTimer.Stop();
        }
    }

    private void OnHeartbeatTick(object? sender, EventArgs e)
    {
        if (_lockedWindows.Count == 0)
        {
            StopHeartbeat();
            return;
        }

        MaintainTopmost();
    }

    // ==================== 高亮闪光 ====================

    /// <summary>
    /// 在目标窗口上闪一下高亮边框，作为"锁的是这个窗口"的确认反馈。
    /// 保持 0.55 秒后淡出 0.35 秒，然后自动隐藏并回收，不在窗口上留下常驻边框。
    /// </summary>
    private void FlashHighlight(IntPtr target)
    {
        if (!NativeMethods.GetWindowRect(target, out var rect)) return;

        // 同一窗口上一次闪光还没结束就先收掉，避免同时出现两个框
        HideHighlight(target, destroy: true);

        var overlay = new HighlightWindow();
        _overlays[target] = overlay;

        overlay.FlashAbove(target,
            rect.Left - OverlayPadding,
            rect.Top - OverlayPadding,
            rect.Width + OverlayPadding * 2,
            rect.Height + OverlayPadding * 2,
            HighlightHoldMs,
            HighlightFadeMs,
            () =>
            {
                // 动画结束后回收，避免长期占用窗口句柄
                if (_overlays.TryGetValue(target, out HighlightWindow? current) && ReferenceEquals(current, overlay))
                {
                    _overlays.Remove(target);
                }

                overlay.CloseSafely();
            });
    }

    /// <summary>立即收掉某个窗口的闪光；destroy=true 时连窗口一起关掉。</summary>
    private void HideHighlight(IntPtr target, bool destroy)
    {
        if (!_overlays.TryGetValue(target, out HighlightWindow? overlay)) return;

        overlay.Conceal();

        if (!destroy) return;

        overlay.CloseSafely();
        _overlays.Remove(target);
    }

    /// <summary>关掉所有边框（全部解锁、程序退出时用）。</summary>
    private void ClearHighlights()
    {
        foreach (HighlightWindow overlay in _overlays.Values)
        {
            overlay.Conceal();
            overlay.CloseSafely();
        }

        _overlays.Clear();
    }

    // ==================== 目标窗口解析与校验 ====================

    /// <summary>取鼠标坐标对应的顶层窗口（不做任何合法性校验）。</summary>
    private static IntPtr GetTopWindowUnderCursor()
    {
        if (!NativeMethods.GetCursorPos(out var cursor)) return IntPtr.Zero;

        IntPtr hit = NativeMethods.WindowFromPoint(cursor);
        if (hit == IntPtr.Zero) return IntPtr.Zero;

        IntPtr top = NativeMethods.GetAncestor(hit, NativeMethods.GA_ROOT);
        return top == IntPtr.Zero ? hit : top;
    }

    /// <summary>
    /// 解析并校验鼠标下的目标窗口。失败时通过 <paramref name="error"/> 返回给用户看的文案。
    /// 校验顺序：有效性 -> 系统窗口 -> 全屏应用 -> 管理员权限。
    /// </summary>
    private static bool TryResolveTarget(out IntPtr window, out string error)
    {
        window = IntPtr.Zero;
        error = string.Empty;

        IntPtr candidate = GetTopWindowUnderCursor();
        if (candidate == IntPtr.Zero)
        {
            error = Messages.NoWindowUnderCursor;
            return false;
        }

        if (!NativeMethods.IsWindow(candidate) || !NativeMethods.IsWindowVisible(candidate))
        {
            error = Messages.SystemWindowUnsupported;
            return false;
        }

        // 尺寸为 0 的窗口（隐藏的宿主窗口等）
        if (!NativeMethods.GetWindowRect(candidate, out var rect) || rect.Width <= 1 || rect.Height <= 1)
        {
            error = Messages.SystemWindowUnsupported;
            return false;
        }

        // 本程序自己的窗口（含设置对话框）
        NativeMethods.GetWindowThreadProcessId(candidate, out uint processId);
        if (processId == (uint)Environment.ProcessId)
        {
            error = Messages.SystemWindowUnsupported;
            return false;
        }

        // 桌面、任务栏等系统保护窗口
        if (IsSystemWindowClass(candidate))
        {
            error = Messages.SystemWindowUnsupported;
            return false;
        }

        // 全屏应用（游戏、全屏视频）
        if (IsFullscreenWindow(candidate))
        {
            error = Messages.FullscreenUnsupported;
            return false;
        }

        // 管理员权限窗口：普通权限进程无法置顶它
        if (IsProcessElevated(processId) && !SelfElevated.Value)
        {
            error = Messages.ElevationRequired;
            return false;
        }

        window = candidate;
        return true;
    }

    private static bool IsSystemWindowClass(IntPtr window)
    {
        var className = new StringBuilder(256);
        NativeMethods.GetClassName(window, className, className.Capacity);
        string cls = className.ToString();

        foreach (string skip in SkipWindowClasses)
        {
            if (string.Equals(cls, skip, StringComparison.OrdinalIgnoreCase)) return true;
        }

        return false;
    }

    /// <summary>
    /// 判断是否为全屏应用：窗口矩形和它所在显示器的完整矩形完全重合。
    /// 最大化窗口会被明确排除（它只占工作区，不是全屏应用）。
    /// </summary>
    private static bool IsFullscreenWindow(IntPtr window)
    {
        int style = NativeMethods.GetWindowLong(window, NativeMethods.GWL_STYLE);
        if ((style & NativeMethods.WS_MAXIMIZE) != 0) return false;

        IntPtr monitor = NativeMethods.MonitorFromWindow(window, NativeMethods.MONITOR_DEFAULTTONEAREST);
        if (monitor == IntPtr.Zero) return false;

        var info = new NativeMethods.MONITORINFO { cbSize = Marshal.SizeOf<NativeMethods.MONITORINFO>() };
        if (!NativeMethods.GetMonitorInfo(monitor, ref info)) return false;
        if (!NativeMethods.GetWindowRect(window, out var rect)) return false;

        const int tolerance = 2; // 容忍 1~2 像素的边框误差
        return Math.Abs(rect.Left - info.rcMonitor.Left) <= tolerance
               && Math.Abs(rect.Top - info.rcMonitor.Top) <= tolerance
               && Math.Abs(rect.Right - info.rcMonitor.Right) <= tolerance
               && Math.Abs(rect.Bottom - info.rcMonitor.Bottom) <= tolerance;
    }

    /// <summary>置顶一个窗口，失败时把原因写进 error。</summary>
    private static bool ApplyTopmost(IntPtr window, out string error)
    {
        error = string.Empty;

        bool ok = NativeMethods.SetWindowPos(window, NativeMethods.HWND_TOPMOST, 0, 0, 0, 0,
            NativeMethods.SWP_NOMOVE | NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOACTIVATE);

        if (ok) return true;

        int lastError = Marshal.GetLastWin32Error();
        error = lastError == NativeMethods.ERROR_ACCESS_DENIED
            ? Messages.ElevationRequired
            : string.Format(Messages.PinFailed, lastError);

        return false;
    }

    // ==================== 权限判断 ====================

    /// <summary>
    /// 判断某个进程是否以管理员（提升）权限运行。
    /// 连 OpenProcess 都被拒绝时，说明对方权限比我们高，也视为需要管理员。
    /// </summary>
    private static bool IsProcessElevated(uint processId)
    {
        IntPtr processHandle = NativeMethods.OpenProcess(
            NativeMethods.PROCESS_QUERY_LIMITED_INFORMATION, false, processId);

        if (processHandle == IntPtr.Zero)
        {
            return Marshal.GetLastWin32Error() == NativeMethods.ERROR_ACCESS_DENIED;
        }

        try
        {
            if (!NativeMethods.OpenProcessToken(processHandle, NativeMethods.TOKEN_QUERY, out IntPtr tokenHandle))
            {
                return false;
            }

            try
            {
                // TOKEN_ELEVATION 结构体只有一个 DWORD
                if (!NativeMethods.GetTokenInformation(tokenHandle, NativeMethods.TokenElevation,
                        out uint elevation, sizeof(uint), out _))
                {
                    return false;
                }

                return elevation != 0;
            }
            finally
            {
                NativeMethods.CloseHandle(tokenHandle);
            }
        }
        finally
        {
            NativeMethods.CloseHandle(processHandle);
        }
    }

    public void Dispose()
    {
        UnlockAll(); // 退出时把锁定窗口恢复成非置顶
        ClearHighlights();
        StopHeartbeat();

        foreach (IntPtr hook in _winEventHooks)
        {
            NativeMethods.UnhookWinEvent(hook);
        }

        _winEventHooks.Clear();
    }
}
