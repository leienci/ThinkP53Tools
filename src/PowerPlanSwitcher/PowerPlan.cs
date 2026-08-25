namespace PowerPlanSwitcher;

public sealed class PowerPlan
{
    public Guid Guid { get; init; }
    public string SystemName { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public bool IsActive { get; init; }
    public int Index { get; init; }

    public override string ToString() => DisplayName;
}
