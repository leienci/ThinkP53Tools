using System.Diagnostics;

namespace PowerPlanSwitcher;

public sealed class PowerPlanService
{
    private readonly Settings _settings;
    private const int ProcessTimeoutMs = 5000;

    public PowerPlanService(Settings settings)
    {
        _settings = settings;
    }

    public Guid? GetActiveGuid()
    {
        var output = RunPowerCfg("/getactivescheme").Trim();
        if (string.IsNullOrEmpty(output))
            return null;

        var span = output.AsSpan();
        var idx = span.IndexOf(":");
        if (idx < 0)
            return null;

        var guidPart = span[(idx + 1)..].Trim();
        if (Guid.TryParse(guidPart, out var guid))
            return guid;

        return ExtractGuid(span);
    }

    private static Guid? ExtractGuid(ReadOnlySpan<char> s)
    {
        var start = -1;
        for (int i = 0; i < s.Length; i++)
        {
            if (s[i] == ':' && i + 1 < s.Length)
            {
                start = i + 1;
                break;
            }
            if (s[i] == '-' && start < 0)
            {
                var candidate = s[i..];
                var end = candidate.IndexOf(')');
                if (end > 0)
                    candidate = candidate[..end];
                if (Guid.TryParse(candidate, out var g))
                    return g;
            }
        }

        if (start >= 0)
        {
            var candidate = s[start..];
            var end = candidate.IndexOfAny(" \t\r\n");
            if (end > 0)
                candidate = candidate[..end];
            if (Guid.TryParse(candidate, out var g))
                return g;
        }

        return null;
    }

    public List<PowerPlan> EnumeratePlans()
    {
        var output = RunPowerCfg("/list");
        var plans = new List<PowerPlan>();
        var active = GetActiveGuid();
        var idx = 0;

        foreach (var line in output.Split('\n'))
        {
            var trimmed = line.Trim();
            if (!trimmed.StartsWith("Power Scheme GUID:", StringComparison.OrdinalIgnoreCase))
                continue;

            var guidStr = ExtractGuidFromLine(trimmed);
            if (string.IsNullOrEmpty(guidStr) || !Guid.TryParse(guidStr, out var guid))
                continue;

            var sysName = ExtractParenName(trimmed);

            plans.Add(new PowerPlan
            {
                Guid = guid,
                SystemName = sysName,
                DisplayName = ResolveDisplayName(sysName, guid),
                IsActive = active.HasValue && guid == active.Value,
                Index = idx++
            });
        }

        if (plans.Count == 1 && !plans[0].IsActive)
        {
            plans[0] = new PowerPlan
            {
                Guid = plans[0].Guid,
                SystemName = plans[0].SystemName,
                DisplayName = plans[0].DisplayName,
                IsActive = true,
                Index = plans[0].Index
            };
        }

        return plans;
    }

    private string ResolveDisplayName(string sysName, Guid guid)
    {
        var key = sysName.Replace(" ", "");
        if (!string.IsNullOrEmpty(key) && _settings.DisplayNames.TryGetValue(key, out var mapped))
            return mapped;
        if (!string.IsNullOrEmpty(sysName) && _settings.DisplayNames.TryGetValue(sysName, out var mapped2))
            return mapped2;
        return sysName;
    }

    private static string? ExtractGuidFromLine(string line)
    {
        var m = System.Text.RegularExpressions.Regex.Match(line,
            @"([0-9A-Fa-f]{8}\-[0-9A-Fa-f]{4}\-[0-9A-Fa-f]{4}\-[0-9A-Fa-f]{4}\-[0-9A-Fa-f]{12})");
        return m.Success ? m.Groups[1].Value : null;
    }

    private static string ExtractParenName(string line)
    {
        var start = line.IndexOf('(');
        if (start < 0)
            return string.Empty;
        var end = line.IndexOf(')', start + 1);
        if (end < 0)
            return string.Empty;
        var inner = line.Substring(start + 1, end - start - 1).Trim();
        return inner;
    }

    public bool SetActive(Guid guid)
    {
        var output = RunPowerCfg($"/setactive {guid}");
        return string.IsNullOrEmpty(output) && GetActiveGuid() == guid;
    }

    public void Refresh()
    {
    }

    private string RunPowerCfg(string args)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "powercfg.exe",
                Arguments = args,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            };

            using var proc = Process.Start(psi);
            if (proc is null)
                return string.Empty;

            var outputTask = proc.StandardOutput.ReadToEndAsync();
            var errorTask = proc.StandardError.ReadToEndAsync();

            if (!proc.WaitForExit(ProcessTimeoutMs))
            {
                proc.Kill(true);
                return errorTask.GetAwaiter().GetResult();
            }

            var output = outputTask.GetAwaiter().GetResult();
            var error = errorTask.GetAwaiter().GetResult();

            if (proc.ExitCode != 0)
                return error;

            return output;
        }
        catch
        {
            return string.Empty;
        }
    }

    public static string GetPlanIconKey(Guid guid)
    {
        return guid.ToString("D").ToLowerInvariant();
    }
}
