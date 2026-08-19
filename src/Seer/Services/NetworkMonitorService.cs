using System;
using System.Net.NetworkInformation;
using Seer.Models;

namespace Seer.Services;

public sealed class NetworkMonitorService
{
    private long _lastBytesReceived;
    private long _lastBytesSent;
    private DateTime _lastTime;
    private bool _isFirstPoll = true;

    public NetworkMetrics GetMetrics()
    {
        long currentReceived = 0;
        long currentSent = 0;

        try
        {
            var interfaces = NetworkInterface.GetAllNetworkInterfaces();
            foreach (var ni in interfaces)
            {
                if (ni.OperationalStatus == OperationalStatus.Up && 
                    ni.NetworkInterfaceType != NetworkInterfaceType.Loopback &&
                    ni.NetworkInterfaceType != NetworkInterfaceType.Tunnel)
                {
                    var stats = ni.GetIPv4Statistics();
                    currentReceived += stats.BytesReceived;
                    currentSent += stats.BytesSent;
                }
            }
        }
        catch
        {
            // Handle offline/disabled gracefully
            return new NetworkMetrics { DownloadMbps = 0, UploadMbps = 0 };
        }

        var now = DateTime.UtcNow;

        if (_isFirstPoll)
        {
            _lastBytesReceived = currentReceived;
            _lastBytesSent = currentSent;
            _lastTime = now;
            _isFirstPoll = false;
            return new NetworkMetrics { DownloadMbps = 0, UploadMbps = 0 };
        }

        var timeDeltaSeconds = (now - _lastTime).TotalSeconds;
        if (timeDeltaSeconds <= 0) timeDeltaSeconds = 1; // Prevent div by 0

        var receivedDelta = currentReceived - _lastBytesReceived;
        var sentDelta = currentSent - _lastBytesSent;

        // In case counters wrap or interfaces change, preventing negative values
        if (receivedDelta < 0) receivedDelta = 0;
        if (sentDelta < 0) sentDelta = 0;

        _lastBytesReceived = currentReceived;
        _lastBytesSent = currentSent;
        _lastTime = now;

        // Convert bytes to bits (* 8), then to Megabits (/ 1,000,000)
        double downloadMbps = (receivedDelta / timeDeltaSeconds) * 8 / 1_000_000.0;
        double uploadMbps = (sentDelta / timeDeltaSeconds) * 8 / 1_000_000.0;

        return new NetworkMetrics 
        { 
            DownloadMbps = downloadMbps, 
            UploadMbps = uploadMbps 
        };
    }
}
