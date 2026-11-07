using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using TopMostPin.Interop;

namespace TopMostPin.UI;

/// <summary>
/// 轻量提示浮窗（自绘，不走系统托盘气泡）。
///
/// 为什么不用 NotifyIcon.ShowBalloonTip：
///   1) Win10/11 的气泡会经过 Shell 通知管道，首次弹出常有 0.5~2 秒延迟；
///   2) 用户关掉系统通知后，气泡直接不显示 —— 对"必须告知用户"的失败原因不可接受。
/// 自绘浮窗是即时的（同一帧出现），且不依赖任何系统设置。
///
/// 全程序共用一个实例：
///   - 出现在鼠标附近，并自动避开屏幕边缘；
///   - 不抢焦点、鼠标穿透、不进 Alt+Tab；
///   - 显示 2.5 秒（警告 3.5 秒）后淡出隐藏。
/// </summary>
public partial class ToastWindow : Window
{
    /// <summary>普通提示停留时长（毫秒）</summary>
    private const int InfoDurationMs = 2500;

    /// <summary>警告提示停留时长（毫秒），比普通提示多停留一会儿</summary>
    private const int WarningDurationMs = 3500;

    /// <summary>淡出时长（毫秒）</summary>
    private const int FadeOutMs = 240;

    /// <summary>鼠标与浮窗之间的间距（物理像素）</summary>
    private const int CursorOffsetX = 18;
    private const int CursorOffsetY = 24;

    private static readonly Brush InfoBrush = new SolidColorBrush(Color.FromRgb(0x29, 0xB6, 0xF6));
    private static readonly Brush WarningBrush = new SolidColorBrush(Color.FromRgb(0xFF, 0x8A, 0x3D));

    /// <summary>全局唯一实例，避免连续提示时堆出一堆窗口</summary>
    private static ToastWindow? _instance;

    private IntPtr _handle;
    private DispatcherTimer? _hideTimer;

    public ToastWindow()
    {
        InitializeComponent();
        InfoBrush.Freeze();
        WarningBrush.Freeze();
    }

    /// <summary>显示一条提示。可以在 UI 线程的任何地方直接调用。</summary>
    public static void Show(string message, bool isWarning)
    {
        _instance ??= new ToastWindow();
        _instance.ShowMessage(message, isWarning);
    }

    /// <summary>
    /// 预热：提前创建窗口和渲染栈。
    /// WPF 首次 Show 需要建 HWND + 初始化渲染，约 60~100ms，
    /// 放在第一次提示里会被感知成"卡一下"；挪到启动阶段做，用户完全看不到。
    /// 预热全程 Opacity=0 且在屏幕外，不会闪现。
    /// </summary>
    public static void WarmUp()
    {
        if (_instance != null) return;

        _instance = new ToastWindow();

        if (_instance._handle == IntPtr.Zero)
        {
            _instance.Show(); // Opacity 仍为 0，位置在 -20000，肉眼不可见
        }

        _instance.Conceal();
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);

        _handle = new WindowInteropHelper(this).Handle;

        // 鼠标穿透 + 不抢焦点 + 不进 Alt+Tab
        int exStyle = NativeMethods.GetWindowLong(_handle, NativeMethods.GWL_EXSTYLE);
        exStyle |= NativeMethods.WS_EX_TRANSPARENT | NativeMethods.WS_EX_NOACTIVATE | NativeMethods.WS_EX_TOOLWINDOW;
        NativeMethods.SetWindowLong(_handle, NativeMethods.GWL_EXSTYLE, exStyle);
    }

    private void ShowMessage(string message, bool isWarning)
    {
        MessageText.Text = message;
        AccentBar.Background = isWarning ? WarningBrush : InfoBrush;

        // 打断上一次的淡出动画，保证这次的显示不受影响
        BeginAnimation(OpacityProperty, null);

        if (_handle == IntPtr.Zero || !IsVisible)
        {
            Show(); // 此刻 Opacity=0，不可见
        }

        // SizeToContent 需要先完成一次测量，才能拿到真实尺寸去贴近鼠标
        Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() =>
        {
            PlaceNearCursor();
            Opacity = 1;
        }));

        RestartHideTimer(isWarning ? WarningDurationMs : InfoDurationMs);
    }

    /// <summary>把浮窗摆到鼠标右下方，超出屏幕边界时自动翻到另一侧。</summary>
    private void PlaceNearCursor()
    {
        if (_handle == IntPtr.Zero) return;
        if (!NativeMethods.GetCursorPos(out var cursor)) return;
        if (!NativeMethods.GetWindowRect(_handle, out var rect)) return;

        int width = rect.Width;
        int height = rect.Height;
        if (width <= 0 || height <= 0) return;

        // 用鼠标所在显示器的工作区（避开任务栏）做边界约束
        var workArea = GetWorkArea(cursor);

        int x = cursor.X + CursorOffsetX;
        int y = cursor.Y + CursorOffsetY;

        if (x + width > workArea.Right - 8) x = cursor.X - width - CursorOffsetX;
        if (y + height > workArea.Bottom - 8) y = cursor.Y - height - CursorOffsetY;

        // 极端情况下仍然越界，就硬夹到工作区内
        x = Math.Max(workArea.Left + 8, Math.Min(x, workArea.Right - width - 8));
        y = Math.Max(workArea.Top + 8, Math.Min(y, workArea.Bottom - height - 8));

        NativeMethods.SetWindowPos(_handle, NativeMethods.HWND_TOPMOST, x, y, 0, 0,
            NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOACTIVATE | NativeMethods.SWP_SHOWWINDOW);
    }

    private static NativeMethods.RECT GetWorkArea(NativeMethods.POINT point)
    {
        IntPtr monitor = NativeMethods.MonitorFromPoint(point, NativeMethods.MONITOR_DEFAULTTONEAREST);
        if (monitor != IntPtr.Zero)
        {
            var info = new NativeMethods.MONITORINFO { cbSize = System.Runtime.InteropServices.Marshal.SizeOf<NativeMethods.MONITORINFO>() };
            if (NativeMethods.GetMonitorInfo(monitor, ref info)) return info.rcWork;
        }

        // 拿不到就退回主屏尺寸
        return new NativeMethods.RECT
        {
            Left = 0,
            Top = 0,
            Right = (int)SystemParameters.PrimaryScreenWidth,
            Bottom = (int)SystemParameters.PrimaryScreenHeight,
        };
    }

    private void RestartHideTimer(int durationMs)
    {
        if (_hideTimer == null)
        {
            _hideTimer = new DispatcherTimer();
            _hideTimer.Tick += OnHideTimerTick;
        }

        _hideTimer.Stop();
        _hideTimer.Interval = TimeSpan.FromMilliseconds(durationMs);
        _hideTimer.Start();
    }

    private void OnHideTimerTick(object? sender, EventArgs e)
    {
        if (_hideTimer != null) _hideTimer.Stop();

        var fade = new DoubleAnimation
        {
            From = 1,
            To = 0,
            Duration = new Duration(TimeSpan.FromMilliseconds(FadeOutMs)),
            FillBehavior = FillBehavior.HoldEnd,
        };

        fade.Completed += (_, _) => Conceal();
        BeginAnimation(OpacityProperty, fade);
    }

    /// <summary>立即隐藏（程序退出时用）。</summary>
    public static void HideAll()
    {
        _instance?.Conceal();
    }

    private void Conceal()
    {
        BeginAnimation(OpacityProperty, null); // 动画会覆盖 Opacity，必须先解除
        Opacity = 0;

        if (IsVisible)
        {
            Hide();
        }
    }
}
