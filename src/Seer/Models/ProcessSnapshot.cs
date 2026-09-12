using System;
using System.Collections.Generic;

namespace Seer.Models;

/// <summary>
/// One poll's worth of process information: the few processes worth showing,
/// plus the totals that put them in context.
///
/// The totals are free — the monitor already enumerates every process to find
/// the top few, and used to throw the count away.
/// </summary>
/// <param name="Top">The busiest processes, already sorted.</param>
/// <param name="TotalProcesses">Every process seen this poll, not just the listed ones.</param>
/// <param name="TotalThreads">Threads across the whole machine, or 0 when the counter is unavailable.</param>
public record ProcessSnapshot(
    IReadOnlyList<ProcessMetrics> Top,
    int TotalProcesses,
    int TotalThreads)
{
    public static readonly ProcessSnapshot Empty =
        new(Array.Empty<ProcessMetrics>(), 0, 0);
}
