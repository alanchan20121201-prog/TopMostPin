using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Threading;
using TopMostPin.Services;
using TopMostPin.Tray;

namespace TopMostPin;

/// <summary>
/// 应用入口。本程序是纯托盘程序（Tray-only）：
///   - 没有 StartupUri，没有 MainWindow，启动后桌面上不会出现任何窗口；
///   - 整个界面只有托盘图标 + 右键菜单 + 设置对话框（按需弹出）。
///
/// 注意：这里刻意不显示任何 MessageBox。
/// 启动阶段一旦弹窗，桌面上就会冒出一个白色小窗口挡住用户正在做的事，
/// 所以所有启动期提示都改走托盘气泡。
/// </summary>
public partial class App : Application
{
    // 单实例互斥体：避免重复启动注册同一个全局快捷键
    private Mutex? _singleInstanceMutex;

    private TrayIconController? _tray;

    protected override void OnStartup(StartupEventArgs e)
    {
        // ---------- 纯托盘运行 ----------
        // 没有任何主窗口，绝不能因为"窗口都关掉了"而自动退出。
        // 这里用全限定名，避免和 ShutdownMode 属性同名产生歧义。
        ShutdownMode = System.Windows.ShutdownMode.OnExplicitShutdown;

        // ---------- 单实例检查 ----------
        _singleInstanceMutex = new Mutex(true, @"TopMostPin.SingleInstance.Mutex", out bool isFirstInstance);
        if (!isFirstInstance)
        {
            // 已经有一份在托盘里跑了：静默退出，不弹任何窗口。
            // 想找它的话，托盘里那个蓝色图标就是。
            Shutdown();
            return;
        }

        base.OnStartup(e);

        // 兜底异常处理：托盘程序没有界面，这里只写日志，绝不弹窗
        DispatcherUnhandledException += OnDispatcherUnhandledException;

        // ---------- 只做两件事：托盘图标 + 全局快捷键监听 ----------
        _tray = new TrayIconController();
        _tray.Start();
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        e.Handled = true; // 不让进程退出，托盘程序尽量保持存活

        LogFatal(e.Exception);
    }

    /// <summary>把异常写到 %APPDATA%\TopMostPin\error.log，不打断用户。</summary>
    private static void LogFatal(Exception exception)
    {
        try
        {
            string directory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "TopMostPin");

            Directory.CreateDirectory(directory);

            File.AppendAllText(
                Path.Combine(directory, "error.log"),
                $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {exception}{Environment.NewLine}{Environment.NewLine}");
        }
        catch
        {
            // 连日志都写不了就算了，绝不能让异常处理本身再抛异常
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _tray?.Dispose();
        _singleInstanceMutex?.ReleaseMutex();
        _singleInstanceMutex?.Dispose();
        base.OnExit(e);
    }
}
