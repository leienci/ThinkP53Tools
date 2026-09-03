using System.Reflection;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace PowerPlanSwitcher;

public static class PlanIconLibrary
{
    private static readonly Dictionary<string, Icon> _iconCache = new();
    private static readonly Dictionary<string, Image> _imageCache = new();

    private static readonly Dictionary<string, string> _planNameToPngMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Balanced"] = "Balanced.png",
        ["平衡"] = "Balanced.png",
        ["High performance"] = "HighPerf.png",
        ["高性能"] = "HighPerf.png",
        ["Power saver"] = "PowerSaver.png",
        ["节能"] = "PowerSaver.png",
        ["Ultimate Performance"] = "UltimatePerf.png",
        ["卓越性能"] = "UltimatePerf.png",
    };

    public static Icon GetIcon(string planName, int size = 16)
        => GetIconByString(planName, size, IsLightTaskbar());

    public static Icon GetIcon(Guid guid, int size = 16)
    {
        if (guid == Guid.Empty)
            return GetIconByString("Balanced.png", size, IsLightTaskbar());
        var name = ResolveNameFromGuid(guid);
        return GetIconByString(name, size, IsLightTaskbar());
    }

    public static Image GetImage(string planName, int size = 16, bool black = false)
    {
        var cacheKey = $"{planName}_{(black ? "B" : "W")}_{size}";
        if (_imageCache.TryGetValue(cacheKey, out var cached))
            return cached;

        using var img = LoadPng(GetPngPath(planName, black), size);
        var bmp = new System.Drawing.Bitmap(size, size, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        using var g = System.Drawing.Graphics.FromImage(bmp);
        g.Clear(System.Drawing.Color.Transparent);
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
        g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
        g.DrawImage(img, new System.Drawing.Rectangle(0, 0, size, size));
        _imageCache[cacheKey] = bmp;
        return bmp;
    }

    public static Image GetImage(Guid guid, int size = 16)
    {
        var name = guid == Guid.Empty ? "Balanced.png" : ResolveNameFromGuid(guid);
        return GetImage(name, size);
    }

    public static void ClearCache()
    {
        foreach (var kv in _iconCache) kv.Value.Dispose();
        foreach (var kv in _imageCache) (kv.Value as IDisposable)?.Dispose();
        _iconCache.Clear();
        _imageCache.Clear();
    }

    private static Icon GetIconByString(string planName, int size, bool lightTaskbar)
    {
        var cacheKey = $"{planName}_{(lightTaskbar ? "L" : "D")}_{size}";
        if (_iconCache.TryGetValue(cacheKey, out var cached))
            return cached;

        using var img = LoadPng(GetPngPath(planName, lightTaskbar), size);
        var bmp = new System.Drawing.Bitmap(size, size, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        using var g = System.Drawing.Graphics.FromImage(bmp);
        g.Clear(System.Drawing.Color.Transparent);
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
        g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
        g.DrawImage(img, new System.Drawing.Rectangle(0, 0, size, size));
        var hicon = bmp.GetHicon();
        try
        {
            using var icon = Icon.FromHandle(hicon);
            _iconCache[cacheKey] = (Icon)icon.Clone();
        }
        finally
        {
            DestroyIcon(hicon);
        }
        return _iconCache[cacheKey];
    }

    private static string ResolveNameFromGuid(Guid guid)
    {
        return guid.ToString("D").ToLowerInvariant() switch
        {
            "381b4222-f694-41f0-9685-ff5bb260df2e" => "Balanced.png",
            "8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c" => "HighPerf.png",
            "a1841308-3541-4fab-bc81-f71556f20b4a" => "PowerSaver.png",
            "e9a42b02-d5df-448d-aa00-03f14749eb61" => "UltimatePerf.png",
            _ => "Balanced.png"
        };
    }

    private static string GetPngPath(string planName, bool lightTaskbar)
    {
        string png;
        if (planName.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
            png = planName;
        else if (_planNameToPngMap.TryGetValue(planName, out var mapped))
            png = mapped;
        else
            png = "Balanced.png";

        if (lightTaskbar)
            png = png.Replace(".png", ".black.png", StringComparison.OrdinalIgnoreCase);
        return png;
    }

    private static System.Drawing.Image LoadPng(string resourceName, int size)
    {
        var assembly = Assembly.GetExecutingAssembly();
        var resourcePath = assembly.GetManifestResourceNames()
            .FirstOrDefault(n => n.EndsWith(resourceName, StringComparison.OrdinalIgnoreCase));

        if (resourcePath == null)
            return SystemIcons.Application.ToBitmap();

        using var stream = assembly.GetManifestResourceStream(resourcePath);
        if (stream == null)
            return SystemIcons.Application.ToBitmap();

        using var source = System.Drawing.Image.FromStream(stream);
        return new System.Drawing.Bitmap(source);
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyIcon(IntPtr hIcon);

    private static bool IsLightTaskbar()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            if (key?.GetValue("SystemUsesLightTheme") is int v)
                return v == 1;
        }
        catch
        {
        }
        return false;
    }
}
