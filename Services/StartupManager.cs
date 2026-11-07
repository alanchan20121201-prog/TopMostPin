using System.Windows.Forms;
using Microsoft.Win32;

namespace TopMostPin.Services;

/// <summary>
/// 开机自启管理：写 HKCU\Software\Microsoft\Windows\CurrentVersion\Run。
/// 用当前用户注册表，不需要管理员权限。
/// </summary>
public static class StartupManager
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "TopMostPin";

    /// <summary>当前是否已开启开机自启（同时校验记录的路径是否就是当前 exe）。</summary>
    public static bool IsEnabled
    {
        get
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: false);
                var value = key?.GetValue(ValueName) as string;
                if (string.IsNullOrEmpty(value)) return false;

                // 如果用户移动了 exe，旧路径已经失效，视为未开启（避免误导）
                string exePath = GetExecutablePath();
                return value.Contains(exePath, StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }
    }

    /// <summary>开启 / 关闭开机自启。</summary>
    public static bool SetEnabled(bool enabled)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true)
                            ?? Registry.CurrentUser.CreateSubKey(RunKeyPath);

            if (key == null) return false;

            if (enabled)
            {
                // 路径带引号，防止路径里有空格时启动失败
                key.SetValue(ValueName, $"\"{GetExecutablePath()}\"");
            }
            else if (key.GetValue(ValueName) != null)
            {
                key.DeleteValue(ValueName, throwOnMissingValue: false);
            }

            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// 当前 exe 的完整路径。
    /// 单文件发布下 Environment.ProcessPath 就是 exe 自身路径（不是解压出来的临时目录）。
    /// </summary>
    private static string GetExecutablePath()
    {
        return Environment.ProcessPath ?? Application.ExecutablePath;
    }
}
