using System.Collections.Generic;
using Seer.Models;

namespace Seer.Services;

/// <summary>
/// Marks the processes that just arrived in the top list or climbed it.
///
/// Two rules keep the marks meaningful rather than decorative:
///
/// A climb must be backed by a real increase in CPU (<see cref="MinimumClimb"/>
/// points). The tail of the list swaps places on rounding noise almost every
/// poll — three processes sitting at 2% will reorder endlessly — and marking
/// that would train the eye to ignore the arrows entirely.
///
/// A mark is held for <see cref="HoldPolls"/> polls. At a one-second cadence a
/// single-frame arrow is gone before it can be read.
///
/// Everything is keyed by process id, not name: a machine routinely runs a
/// dozen processes called "chrome" or "brave", and keying by name would let
/// them overwrite each other's history and mark the wrong rows.
///
/// Pure bookkeeping over successive lists, so the rules can be tested.
/// </summary>
public sealed class ProcessRankTracker
{
    /// <summary>CPU points a process must gain before a climb is marked.</summary>
    private const double MinimumClimb = 1.5;

    /// <summary>Polls a mark stays visible.</summary>
    private const int HoldPolls = 3;

    private readonly Dictionary<int, int> _previousRank = new();
    private readonly Dictionary<int, double> _previousCpu = new();
    private readonly Dictionary<int, (RankMark Mark, int Until)> _marks = new();
    private int _poll;
    private bool _seenAnything;

    /// <summary>
    /// Returns the same processes with their rank marks filled in. The first
    /// call marks nothing: everything is new when there is no previous list.
    /// </summary>
    public IReadOnlyList<ProcessMetrics> Apply(IReadOnlyList<ProcessMetrics> top)
    {
        _poll++;

        var marked = new List<ProcessMetrics>(top.Count);

        for (var rank = 0; rank < top.Count; rank++)
        {
            var process = top[rank];

            if (_seenAnything)
            {
                var known = _previousRank.TryGetValue(process.Pid, out var previousRank);
                var previousCpu = _previousCpu.TryGetValue(process.Pid, out var cpu) ? cpu : process.CpuPercent;
                var delta = process.CpuPercent - previousCpu;

                if (!known)
                    _marks[process.Pid] = (RankMark.New, _poll + HoldPolls);
                else if (rank < previousRank && delta >= MinimumClimb)
                    _marks[process.Pid] = (RankMark.Up, _poll + HoldPolls);
                else if (rank > previousRank && delta <= -MinimumClimb)
                    _marks[process.Pid] = (RankMark.Down, _poll + HoldPolls);
            }

            var mark = _marks.TryGetValue(process.Pid, out var held) && held.Until > _poll
                ? held.Mark
                : RankMark.None;

            marked.Add(process with { RankMark = mark });
        }

        Remember(top);
        PruneExpiredMarks();

        return marked;
    }

    private void Remember(IReadOnlyList<ProcessMetrics> top)
    {
        _previousRank.Clear();
        _previousCpu.Clear();

        for (var rank = 0; rank < top.Count; rank++)
        {
            _previousRank[top[rank].Pid] = rank;
            _previousCpu[top[rank].Pid] = top[rank].CpuPercent;
        }

        if (top.Count > 0)
            _seenAnything = true;
    }

    private void PruneExpiredMarks()
    {
        if (_marks.Count == 0)
            return;

        var expired = new List<int>();
        foreach (var (pid, held) in _marks)
        {
            if (held.Until <= _poll)
                expired.Add(pid);
        }

        foreach (var pid in expired)
            _marks.Remove(pid);
    }
}
