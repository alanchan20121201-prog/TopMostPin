using System.Windows.Forms;
using TopMostPin.Hotkey;
using TopMostPin.Services;
using TopMostPin.UI;

namespace TopMostPin.Tray;

/// <summary>
/// 托盘控制器：程序"界面"的全部内容。
/// 负责托盘图标（含锁定数量角标）、右键菜单（修改快捷键 / 全部解锁 / 开机自启 / 退出），
/// 并把快捷键、置顶逻辑、设置存储串起来。
/// </summary>
public sealed class TrayIconController : IDisposable
{
    private readonly NotifyIcon _notifyIcon;
    private readonly ContextMenuStrip _menu;
    private readonly ToolStripMenuItem _hotkeyMenuItem;
    private readonly ToolStripMenuItem _unlockAllMenuItem;
    private readonly ToolStripMenuItem _startupMenuItem;

    private readonly HotkeyManager _hotkeyManager;
    private readonly WindowPinner _pinner;
    private readonly IconFactory _iconFactory;
    private readonly AppSettings _settings;

    private bool _disposed;

    public TrayIconController()
    {
        _settings = AppSettings.Load();
        _iconFactory = new IconFactory();

        // ---------- 快捷键 ----------
        _hotkeyManager = new HotkeyManager();
        _hotkeyManager.Pressed += OnHotkeyPressed;

        // ---------- 置顶逻辑 ----------
        _pinner = new WindowPinner();
        _pinner.Notify += OnPinNotify;
        _pinner.LockedCountChanged += OnLockedCountChanged;

        // ---------- 菜单项 ----------
        _hotkeyMenuItem = new ToolStripMenuItem(
            Messages.MenuHotkey, null, (_, _) => ShowHotkeyDialog());

        _unlockAllMenuItem = new ToolStripMenuItem(
            Messages.MenuUnlockAll, null, (_, _) => _pinner.UnlockAll());

        _startupMenuItem = new ToolStripMenuItem(
            Messages.MenuStartup, null, (_, _) => ToggleStartup());

        var exitMenuItem = new ToolStripMenuItem(
            Messages.MenuExit, null, (_, _) => ExitApplication());

        _menu = new ContextMenuStrip();
        _menu.Items.Add(_hotkeyMenuItem);
        _menu.Items.Add(new ToolStripSeparator());
        _menu.Items.Add(_unlockAllMenuItem);
        _menu.Items.Add(new ToolStripSeparator());
        _menu.Items.Add(_startupMenuItem);
        _menu.Items.Add(new ToolStripSeparator());
        _menu.Items.Add(exitMenuItem);

        // 每次打开菜单前刷新文字和勾选状态
        _menu.Opening += (_, _) => RefreshMenuState();

        // ---------- 托盘图标 ----------
        _notifyIcon = new NotifyIcon
        {
            Icon = _iconFactory.GetTrayIcon(0),
            Text = Messages.AppTitle,
            Visible = true,
            ContextMenuStrip = _menu,
        };
        _notifyIcon.DoubleClick += (_, _) => ShowUsageHint();
    }

    /// <summary>启动时注册快捷键。注册失败（被占用）时用气泡提示，不弹任何窗口。</summary>
    public void Start()
    {
        RefreshMenuState();
        RefreshBadge();

        if (!TryApplyHotkey(_settings.ResolveHotkey()))
        {
            // 只弹托盘气泡：启动阶段绝不能弹出窗口挡住桌面。
            // 托盘菜单里对应项会显示"（未注册）"，用户可以自己点进去改。
            ShowBalloon(Messages.HotkeyConflict, true);
        }
    }

    // ==================== 快捷键 ====================

    private void OnHotkeyPressed()
    {
        _pinner.ToggleLockUnderCursor();
    }

    /// <summary>
    /// 注册快捷键并把结果写回设置文件。失败返回 false，具体提示由调用方决定。
    /// </summary>
    private bool TryApplyHotkey(HotkeyDefinition definition)
    {
        if (!_hotkeyManager.TryRegister(definition, out string error))
        {
            System.Diagnostics.Debug.WriteLine($"[TopMostPin] 快捷键注册失败：{error}");
            return false;
        }

        _settings.Hotkey = definition.ToString();
        _settings.Save();

        return true;
    }

