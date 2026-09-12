using Seer.Models;

namespace Seer.Services;

/// <summary>
/// Keeps a severity on screen for a few polls after it clears.
///
/// A reading sitting on its threshold crosses it repeatedly — 84.9, 85.1,
/// 84.8 — and a panel edge wired straight to that flickers once a second,
/// which reads as a fault in the app rather than a state of the machine.
/// Escalation is immediate; only the return to calm waits.
///
/// This is presentation only. The alert log still records every escalation
/// exactly as it happens, because that is a record rather than a display.
/// </summary>
public sealed class SeverityHold
{
    private readonly int _pollsBeforeClearing;
    private AlertSeverity _held = AlertSeverity.Nominal;
    private int _lowerPolls;

    public SeverityHold(int pollsBeforeClearing = 3)
        => _pollsBeforeClearing = pollsBeforeClearing;

    /// <summary>The severity to show.</summary>
    public AlertSeverity Held => _held;

    /// <summary>
    /// Feeds the current severity and returns the one to display. Rising is
    /// instant; falling takes <c>pollsBeforeClearing</c> consecutive calmer
    /// polls, so a value hovering on its threshold holds steady.
    /// </summary>
    public AlertSeverity Push(AlertSeverity current)
    {
        if (current > _held)
        {
            _held = current;
            _lowerPolls = 0;
        }
        else if (current < _held)
        {
            if (++_lowerPolls >= _pollsBeforeClearing)
            {
                _held = current;
                _lowerPolls = 0;
            }
        }
        else
        {
            _lowerPolls = 0;
        }

        return _held;
    }
}
