namespace Seer.Models;

public record ProcessMetrics
{
    public int Pid { get; init; }
    public string Name { get; init; } = string.Empty;
    public double CpuPercent { get; init; }
    public double WorkingSetMb { get; init; }

    public string CpuBar
    {
        get
        {
            // Calculate number of bars out of 10
            int bars = (int)(CpuPercent / 10.0);
            if (bars > 10) bars = 10;
            if (bars < 0) bars = 0;
            return new string('|', bars).PadRight(10, ' ');
        }
    }
}
