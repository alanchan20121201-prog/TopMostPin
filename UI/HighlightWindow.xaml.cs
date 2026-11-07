using System.Windows;
using System.Windows.Interop;
using TopMostPin.Interop;

namespace TopMostPin.UI;

/// <summary>
/// 锁定窗口的高亮边框窗口。每个锁定窗口各持有一个实例。
///
/// 关键点：
///  1) WS_EX_TRANSPARENT   -> 鼠标穿透，WindowFromPoint 也不会命中它；
///  2) WS_EX_NOACTIVATE    -> 显示时不会抢走目标窗口的焦点；
///  3) WS_EX_TOOLWINDOW    -> 不出现在 Alt+Tab / 任务栏；
///  4) 用 SetWindowPos 的 hWndInsertAfter 参数把边框插到"目标窗口正上方"，
///     而不是简单地全局置顶 —— 这是多个窗口重叠时不穿帮的关键。
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
    /// 把边框摆到 <paramref name="targetWindow"/> 正上方，并贴合指定矩形（物理像素）。
    ///
    /// SetWindowPos 的第二个参数 = 目标窗口句柄，含义是"插到这个窗口的上面"。
    /// 这样 Z 序就是：目标窗口 -> 它的边框 -> 更上面的其他窗口。
    /// 两个锁定窗口重叠时，上面的窗口会正常盖住下面的边框，视觉上不会错位。
    /// </summary>
    public void ShowAbove(IntPtr targetWindow, int x, int y, int width, int height)
    {
        if (_handle == IntPtr.Zero || !IsVisible)
        {
            // 首次 Show 才会创建 HWND；此刻 Opacity=0，肉眼不可见
            Show();
        }

        if (_handle == IntPtr.Zero) return;

        NativeMethods.SetWindowPos(_handle, targetWindow, x, y, width, height,
            NativeMethods.SWP_NOACTIVATE | NativeMethods.SWP_SHOWWINDOW);

        Opacity = 1; // 摆好位置之后再显现
    }

    /// <summary>临时隐藏（比如目标窗口最小化）。先变全透明再 Hide，避免残留一帧。</summary>
    public void Conceal()
    {
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
