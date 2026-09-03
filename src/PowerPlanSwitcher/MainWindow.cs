using System.Runtime.InteropServices;

namespace PowerPlanSwitcher;

public sealed class MainWindow : Form
{
    private const int WM_HOTKEY = 0x0312;
    private const int WM_SETTINGCHANGE = 0x001A;
    private const int HotkeyId = 0x4250;
    private readonly KeyboardHook _keyboardHook;

    public event EventHandler? HotkeyPressed;
    public event EventHandler? WinKeyReleased;
    public event EventHandler? EscapePressed;
    public event EventHandler? SystemThemeChanged;

    public MainWindow()
    {
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        ShowIcon = false;
        WindowState = FormWindowState.Minimized;
        Opacity = 0;
        Size = new Size(1, 1);
        _keyboardHook = new KeyboardHook();
        _keyboardHook.WinKeyReleased += (_, _) => WinKeyReleased?.Invoke(this, EventArgs.Empty);
        _keyboardHook.EscapePressed += (_, _) => EscapePressed?.Invoke(this, EventArgs.Empty);
    }

    public bool RegisterHotkey(string key)
    {
        if (!IsHandleCreated)
            _ = Handle;
        return GlobalHotkey.Register(Handle, HotkeyId, key);
    }

    public void UnregisterHotkey()
    {
        if (IsHandleCreated)
            GlobalHotkey.Unregister(Handle, HotkeyId);
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        _keyboardHook.Install();
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == WM_HOTKEY && m.WParam.ToInt32() == HotkeyId)
            HotkeyPressed?.Invoke(this, EventArgs.Empty);
        else if (m.Msg == WM_SETTINGCHANGE)
        {
            var name = m.LParam == IntPtr.Zero ? null : Marshal.PtrToStringUni(m.LParam);
            if (name != null && (name.StartsWith("ImmersiveColorSet", StringComparison.OrdinalIgnoreCase)
                              || name.StartsWith("ThemeChanged", StringComparison.OrdinalIgnoreCase)))
            {
                SystemThemeChanged?.Invoke(this, EventArgs.Empty);
            }
        }
        base.WndProc(ref m);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            UnregisterHotkey();
            _keyboardHook.Dispose();
        }
        base.Dispose(disposing);
    }
}

internal sealed class KeyboardHook : IDisposable
{
    private const int WH_KEYBOARD_LL = 13;
    private const int WM_KEYUP = 0x0101;
    private const int WM_SYSKEYUP = 0x0105;
    private const int VK_LWIN = 0x5B;
    private const int VK_RWIN = 0x5C;
    private const int VK_ESCAPE = 0x1B;

    private LowLevelKeyboardProc? _proc;
    private IntPtr _hookId;

    public event EventHandler? WinKeyReleased;
    public event EventHandler? EscapePressed;

    public void Install()
    {
        if (_hookId != IntPtr.Zero)
            return;
        _proc = HookCallback;
        _hookId = SetWindowsHookEx(WH_KEYBOARD_LL, _proc, GetModuleHandle(null), 0);
    }

    public void Dispose()
    {
        if (_hookId != IntPtr.Zero)
        {
            UnhookWindowsHookEx(_hookId);
            _hookId = IntPtr.Zero;
        }
        GC.SuppressFinalize(this);
    }

    private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0 && (wParam.ToInt32() == WM_KEYUP || wParam.ToInt32() == WM_SYSKEYUP))
        {
            var info = Marshal.PtrToStructure<KbdLlHookStruct>(lParam);
            if ((info.flags & 0x10) == 0)
            {
                if (info.vkCode == VK_LWIN || info.vkCode == VK_RWIN)
                    WinKeyReleased?.Invoke(this, EventArgs.Empty);
                else if (info.vkCode == VK_ESCAPE)
                    EscapePressed?.Invoke(this, EventArgs.Empty);
            }
        }
        return CallNextHookEx(_hookId, nCode, wParam, lParam);
    }

    private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential)]
    private struct KbdLlHookStruct
    {
        public uint vkCode;
        public uint scanCode;
        public uint flags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll")]
    private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr GetModuleHandle(string? lpModuleName);
}
