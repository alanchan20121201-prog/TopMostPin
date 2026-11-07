namespace TopMostPin.Services;

/// <summary>
/// 所有面向用户的提示文案集中在这里，改文案只改这个文件即可。
/// 带 {0} 的用 string.Format 填充。
/// </summary>
public static class Messages
{
    /// <summary>托盘提示气泡的标题</summary>
    public const string AppTitle = "TopMostPin";

    // ==================== 快捷键 ====================

    /// <summary>快捷键被别的程序占用，或注册失败</summary>
    public const string HotkeyConflict = "快捷键冲突，请在托盘菜单中更换。";

    /// <summary>快捷键修改成功，{0} = 组合键文本</summary>
    public const string HotkeyChanged = "快捷键已更改为 {0}。";

    /// <summary>录制快捷键时没有按下有效组合</summary>
    public const string HotkeyNeedKey = "请先按下一个快捷键组合。";

    // ==================== 置顶失败原因 ====================

    /// <summary>目标窗口属于管理员权限进程</summary>
    public const string ElevationRequired = "该窗口需要管理员权限，请以管理员身份运行本工具后重试。";

    /// <summary>目标窗口是全屏应用</summary>
    public const string FullscreenUnsupported = "全屏应用暂不支持置顶。";

    /// <summary>锁定窗口被独占全屏应用盖住，置顶维持无法生效</summary>
    public const string FullscreenCoversLocked = "该窗口被独占全屏应用覆盖，暂时无法保持置顶。退出全屏后会自动恢复。";

    /// <summary>桌面、任务栏等系统保护窗口</summary>
    public const string SystemWindowUnsupported = "该窗口不支持置顶。";

    /// <summary>鼠标下没有可用的目标窗口</summary>
    public const string NoWindowUnderCursor = "该窗口不支持置顶。";

    /// <summary>置顶动作被系统拒绝（UIPI 等），{0} = 错误码</summary>
    public const string PinFailed = "置顶失败（错误码 {0}），请尝试以管理员身份运行本工具。";

    // ==================== 操作结果 ====================

    /// <summary>锁定成功，{0} = 当前锁定数量</summary>
    public const string Locked = "已锁定置顶，当前锁定 {0} 个窗口。";

    /// <summary>取消单个锁定</summary>
    public const string Unlocked = "已取消锁定。";

    /// <summary>全部解锁</summary>
    public const string UnlockedAll = "已取消全部锁定。";

    /// <summary>没有任何锁定时的"全部解锁"提示</summary>
    public const string NothingLocked = "当前没有锁定的窗口。";

    /// <summary>开机自启开启</summary>
    public const string StartupEnabled = "已开启开机自启。";

    /// <summary>开机自启关闭</summary>
    public const string StartupDisabled = "已关闭开机自启。";

    /// <summary>写注册表失败</summary>
    public const string StartupFailed = "修改开机自启失败，可能被安全软件拦截。";

    /// <summary>双击托盘图标的用法提示，{0} = 快捷键</summary>
    public const string UsageHint = "按一下 {0} 并指向窗口即可锁定置顶，再按一次取消锁定。";

    /// <summary>没有可用快捷键时的提示</summary>
    public const string NoHotkeyRegistered = "还没有可用的快捷键，请右键托盘图标进行设置。";

    // ==================== 托盘菜单文字 ====================

    public const string MenuHotkey = "修改快捷键";
    public const string MenuUnlockAll = "全部解锁";
    public const string MenuStartup = "开机自启";
    public const string MenuExit = "退出";
    public const string HotkeyNotRegistered = "未注册";
}
