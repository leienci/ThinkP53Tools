using System.Globalization;
using System.Text;

namespace PowerPlanSwitcher;

public static class IniSettings
{
    public const string DefaultHotkey = "Win+F4";
    private const string DefaultIniPath = "setting.ini";

    public static Settings Load(string? filePath = null)
    {
        var path = string.IsNullOrWhiteSpace(filePath)
            ? Path.Combine(AppContext.BaseDirectory, DefaultIniPath)
            : filePath;
        if (!File.Exists(path))
        {
            CreateDefault(path);
            return new Settings();
        }

        try
        {
            return ParseIni(File.ReadAllText(path, Encoding.UTF8), path);
        }
        catch
        {
            return new Settings { FilePath = path, LoadedWithErrors = true };
        }
    }

    private static Settings ParseIni(string content, string filePath)
    {
        var dict = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
        var current = string.Empty;

        foreach (var rawLine in content.Split('\n'))
        {
            var line = rawLine.Trim();
            if (string.IsNullOrEmpty(line) || line.StartsWith(';') || line.StartsWith('#'))
                continue;

            if (line.StartsWith('['))
            {
                var end = line.IndexOf(']');
                if (end > 0)
                    current = line.Substring(1, end - 1).Trim().ToLowerInvariant();
                if (!dict.ContainsKey(current))
                    dict[current] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                continue;
            }

            if (string.IsNullOrEmpty(current))
                continue;

            var idx = line.IndexOf('=');
            if (idx <= 0)
                continue;

            var key = line.Substring(0, idx).Trim();
            var val = line.Substring(idx + 1).Trim();
            dict[current][key] = val;
        }

        var s = new Settings { FilePath = filePath };

        TryGet(dict, "hotkey", "enabled", out var hkEnabled);
        if (bool.TryParse(hkEnabled, out var b))
            s.Hotkey.Enabled = b;

        TryGet(dict, "hotkey", "key", out var keyStr);
        if (!string.IsNullOrEmpty(keyStr))
            s.Hotkey.Key = keyStr;

        TryGet(dict, "osd", "enabled", out var osdEnabled);
        if (bool.TryParse(osdEnabled, out var ob))
            s.Osd.Enabled = ob;

        TryGet(dict, "osd", "durationms", out var durStr);
        if (int.TryParse(durStr, out var dur) && dur > 0)
            s.Osd.DurationMs = dur;

        TryGet(dict, "osd", "opacity", out var opStr);
        if (double.TryParse(opStr, NumberStyles.Float, CultureInfo.InvariantCulture, out var op) && op >= 0.0 && op <= 1.0)
            s.Osd.Opacity = op;

        TryGet(dict, "behavior", "refreshintervalms", out var riStr);
        if (int.TryParse(riStr, out var ri) && ri > 0)
            s.Behavior.RefreshIntervalMs = ri;

        TryGet(dict, "behavior", "cancelonescape", out var ceStr);
        if (bool.TryParse(ceStr, out var ce))
            s.Behavior.CancelOnEscape = ce;

        foreach (var kvp in dict.GetValueOrDefault("displaynames", new Dictionary<string, string>()))
        {
            if (!string.IsNullOrEmpty(kvp.Value))
                s.DisplayNames[kvp.Key] = kvp.Value;
        }

        return s;
    }

    private static void TryGet(Dictionary<string, Dictionary<string, string>> dict, string section, string key, out string? value)
    {
        value = dict.GetValueOrDefault(section.ToLowerInvariant())?.GetValueOrDefault(key.ToLowerInvariant());
    }

    private static void CreateDefault(string path)
    {
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            Directory.CreateDirectory(dir);

        File.WriteAllText(path, DefaultContent, Encoding.UTF8);
    }

    private const string DefaultContent =
        """
        [Hotkey]
        Enabled=true
        Key=Win+F4

        [OSD]
        Enabled=true
        DurationMs=1500
        Opacity=0.82

        [Behavior]
        RefreshIntervalMs=1000
        CancelOnEscape=true

        [DisplayNames]
        Balanced=平衡
        High performance=高性能
        Power saver=节能
        Ultimate Performance=卓越性能
        """;

}

public sealed class Settings
{
    public string FilePath { get; init; } = string.Empty;
    public bool LoadedWithErrors { get; init; }
    public HotkeySettings Hotkey { get; init; } = new();
    public OsdSettings Osd { get; init; } = new();
    public BehaviorSettings Behavior { get; init; } = new();
    public Dictionary<string, string> DisplayNames { get; init; } = new(StringComparer.OrdinalIgnoreCase);
}

public sealed class HotkeySettings
{
    public bool Enabled { get; set; } = true;
    public string Key { get; set; } = IniSettings.DefaultHotkey;
}

public sealed class OsdSettings
{
    public bool Enabled { get; set; } = true;
    public int DurationMs { get; set; } = 1500;
    public double Opacity { get; set; } = 0.82;
}

public sealed class BehaviorSettings
{
    public int RefreshIntervalMs { get; set; } = 1000;
    public bool CancelOnEscape { get; set; } = true;
}
