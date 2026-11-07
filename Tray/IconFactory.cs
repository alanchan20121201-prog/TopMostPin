using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using TopMostPin.Interop;

namespace TopMostPin.Tray;

/// <summary>
/// 托盘图标工厂：运行时用 GDI+ 画图标，不需要额外打包 .ico 文件。
///
/// 支持角标：右下角显示当前锁定窗口数量。图标按数量缓存，
/// 切换时复用同一个 Icon 对象，避免频繁创建/销毁 HICON 造成 GDI 句柄泄漏。
///
/// 想换成自己的图标：把 DrawBaseSymbol() 里的绘制代码替换成绘制自己的图，
/// 角标逻辑（DrawBadge）可以原样保留。
/// </summary>
public sealed class IconFactory : IDisposable
{
    /// <summary>图标尺寸（像素）。32 在 Win10/11 托盘缩放下最清晰。</summary>
    private const int Size = 32;

    /// <summary>数量 -> (Icon, HICON)，HICON 需要在销毁时 DestroyIcon</summary>
    private readonly Dictionary<int, (Icon Icon, IntPtr Handle)> _cache = new();

    /// <summary>
    /// 取对应锁定数量的托盘图标。数量 0 表示普通图标，1-9 显示数字，超过 9 显示 "9+"。
    /// </summary>
    public Icon GetTrayIcon(int lockedCount)
    {
        if (_cache.TryGetValue(lockedCount, out var cached))
        {
            return cached.Icon;
        }

        IntPtr handle = CreateIconHandle(lockedCount);
        var icon = Icon.FromHandle(handle);
        _cache[lockedCount] = (icon, handle);
        return icon;
    }

    private static IntPtr CreateIconHandle(int lockedCount)
    {
        using var bitmap = new Bitmap(Size, Size);

        using (var g = Graphics.FromImage(bitmap))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.AntiAlias;
            g.Clear(Color.Transparent);

            DrawBaseSymbol(g);

            if (lockedCount > 0)
            {
                DrawBadge(g, lockedCount);
            }
        }

        // GetHicon 返回的 HICON 需要手动 DestroyIcon 释放
        return bitmap.GetHicon();
    }

    /// <summary>深色圆角底 + 向上箭头 + 顶部横杠，表示"钉到最上层"。</summary>
    private static void DrawBaseSymbol(Graphics g)
    {
        using (var background = new GraphicsPath())
        {
            const float r = 7f;
            background.AddArc(0, 0, r * 2, r * 2, 180, 90);
            background.AddArc(Size - r * 2, 0, r * 2, r * 2, 270, 90);
            background.AddArc(Size - r * 2, Size - r * 2, r * 2, r * 2, 0, 90);
            background.AddArc(0, Size - r * 2, r * 2, r * 2, 90, 90);
            background.CloseFigure();

            using var brush = new SolidBrush(Color.FromArgb(255, 0x26, 0x32, 0x38));
            g.FillPath(brush, background);
        }

        using var accent = new SolidBrush(Color.FromArgb(255, 0x4F, 0xC3, 0xF7));

        // 三角箭头
        g.FillPolygon(accent, new[]
        {
            new PointF(16f, 6f),
            new PointF(23f, 15f),
            new PointF(9f, 15f),
        });

        // 箭头下方的短柱
        g.FillRectangle(accent, 14.5f, 15f, 3f, 3.5f);

        // 一条横线：符号化地表示"顶边"
        g.FillRectangle(accent, 8f, 21.5f, 16f, 3f);
    }

    /// <summary>右下角画一个橙色圆形角标 + 数字。</summary>
    private static void DrawBadge(Graphics g, int count)
    {
        const float diameter = 17f;
        float x = Size - diameter;
        float y = Size - diameter;

        // 角标外圈先用深色描一道，保证在浅色/深色任务栏上都清晰
        using (var outline = new SolidBrush(Color.FromArgb(255, 0x26, 0x32, 0x38)))
        {
            g.FillEllipse(outline, x - 1.5f, y - 1.5f, diameter + 3f, diameter + 3f);
        }

        using (var fill = new SolidBrush(Color.FromArgb(255, 0xFF, 0x8A, 0x3D)))
        {
            g.FillEllipse(fill, x, y, diameter, diameter);
        }

        string text = count > 9 ? "9+" : count.ToString();

        using var font = new Font("Segoe UI", text.Length > 1 ? 8.5f : 10.5f, FontStyle.Bold, GraphicsUnit.Pixel);
        using var textBrush = new SolidBrush(Color.White);
        using var format = new StringFormat
        {
            Alignment = StringAlignment.Center,
            LineAlignment = StringAlignment.Center,
        };

        // 文字区域稍微上移 0.5px，视觉上更居中
        g.DrawString(text, font, textBrush, new RectangleF(x, y - 0.5f, diameter, diameter), format);
    }

    public void Dispose()
    {
        foreach (var (_, handle) in _cache.Values)
        {
            if (handle != IntPtr.Zero)
            {
                NativeMethods.DestroyIcon(handle);
            }
        }

        _cache.Clear();
    }
}
