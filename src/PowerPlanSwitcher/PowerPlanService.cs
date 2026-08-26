using System.Diagnostics;

namespace PowerPlanSwitcher;

public sealed class PowerPlanService
{
    private readonly Settings _settings;
    private const int ProcessTimeoutMs = 5000;

    private static readonly System.Text.RegularExpressions.Regex GuidRegex = new(
        @"([0-9A-Fa-f]{8}\-[0-9A-Fa-f]{4}\-[0-9A-Fa-f]{4}\-[0-9A-Fa-f]{4}\-[0-9A-Fa-f]{12})",
        System.Text.RegularExpressions.RegexOptions.Compiled);

    private static readonly System.Text.RegularExpressions.Regex ParenNameRegex = new(
        @"\(([^)]*)\)",
        System.Text.RegularExpressions.RegexOptions.Compiled);

    public PowerPlanService(Settings settings)
    {
        _settings = settings;
    }

    public Guid? GetActiveGuid()
    {
        var output = RunPowerCfg("/getactivescheme");
        var match = GuidRegex.Match(output);
        return match.Success && Guid.TryParse(match.Value, out var guid) ? guid : null;
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

            var guidMatch = GuidRegex.Match(trimmed);
            if (!guidMatch.Success || !Guid.TryParse(guidMatch.Value, out var guid))
                continue;

            var sysName = ExtractParenName(trimmed);
            if (string.IsNullOrEmpty(sysName))
                continue;

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

    private static string ExtractParenName(string line)
    {
        var m = ParenNameRegex.Match(line);
        return m.Success ? m.Groups[1].Value.Trim() : string.Empty;
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
