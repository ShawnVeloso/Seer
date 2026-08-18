using System;

namespace Seer.Models;

public record AlertEvent(
    DateTime Timestamp,
    string MetricName,
    AlertSeverity Severity,
    float Value,
    string Unit
)
{
    public string FormattedTime => Timestamp.ToString("HH:mm:ss");
    public string FormattedValue => $"{Value:0.0}{Unit}";
}
