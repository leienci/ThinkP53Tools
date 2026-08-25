using System.Runtime.InteropServices;

namespace PowerPlanSwitcher;

public static class GlobalHotkey
{
    private const int MOD_WIN = 0x0008;
    private const int MOD_ALT = 0x0001;
    private const int MOD_CONTROL = 0x0002;
    private const int MOD_SHIFT = 0x0004;
    private const int WM_HOTKEY = 0x0312;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    public static bool Register(IntPtr hWnd, int id, string key)
    {
        if (!ParseKey(key, out var mod, out var vk))
            return false;

        return RegisterHotKey(hWnd, id, mod, vk);
    }

    public static bool Unregister(IntPtr hWnd, int id)
    {
        return UnregisterHotKey(hWnd, id);
    }

    public static bool ParseKey(string key, out uint modifiers, out uint virtualKey)
    {
        modifiers = 0;
        virtualKey = 0;

        var parts = key.Split('+');
        if (parts.Length == 0)
            return false;

        foreach (var part in parts)
        {
            var p = part.Trim().ToLowerInvariant();
            if (p == "win")
                modifiers |= MOD_WIN;
            else if (p == "alt")
                modifiers |= MOD_ALT;
            else if (p == "ctrl" || p == "control")
                modifiers |= MOD_CONTROL;
            else if (p == "shift")
                modifiers |= MOD_SHIFT;
            else if (TryGetVirtualKey(p, out var vk))
                virtualKey = vk;
            else
                return false;
        }

        return virtualKey != 0;
    }

    private static bool TryGetVirtualKey(string name, out uint vk)
    {
        vk = name.ToUpperInvariant() switch
        {
            "F1" => 0x70,
            "F2" => 0x71,
            "F3" => 0x72,
            "F4" => 0x73,
            "F5" => 0x74,
            "F6" => 0x75,
            "F7" => 0x76,
            "F8" => 0x77,
            "F9" => 0x78,
            "F10" => 0x79,
            "F11" => 0x7A,
            "F12" => 0x7B,
            _ => 0
        };
        return vk != 0;
    }

    public static bool IsWinKey(uint modifiers) => (modifiers & MOD_WIN) == MOD_WIN;
}
