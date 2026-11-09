using System.Windows;
using System.Windows.Input;
using TopMostPin.Hotkey;
using TopMostPin.Services;

namespace TopMostPin.UI;

/// <summary>
/// "修改快捷键"对话框：用户在这里直接按组合键录制，确定时尝试注册。
/// 注册失败（被占用 / 与另一个功能重复）会停留在对话框内提示，不会丢失原来的快捷键。
/// </summary>
public partial class HotkeySettingsWindow : Window
{
    private readonly HotkeyManager _hotkeyManager;
    private HotkeyDefinition? _pending;

    /// <summary>是否成功保存了新快捷键</summary>
    public bool Saved { get; private set; }

    public HotkeySettingsWindow(HotkeyManager hotkeyManager)
    {
        InitializeComponent();

        _hotkeyManager = hotkeyManager;

        Title = Messages.DialogTitle;
        DialogTitleText.Text = Messages.DialogHint;
        DialogRuleText.Text = Messages.DialogRule;
        OkButton.Content = Messages.DialogOk;
        CancelButton.Content = Messages.DialogCancel;

        _pending = hotkeyManager.Current;

        ComboText.Text = _pending?.ToString() ?? Messages.HotkeyUnset;
        Loaded += (_, _) => ComboText.Focus();
    }

    /// <summary>
    /// 在窗口级别拦截按键，直接把当前按下的组合录进来。
    /// </summary>
    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        // Alt 组合键在 WPF 里会变成 Key.System，真实按键在 SystemKey 里
        Key key = e.Key == Key.System ? e.SystemKey : e.Key;

        // Esc 交给窗口处理（取消），不当作快捷键
        if (key == Key.Escape && Keyboard.Modifiers == ModifierKeys.None)
        {
            return;
        }

        ModifierKeys modifiers = Keyboard.Modifiers;

        // 只按了修饰键：先实时预览一下，不当作完整组合
        if (key is Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt
            or Key.LeftShift or Key.RightShift or Key.LWin or Key.RWin)
        {
            string preview = HotkeyDefinition.DescribeModifiers(modifiers);
            HintText.Text = string.Empty;
            ComboText.Text = string.IsNullOrEmpty(preview) ? "…" : preview + "+…";
            e.Handled = true;
            return;
        }

        // 没按修饰键就按回车：交给默认按钮"确定"
        if (key == Key.Enter && modifiers == ModifierKeys.None)
        {
            return;
        }

        e.Handled = true;

        if (!HotkeyDefinition.TryFromKey(key, modifiers, out var definition, out string error) || definition == null)
        {
            HintText.Text = error;
            return;
        }

        _pending = definition;
        HintText.Text = string.Empty;
        ComboText.Text = definition.ToString();
    }

    private void OnConfirmClick(object sender, RoutedEventArgs e)
    {
        if (_pending == null)
        {
            HintText.Text = Messages.HotkeyNeedKey;
            return;
        }

        if (!_hotkeyManager.TryRegister(_pending, out string error))
        {
            // 被别的程序占用：留在对话框，让用户换一个
            HintText.Text = error;
            return;
        }

        Saved = true;
        DialogResult = true;
        Close();
    }
}
