using System.Runtime.InteropServices;

namespace PowerPlanSwitcher;

public sealed class PowerPlanService
{
    private readonly Settings _settings;

    // Win32 error codes
    private const uint ERROR_SUCCESS = 0;
    private const uint ERROR_NO_MORE_ITEMS = 259;
    private const uint ERROR_NOT_FOUND = 1168;
    private const uint ACCESS_SCHEME = 16;

    // Well-known power scheme GUIDs (match PlanIconLibrary mapping)
    private static readonly Dictionary<Guid, string> WellKnownSystemNames = new()
    {
        { new Guid("381b4222-f694-41f0-9685-ff5bb260df2e"), "Balanced" },
        { new Guid("8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c"), "High performance" },
        { new Guid("a1841308-3541-4fab-bc81-f71556f20b4a"), "Power saver" },
        { new Guid("e9a42b02-d5df-448d-aa00-03f14749eb61"), "Ultimate Performance" }
    };

    public PowerPlanService(Settings settings)
    {
        _settings = settings;
    }

    public Guid? GetActiveGuid()
    {
        var hr = PowerGetActiveScheme(IntPtr.Zero, out var ptr);
        if (hr != ERROR_SUCCESS || ptr == IntPtr.Zero)
            return null;

        try
        {
            var guid = Marshal.PtrToStructure<Guid>(ptr);
            return guid;
        }
        finally
        {
            LocalFree(ptr);
        }
    }

    public List<PowerPlan> EnumeratePlans()
    {
        var plans = new List<PowerPlan>();
        var active = GetActiveGuid();
        uint index = 0;
        var guidBuffer = new byte[Marshal.SizeOf<Guid>()];
        var handle = GCHandle.Alloc(guidBuffer, GCHandleType.Pinned);
        try
        {
            var bufferPtr = handle.AddrOfPinnedObject();

            while (true)
            {
                uint bufferSize = (uint)guidBuffer.Length;
                var hr = PowerEnumerate(IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, ACCESS_SCHEME, index, bufferPtr, ref bufferSize);
                if (hr == ERROR_NO_MORE_ITEMS || hr == ERROR_NOT_FOUND)
                    break;
                if (hr != ERROR_SUCCESS)
                {
                    index++;
                    continue;
                }

                var guid = Marshal.PtrToStructure<Guid>(bufferPtr);
                string sysName;
                if (!WellKnownSystemNames.TryGetValue(guid, out sysName))
                {
                    // For custom/user plans, use the localized friendly name as SystemName
                    // so PlanIconLibrary can match it against known display names (EN/CN)
                    sysName = ReadFriendlyName(guid);
                    if (string.IsNullOrEmpty(sysName))
                        sysName = guid.ToString("D");
                }

                var displayName = ResolveDisplayName(guid, sysName);
                plans.Add(new PowerPlan
                {
                    Guid = guid,
                    SystemName = sysName,
                    DisplayName = displayName,
                    IsActive = active.HasValue && guid == active.Value,
                    Index = plans.Count
                });

                index++;
            }
        }
        finally
        {
            handle.Free();
        }

        // Fallback: if only one plan and not marked active, mark it
        if (plans.Count == 1 && !plans[0].IsActive)
        {
            plans[0] = new PowerPlan
            {
                Guid = plans[0].Guid,
                SystemName = plans[0].SystemName,
                DisplayName = plans[0].DisplayName,
                IsActive = true,
                Index = 0
            };
        }

        return plans;
    }

    private string ResolveDisplayName(Guid guid, string sysName)
    {
        // 1. Try settings.ini mapping by well-known system name
        if (WellKnownSystemNames.TryGetValue(guid, out var knownName))
        {
            var key = knownName.Replace(" ", "");
            if (!string.IsNullOrEmpty(key) && _settings.DisplayNames.TryGetValue(key, out var mapped))
                return mapped;
            if (!string.IsNullOrEmpty(knownName) && _settings.DisplayNames.TryGetValue(knownName, out var mapped2))
                return mapped2;
        }

        // 2. Try settings.ini by system name (fallback)
        if (!string.IsNullOrEmpty(sysName))
        {
            var key = sysName.Replace(" ", "");
            if (!string.IsNullOrEmpty(key) && _settings.DisplayNames.TryGetValue(key, out var mapped))
                return mapped;
            if (_settings.DisplayNames.TryGetValue(sysName, out var mapped2))
                return mapped2;
        }

        // 3. Read localized friendly name from the registry via PowerReadFriendlyName
        var friendly = ReadFriendlyName(guid);
        if (!string.IsNullOrEmpty(friendly))
            return friendly;

        // 4. Fallback to system name
        return sysName;
    }

    private string ReadFriendlyName(Guid guid)
    {
        var guidStruct = guid;
        // First call to get required buffer size
        uint bufferSize = 0;
        var hr = PowerReadFriendlyName(IntPtr.Zero, ref guidStruct, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, ref bufferSize);
        if (hr != ERROR_SUCCESS && hr != ERROR_NOT_FOUND)
            return string.Empty;

        if (bufferSize == 0)
            return string.Empty;

        var buffer = Marshal.AllocHGlobal((int)bufferSize);
        try
        {
            hr = PowerReadFriendlyName(IntPtr.Zero, ref guidStruct, IntPtr.Zero, IntPtr.Zero, buffer, ref bufferSize);
            if (hr != ERROR_SUCCESS)
                return string.Empty;

            // Convert from UTF-16 (Windows registry strings)
            var name = Marshal.PtrToStringUni(buffer);
            return name?.TrimEnd('\0') ?? string.Empty;
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    public bool SetActive(Guid guid)
    {
        var guidStruct = guid;
        var hr = PowerSetActiveScheme(IntPtr.Zero, ref guidStruct);
        if (hr != ERROR_SUCCESS)
            return false;

        return GetActiveGuid() == guid;
    }

    public void Refresh()
    {
    }

    public static string GetPlanIconKey(Guid guid)
    {
        return guid.ToString("D").ToLowerInvariant();
    }

    // P/Invoke signatures
    [DllImport("powrprof.dll", SetLastError = true)]
    private static extern uint PowerGetActiveScheme(IntPtr userRootPowerKey, out IntPtr activePolicyGuid);

    [DllImport("powrprof.dll", SetLastError = true)]
    private static extern uint PowerSetActiveScheme(IntPtr userRootPowerKey, ref Guid schemeGuid);

    [DllImport("powrprof.dll", SetLastError = true)]
    private static extern uint PowerEnumerate(
        IntPtr rootPowerKey,
        IntPtr schemeGuid,
        IntPtr subGroupOfPowerSettingsGuid,
        uint accessFlags,
        uint index,
        IntPtr buffer,
        ref uint bufferSize);

    [DllImport("powrprof.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern uint PowerReadFriendlyName(
        IntPtr rootPowerKey,
        ref Guid schemeGuid,
        IntPtr subGroupOfPowerSettingsGuid,
        IntPtr powerSettingGuid,
        IntPtr buffer,
        ref uint bufferSize);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr LocalFree(IntPtr hMem);
}
