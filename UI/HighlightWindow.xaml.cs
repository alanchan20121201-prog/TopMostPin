using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Animation;
using TopMostPin.Interop;

namespace TopMostPin.UI;

/// <summary>
/// 锁定反馈用的高亮边框（一次性闪光，不是常驻边框）。
///
/// 行为：出现 -> 保持约 0.5 秒 -> 淡出约 0.35 秒 -> 自动隐藏并回收。
///
/// 关键点：
///  1) WS_EX_TRANSPARENT   -> 鼠标穿透，WindowFromPoint 也不会命中它；
///  2) WS_EX_NOACTIVATE    -> 显示时不会抢走目标窗口的焦点；
///  3) WS_EX_TOOLWINDOW    -> 不出现在 Alt+Tab / 任务栏；
///  4) 用 SetWindowPos 的 hWndInsertAfter 参数把边框插到"目标窗口正上方"，
///     这样即使两个窗口重叠，闪光也不会跑到别的窗口上面去；
///  5) 位置直接用物理像素设置，绕开 WPF 的 DIP 换算，避免高分屏偏移。
/// </summary>
public partial class HighlightWindow : Window
{
    private IntPtr _handle;

    public HighlightWindow()
    {
        InitializeComponent();
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);

        _handle = new WindowInteropHelper(this).Handle;

        int exStyle = NativeMethods.GetWindowLong(_handle, NativeMethods.GWL_EXSTYLE);
        exStyle |= NativeMethods.WS_EX_TRANSPARENT | NativeMethods.WS_EX_NOACTIVATE | NativeMethods.WS_EX_TOOLWINDOW;
        NativeMethods.SetWindowLong(_handle, NativeMethods.GWL_EXSTYLE, exStyle);
    }

    /// <summary>
    /// 在目标窗口正上方闪一下边框，然后淡出。
    /// </summary>
    /// <param name="targetWindow">目标窗口句柄，边框会插到它正上方</param>
    /// <param name="holdMs">完全显示的保持时长（毫秒）</param>
    /// <param name="fadeMs">淡出时长（毫秒）</param>
    /// <param name="onFinished">淡出结束、窗口已隐藏后的回调（用于回收实例）</param>
    public void FlashAbove(IntPtr targetWindow, int x, int y, int width, int height,
        int holdMs, int fadeMs, Action? onFinished)
    {
        // 清掉可能还在跑的旧动画，保证状态干净
        BeginAnimation(OpacityProperty, null);

        if (_handle == IntPtr.Zero || !IsVisible)
        {
            // 首次 Show 才会创建 HWND；此刻 Opacity=0，肉眼不可见
            Show();
        }

        if (_handle == IntPtr.Zero)
        {
            onFinished?.Invoke();
            return;
        }

        // 此刻 Opacity 还是 0：先把位置和 Z 序摆好，再让它显现，避免闪烁
        NativeMethods.SetWindowPos(_handle, targetWindow, x, y, width, height,
            NativeMethods.SWP_NOACTIVATE | NativeMethods.SWP_SHOWWINDOW);

        Opacity = 1;

        var fade = new DoubleAnimation
        {
            From = 1,
            To = 0,
            BeginTime = TimeSpan.FromMilliseconds(holdMs),
            Duration = new Duration(TimeSpan.FromMilliseconds(fadeMs)),
            FillBehavior = FillBehavior.HoldEnd,
        };

        fade.Completed += (_, _) =>
        {
            Conceal();
            onFinished?.Invoke();
        };

        BeginAnimation(OpacityProperty, fade);
    }

    /// <summary>立即隐藏（解锁、退出时用）。会先停掉淡出动画。</summary>
    public void Conceal()
    {
        BeginAnimation(OpacityProperty, null); // 动画会覆盖 Opacity，必须先解除
        Opacity = 0;

        if (IsVisible)
        {
            Hide();
        }
    }

    /// <summary>安全关闭：从未 Show 过的窗口直接跳过，避免 WPF 抛异常。</summary>
    public void CloseSafely()
    {
        if (_handle == IntPtr.Zero) return;

        Close();
    }
}