    private void ShowHotkeyDialog()
    {
        // 从菜单点击时菜单还没完全关闭，用 BeginInvoke 延后到消息循环空闲再弹窗，
        // 避免模态窗口被菜单的鼠标捕获卡住。
        System.Windows.Application.Current.Dispatcher.BeginInvoke(new Action(() =>
        {
            var dialog = new HotkeySettingsWindow(_hotkeyManager);

            bool? result = dialog.ShowDialog();

            if (result == true && dialog.Saved && _hotkeyManager.Current is { } registered)
            {
                _settings.Hotkey = registered.ToString();
                _settings.Save();

                ShowBalloon(string.Format(Messages.HotkeyChanged, registered), false);
            }

            RefreshMenuState();
        }));
    }

    // ==================== 开机自启 ====================

    private void ToggleStartup()
    {
        bool target = !StartupManager.IsEnabled;

        if (StartupManager.SetEnabled(target))
        {
            ShowBalloon(target ? Messages.StartupEnabled : Messages.StartupDisabled, false);
        }
        else
        {
            ShowBalloon(Messages.StartupFailed, true);
        }

        RefreshMenuState();
    }

    // ==================== 菜单 / 图标 / 提示 ====================

    /// <summary>锁定数量变化时刷新托盘角标。</summary>
    private void OnLockedCountChanged()
    {
        RefreshBadge();
        RefreshMenuState();
    }

    private void RefreshBadge()
    {
        int count = _pinner.LockedCount;

        // 角标数字由 IconFactory 画到图标右下角；按数量缓存，不会泄漏 GDI 句柄
        _notifyIcon.Icon = _iconFactory.GetTrayIcon(count);
        _notifyIcon.Text = count > 0
            ? $"{Messages.AppTitle}（已锁定 {count} 个窗口）"
            : Messages.AppTitle;
    }

    private void RefreshMenuState()
    {
        HotkeyDefinition? hotkey = _hotkeyManager.Current;
        int lockedCount = _pinner.LockedCount;

        _hotkeyMenuItem.Text = hotkey == null
            ? $"{Messages.MenuHotkey}（{Messages.HotkeyNotRegistered}）"
            : $"{Messages.MenuHotkey}（{hotkey}）";

        _unlockAllMenuItem.Text = lockedCount > 0
            ? $"{Messages.MenuUnlockAll}（{lockedCount}）"
            : Messages.MenuUnlockAll;
        _unlockAllMenuItem.Enabled = lockedCount > 0;

        _startupMenuItem.Checked = StartupManager.IsEnabled;
    }

    private void OnPinNotify(string message, bool isWarning)
    {
        ShowBalloon(message, isWarning);
    }

    /// <summary>用托盘气泡提示，不抢焦点。</summary>
    private void ShowBalloon(string message, bool isWarning)
    {
        _notifyIcon.ShowBalloonTip(
            3000,
            Messages.AppTitle,
            message,
            isWarning ? ToolTipIcon.Warning : ToolTipIcon.Info);
    }

    private void ShowUsageHint()
    {
        HotkeyDefinition? hotkey = _hotkeyManager.Current;

        ShowBalloon(hotkey == null
            ? Messages.NoHotkeyRegistered
            : string.Format(Messages.UsageHint, hotkey), false);
    }

    private void ExitApplication()
    {
        // ShutdownMode=OnExplicitShutdown，必须显式关闭
        System.Windows.Application.Current.Shutdown();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        // 先退订事件，避免 Dispose 过程中再弹气泡
        _hotkeyManager.Pressed -= OnHotkeyPressed;
        _pinner.Notify -= OnPinNotify;
        _pinner.LockedCountChanged -= OnLockedCountChanged;

        _hotkeyManager.Dispose();
        _pinner.Dispose();

        // 先隐藏再释放，避免托盘里残留"幽灵图标"
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
        _menu.Dispose();
        _iconFactory.Dispose();
    }
}
