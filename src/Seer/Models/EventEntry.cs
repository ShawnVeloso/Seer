using System;

namespace Seer.Models;

/// <summary>
/// One line in the session event log.
///
/// Wider than an alert: threshold crossings are only some of what happens to
/// a machine being watched. Starting a ping, relaunching elevated, saving
/// settings and a process suddenly taking a quarter of the CPU are all things
/// that explain a change in the numbers, and all of them used to leave no
/// trace at all.
/// </summary>
/// <param name="Timestamp">When it happened.</param>
/// <param name="Source">What it concerns — "CPU Temp", "PING", "SENSORS".</param>
/// <param name="Text">What happened, already formatted for reading.</param>
/// <param name="Severity">Nominal for ordinary events; the rest carry a badge.</param>
public record EventEntry(
    DateTime Timestamp,
    string Source,
    string Text,
    AlertSeverity Severity)
{
    public string FormattedTime => Timestamp.ToString("HH:mm:ss");

    /// <summary>Whether this one deserves a severity badge beside it.</summary>
    public bool HasSeverity => Severity != AlertSeverity.Nominal;
}
