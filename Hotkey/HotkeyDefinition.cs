using System.Windows.Input;
using TopMostPin.Interop;

namespace TopMostPin.Hotkey;

/// <summary>
/// 一个快捷键组合 = 修饰键(Ctrl/Alt/Shift/Win) + 一个主键。
/// 同时负责字符串序列化（"Ctrl+Alt+T"）和反序列化，供设置文件和界面显示使用。
/// </summary>
public sealed class HotkeyDefinition
{
    /// <summary>默认快捷键：Ctrl+Alt+L（锁定置顶，按一下切换）</summary>
    public static readonly HotkeyDefinition Default =
        new(NativeMethods.MOD_CONTROL | NativeMethods.MOD_ALT, 0x4C /* VK_L */);

    /// <summary>Win32 修饰键标志位（MOD_CONTROL 等）</summary>
    public uint Modifiers { get; }

    /// <summary>主键虚拟键码（VK_xxx）</summary>
    public uint VirtualKey { get; }

    public HotkeyDefinition(uint modifiers, uint virtualKey)
    {
        Modifiers = modifiers;
        VirtualKey = virtualKey;
    }

    /// <summary>输出形如 "Ctrl+Alt+T" 的显示名。</summary>
    public override string ToString()
    {
        var parts = new List<string>();
        if ((Modifiers & NativeMethods.MOD_CONTROL) != 0) parts.Add("Ctrl");
        if ((Modifiers & NativeMethods.MOD_ALT) != 0) parts.Add("Alt");
        if ((Modifiers & NativeMethods.MOD_SHIFT) != 0) parts.Add("Shift");
        if ((Modifiers & NativeMethods.MOD_WIN) != 0) parts.Add("Win");
        parts.Add(GetKeyName(VirtualKey));
        return string.Join("+", parts);
    }

    /// <summary>把虚拟键码转成可读名称（A-Z / 0-9 / F1-F24 / 其它交给 WPF 的 Key 枚举）。</summary>
    public static string GetKeyName(uint virtualKey)
    {
        int vk = (int)virtualKey;

        if (vk >= 0x41 && vk <= 0x5A) return ((char)vk).ToString();          // A-Z
        if (vk >= 0x30 && vk <= 0x39) return ((char)vk).ToString();          // 0-9
        if (vk >= 0x70 && vk <= 0x87) return "F" + (vk - 0x6F);              // F1-F24 (0x70 = F1)

        try
        {
            Key key = KeyInterop.KeyFromVirtualKey(vk);
            return key == Key.None ? $"VK{vk:X2}" : key.ToString();
        }
        catch
        {
            return $"VK{vk:X2}";
        }
    }

    /// <summary>把修饰键描述成文本，用于录快捷键过程中的实时预览。</summary>
    public static string DescribeModifiers(ModifierKeys modifiers)
    {
        var parts = new List<string>();
        if (modifiers.HasFlag(ModifierKeys.Control)) parts.Add("Ctrl");
        if (modifiers.HasFlag(ModifierKeys.Alt)) parts.Add("Alt");
        if (modifiers.HasFlag(ModifierKeys.Shift)) parts.Add("Shift");
        if (modifiers.HasFlag(ModifierKeys.Windows)) parts.Add("Win");
        return string.Join("+", parts);
    }

    /// <summary>
    /// 从 WPF 的按键事件构造快捷键。要求至少带一个修饰键，
    /// 避免用户把普通按键（比如 T）注册成全局热键，导致整台机器打字异常。
    /// </summary>
    public static bool TryFromKey(Key key, ModifierKeys modifiers, out HotkeyDefinition? definition, out string error)
    {
        definition = null;
        error = string.Empty;

        // 只按下修饰键时无法组成完整组合
        if (key is Key.None or Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt
            or Key.LeftShift or Key.RightShift or Key.LWin or Key.RWin or Key.System)
        {
            error = "请再按一个普通按键（例如 T、F8、空格）";
            return false;
        }

        int vk = KeyInterop.VirtualKeyFromKey(key);
        if (vk <= 0)
        {
            error = "这个按键不支持注册为全局热键";
            return false;
        }

        uint mods = 0;
        if (modifiers.HasFlag(ModifierKeys.Control)) mods |= NativeMethods.MOD_CONTROL;
        if (modifiers.HasFlag(ModifierKeys.Alt)) mods |= NativeMethods.MOD_ALT;
        if (modifiers.HasFlag(ModifierKeys.Shift)) mods |= NativeMethods.MOD_SHIFT;
        if (modifiers.HasFlag(ModifierKeys.Windows)) mods |= NativeMethods.MOD_WIN;

        if (mods == 0)
        {
            error = "请至少按住 Ctrl / Alt / Shift / Win 中的一个";
            return false;
        }

        definition = new HotkeyDefinition(mods, (uint)vk);
        return true;
    }

    /// <summary>解析 "Ctrl+Alt+T" 这样的字符串（用于读取设置文件）。</summary>
    public static bool TryParse(string? text, out HotkeyDefinition? definition)
    {
        definition = null;
        if (string.IsNullOrWhiteSpace(text)) return false;

        uint mods = 0;
        string? keyPart = null;

        foreach (string raw in text.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            switch (raw.ToLowerInvariant())
            {
                case "ctrl":
                case "control":
                    mods |= NativeMethods.MOD_CONTROL;
                    break;
                case "alt":
                    mods |= NativeMethods.MOD_ALT;
                    break;
                case "shift":
                    mods |= NativeMethods.MOD_SHIFT;
                    break;
                case "win":
                case "windows":
                    mods |= NativeMethods.MOD_WIN;
                    break;
                default:
                    keyPart = raw; // 最后出现的非修饰键即主键
                    break;
            }
        }

        if (keyPart == null || mods == 0) return false;
        if (!TryParseKey(keyPart, out uint vk)) return false;

        definition = new HotkeyDefinition(mods, vk);
        return true;
    }

    private static bool TryParseKey(string text, out uint virtualKey)
    {
        virtualKey = 0;
        string s = text.Trim();
        if (s.Length == 0) return false;

        // 单个字符：字母或数字
        if (s.Length == 1)
        {
            char c = char.ToUpperInvariant(s[0]);
            if ((c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9'))
            {
                virtualKey = c;
                return true;
            }
        }

        // F1 - F24
        if (s.Length >= 2 && (s[0] == 'F' || s[0] == 'f') && int.TryParse(s.AsSpan(1), out int fn)
            && fn >= 1 && fn <= 24)
        {
            virtualKey = (uint)(0x6F + fn);
            return true;
        }

        // 其它交给 WPF 的 Key 枚举（Space、OemComma、PageUp ...）
        if (Enum.TryParse<Key>(s, ignoreCase: true, out Key key))
        {
            int vk = KeyInterop.VirtualKeyFromKey(key);
            if (vk > 0)
            {
                virtualKey = (uint)vk;
                return true;
            }
        }

        return false;
    }
}
