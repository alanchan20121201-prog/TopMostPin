namespace TopMostPin.Services;

/// <summary>
/// 所有面向用户的提示文案集中在这里，改文案只改这个文件即可。
/// 带 {0} 的用 string.Format 填充。
///
/// 四种语言版本共用同一份源码，由编译期常量切换：
///   dotnet publish -p:AppLang=zh-CN | zh-TW | en | ja
/// 每个 EXE 只包含一种语言，运行时不判断系统区域。
/// </summary>
public static class Messages
{
#if LANG_ZH_TW
    // ==================== 繁體中文 ====================
    public const string AppTitle = "TopMostPin";
    public const string HotkeyConflict = "快捷鍵衝突，請在托盤選單中更換。";
    public const string HotkeyChanged = "快捷鍵已變更為 {0}。";
    public const string HotkeyNeedKey = "請先按下一個快捷鍵組合。";
    public const string ElevationRequired = "該視窗需要管理員權限，請以管理員身分執行本工具後重試。";
    public const string FullscreenUnsupported = "全螢幕應用程式暫不支援置頂。";
    public const string FullscreenCoversLocked = "該視窗被獨占全螢幕應用程式覆蓋，暫時無法保持置頂。退出全螢幕後會自動恢復。";
    public const string SystemWindowUnsupported = "該視窗不支援置頂。";
    public const string NoWindowUnderCursor = "該視窗不支援置頂。";
    public const string PinFailed = "置頂失敗（錯誤碼 {0}），請嘗試以管理員身分執行本工具。";
    public const string Locked = "已鎖定置頂，目前鎖定 {0} 個視窗。";
    public const string Unlocked = "已取消鎖定。";
    public const string UnlockedAll = "已取消全部鎖定。";
    public const string NothingLocked = "目前沒有鎖定的視窗。";
    public const string StartupEnabled = "已開啟開機自啟。";
    public const string StartupDisabled = "已關閉開機自啟。";
    public const string StartupFailed = "修改開機自啟失敗，可能被安全軟體攔截。";
    public const string UsageHint = "按一下 {0} 並指向視窗即可鎖定置頂，再按一次取消鎖定。";
    public const string NoHotkeyRegistered = "還沒有可用的快捷鍵，請右鍵托盤圖示進行設定。";
    public const string MenuHotkey = "修改快捷鍵";
    public const string MenuUnlockAll = "全部解鎖";
    public const string MenuStartup = "開機自啟";
    public const string MenuExit = "結束";
    public const string HotkeyNotRegistered = "未註冊";
    public const string DialogTitle = "修改快捷鍵";
    public const string DialogHint = "按下新的快捷鍵組合";
    public const string DialogRule = "必須包含 Ctrl / Alt / Shift / Win 中的至少一個，按 Esc 取消。";
    public const string DialogOk = "確定";
    public const string DialogCancel = "取消";
    public const string HotkeyUnset = "未設定";
    public const string LockedCount = "（已鎖定 {0} 個視窗）";
    public const string HotkeyNeedNormalKey = "請再按一個普通按鍵（例如 T、F8、空白鍵）";
    public const string HotkeyUnsupported = "這個按鍵不支援註冊為全域熱鍵";
    public const string HotkeyNeedModifier = "請至少按住 Ctrl / Alt / Shift / Win 中的一個";

#elif LANG_EN
    // ==================== English ====================
    public const string AppTitle = "TopMostPin";
    public const string HotkeyConflict = "Hotkey conflict. Please choose another one in the tray menu.";
    public const string HotkeyChanged = "Hotkey changed to {0}.";
    public const string HotkeyNeedKey = "Please press a hotkey combination first.";
    public const string ElevationRequired = "This window requires administrator privileges. Run this tool as administrator and try again.";
    public const string FullscreenUnsupported = "Full-screen apps cannot be pinned on top.";
    public const string FullscreenCoversLocked = "An exclusive full-screen app is covering this window, so top-most cannot be maintained. It will resume after you leave full screen.";
    public const string SystemWindowUnsupported = "This window cannot be pinned on top.";
    public const string NoWindowUnderCursor = "This window cannot be pinned on top.";
    public const string PinFailed = "Failed to pin (error {0}). Try running this tool as administrator.";
    public const string Locked = "Pinned on top. {0} window(s) locked.";
    public const string Unlocked = "Unlocked.";
    public const string UnlockedAll = "All windows unlocked.";
    public const string NothingLocked = "No windows are currently locked.";
    public const string StartupEnabled = "Start with Windows enabled.";
    public const string StartupDisabled = "Start with Windows disabled.";
    public const string StartupFailed = "Could not change the startup setting; it may be blocked by security software.";
    public const string UsageHint = "Press {0} while pointing at a window to pin it, then press again to unpin.";
    public const string NoHotkeyRegistered = "No usable hotkey yet. Right-click the tray icon to set one.";
    public const string MenuHotkey = "Change hotkey";
    public const string MenuUnlockAll = "Unlock all";
    public const string MenuStartup = "Start with Windows";
    public const string MenuExit = "Exit";
    public const string HotkeyNotRegistered = "Not registered";
    public const string DialogTitle = "Change hotkey";
    public const string DialogHint = "Press the new hotkey combination";
    public const string DialogRule = "Include at least one of Ctrl / Alt / Shift / Win. Press Esc to cancel.";
    public const string DialogOk = "OK";
    public const string DialogCancel = "Cancel";
    public const string HotkeyUnset = "Not set";
    public const string LockedCount = " ({0} window(s) locked)";
    public const string HotkeyNeedNormalKey = "Press a normal key as well (e.g. T, F8, Space)";
    public const string HotkeyUnsupported = "This key cannot be registered as a global hotkey";
    public const string HotkeyNeedModifier = "Hold at least one of Ctrl / Alt / Shift / Win";

#elif LANG_JA
    // ==================== 日本語 ====================
    public const string AppTitle = "TopMostPin";
    public const string HotkeyConflict = "ホットキーが競合しています。トレイメニューで別のキーに変更してください。";
    public const string HotkeyChanged = "ホットキーを {0} に変更しました。";
    public const string HotkeyNeedKey = "まずホットキーの組み合わせを押してください。";
    public const string ElevationRequired = "このウィンドウには管理者権限が必要です。本ツールを管理者として実行して再試行してください。";
    public const string FullscreenUnsupported = "全画面アプリは最前面に固定できません。";
    public const string FullscreenCoversLocked = "排他全画面アプリがこのウィンドウを覆っているため、最前面を維持できません。全画面を終了すると自動的に復帰します。";
    public const string SystemWindowUnsupported = "このウィンドウは最前面に固定できません。";
    public const string NoWindowUnderCursor = "このウィンドウは最前面に固定できません。";
    public const string PinFailed = "最前面固定に失敗しました（エラー {0}）。管理者として実行してお試しください。";
    public const string Locked = "最前面に固定しました。現在 {0} 個のウィンドウをロック中です。";
    public const string Unlocked = "ロックを解除しました。";
    public const string UnlockedAll = "すべてのロックを解除しました。";
    public const string NothingLocked = "現在ロック中のウィンドウはありません。";
    public const string StartupEnabled = "自動起動を有効にしました。";
    public const string StartupDisabled = "自動起動を無効にしました。";
    public const string StartupFailed = "自動起動の設定を変更できませんでした。セキュリティソフトにブロックされた可能性があります。";
    public const string UsageHint = "{0} を押しながらウィンドウにカーソルを合わせると最前面固定、もう一度押すと解除します。";
    public const string NoHotkeyRegistered = "使用できるホットキーがありません。トレイアイコンを右クリックして設定してください。";
    public const string MenuHotkey = "ホットキーを変更";
    public const string MenuUnlockAll = "すべて解除";
    public const string MenuStartup = "自動起動";
    public const string MenuExit = "終了";
    public const string HotkeyNotRegistered = "未登録";
    public const string DialogTitle = "ホットキーを変更";
    public const string DialogHint = "新しいホットキーの組み合わせを押してください";
    public const string DialogRule = "Ctrl / Alt / Shift / Win のいずれかを必ず含めてください。Esc でキャンセル。";
    public const string DialogOk = "OK";
    public const string DialogCancel = "キャンセル";
    public const string HotkeyUnset = "未設定";
    public const string LockedCount = "（{0} 個のウィンドウをロック中）";
    public const string HotkeyNeedNormalKey = "通常のキーも押してください（例：T、F8、スペース）";
    public const string HotkeyUnsupported = "このキーはグローバルホットキーに登録できません";
    public const string HotkeyNeedModifier = "Ctrl / Alt / Shift / Win のいずれかを押したままにしてください";

#else
    // ==================== 简体中文（预设） ====================
    public const string AppTitle = "TopMostPin";
    public const string HotkeyConflict = "快捷键冲突，请在托盘菜单中更换。";
    public const string HotkeyChanged = "快捷键已更改为 {0}。";
    public const string HotkeyNeedKey = "请先按下一个快捷键组合。";
    public const string ElevationRequired = "该窗口需要管理员权限，请以管理员身份运行本工具后重试。";
    public const string FullscreenUnsupported = "全屏应用暂不支持置顶。";
    public const string FullscreenCoversLocked = "该窗口被独占全屏应用覆盖，暂时无法保持置顶。退出全屏后会自动恢复。";
    public const string SystemWindowUnsupported = "该窗口不支持置顶。";
    public const string NoWindowUnderCursor = "该窗口不支持置顶。";
    public const string PinFailed = "置顶失败（错误码 {0}），请尝试以管理员身份运行本工具。";
    public const string Locked = "已锁定置顶，当前锁定 {0} 个窗口。";
    public const string Unlocked = "已取消锁定。";
    public const string UnlockedAll = "已取消全部锁定。";
    public const string NothingLocked = "当前没有锁定的窗口。";
    public const string StartupEnabled = "已开启开机自启。";
    public const string StartupDisabled = "已关闭开机自启。";
    public const string StartupFailed = "修改开机自启失败，可能被安全软件拦截。";
    public const string UsageHint = "按一下 {0} 并指向窗口即可锁定置顶，再按一次取消锁定。";
    public const string NoHotkeyRegistered = "还没有可用的快捷键，请右键托盘图标进行设置。";
    public const string MenuHotkey = "修改快捷键";
    public const string MenuUnlockAll = "全部解锁";
    public const string MenuStartup = "开机自启";
    public const string MenuExit = "退出";
    public const string HotkeyNotRegistered = "未注册";
    public const string DialogTitle = "修改快捷键";
    public const string DialogHint = "按下新的快捷键组合";
    public const string DialogRule = "必须包含 Ctrl / Alt / Shift / Win 中的至少一个，按 Esc 取消。";
    public const string DialogOk = "确定";
    public const string DialogCancel = "取消";
    public const string HotkeyUnset = "未设置";
    public const string LockedCount = "（已锁定 {0} 个窗口）";
    public const string HotkeyNeedNormalKey = "请再按一个普通按键（例如 T、F8、空格）";
    public const string HotkeyUnsupported = "这个按键不支持注册为全局热键";
    public const string HotkeyNeedModifier = "请至少按住 Ctrl / Alt / Shift / Win 中的一个";

#endif
}
