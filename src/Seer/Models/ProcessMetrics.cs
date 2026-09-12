namespace Seer.Models;

/// <summary>How a process moved in the top list since the previous poll.</summary>
public enum RankMark
{
    None,

    /// <summary>Climbed, backed by a real increase in CPU.</summary>
    Up,

    /// <summary>Dropped.</summary>
    Down,

    /// <summary>Wasn't in the list at all last poll.</summary>
    New
}

public record ProcessMetrics
{
    public int Pid { get; init; }
    public string Name { get; init; } = string.Empty;
    public double CpuPercent { get; init; }
    public double WorkingSetMb { get; init; }

    /// <summary>
    /// Filled in by <c>ProcessRankTracker</c> rather than by the monitor: it
    /// is a fact about two consecutive polls, not about the process.
    /// </summary>
    public RankMark RankMark { get; init; }
}
