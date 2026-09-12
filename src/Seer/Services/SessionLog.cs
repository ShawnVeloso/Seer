using System;
using System.Collections.ObjectModel;
using Seer.Models;

namespace Seer.Services;

/// <summary>
/// Everything notable that happened this session, newest first.
///
/// Session-only by design, like the alert log it replaces: this is a record
/// of what the machine did while you were watching, not a history to keep.
///
/// The collection is mutated in place rather than reassigned — the panel
/// binds to it once, and swapping the list every poll would rebuild every row
/// on screen for no reason.
/// </summary>
public sealed class SessionLog
{
    /// <summary>Entries kept before the oldest is dropped.</summary>
    private const int MaxEntries = 50;

    public ObservableCollection<EventEntry> Entries { get; } = new();

    /// <summary>Most recent entry, or null when nothing has happened yet.</summary>
    public EventEntry? Latest => Entries.Count > 0 ? Entries[0] : null;

    public void Add(string source, string text, AlertSeverity severity = AlertSeverity.Nominal)
        => Add(new EventEntry(DateTime.Now, source, text, severity));

    /// <summary>Records a threshold crossing in the same log as everything else.</summary>
    public void AddAlert(AlertEvent alert)
        => Add(new EventEntry(
            alert.Timestamp,
            alert.MetricName,
            $"{alert.FormattedValue} -> {alert.Severity.ToString().ToUpperInvariant()}",
            alert.Severity));

    private void Add(EventEntry entry)
    {
        Entries.Insert(0, entry);

        while (Entries.Count > MaxEntries)
            Entries.RemoveAt(Entries.Count - 1);
    }
}
