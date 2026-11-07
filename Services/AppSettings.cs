using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using TopMostPin.Hotkey;

namespace TopMostPin.Services;

/// <summary>
/// 简单的本地设置存储（不联网）。
/// 位置：%APPDATA%\TopMostPin\settings.json
/// </summary>
public sealed class AppSettings
{
    /// <summary>全局快捷键（锁定置顶，按一下切换），例如 "Ctrl+Alt+L"</summary>
    public string? Hotkey { get; set; }

    /// <summary>
    /// 上一版（临时置顶 + 锁定置顶双快捷键）的字段名。
    /// 只用于读取旧配置做迁移，保存时会被清空。
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? LockHotkey { get; set; }

    private static string SettingsDirectory =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "TopMostPin");

    private static string SettingsFile => Path.Combine(SettingsDirectory, "settings.json");

    /// <summary>读取设置；文件不存在或损坏时返回默认设置，不抛异常。</summary>
    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(SettingsFile))
            {
                string json = File.ReadAllText(SettingsFile);
                var loaded = JsonSerializer.Deserialize<AppSettings>(json);
                if (loaded != null)
                {
                    return loaded;
                }
            }
        }
        catch
        {
            // 设置文件坏了就当没有，用默认值，避免程序起不来
        }

        return new AppSettings();
    }

    /// <summary>写回设置文件，失败时静默忽略（不影响主功能）。</summary>
    public void Save()
    {
        try
        {
            LockHotkey = null; // 迁移完成后不再写出旧字段

            Directory.CreateDirectory(SettingsDirectory);
            string json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(SettingsFile, json);
        }
        catch
        {
            // 忽略
        }
    }

    /// <summary>
    /// 解析快捷键：优先新字段；旧配置里的 LockHotkey 沿用它；都没有则用默认 Ctrl+Alt+L。
    /// </summary>
    public HotkeyDefinition ResolveHotkey()
    {
        return Parse(Hotkey) ?? Parse(LockHotkey) ?? HotkeyDefinition.Default;
    }

    private static HotkeyDefinition? Parse(string? text)
    {
        return HotkeyDefinition.TryParse(text, out var definition) ? definition : null;
    }
}
