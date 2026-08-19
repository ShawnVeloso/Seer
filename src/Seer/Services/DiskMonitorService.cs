using System;
using System.Diagnostics;
using Seer.Models;

namespace Seer.Services;

public sealed class DiskMonitorService : IDisposable
{
    private PerformanceCounter? _readCounter;
    private PerformanceCounter? _writeCounter;
    private bool _isInitialized;
    private bool _isUnavailable;

    public void Initialize()
    {
        if (_isInitialized || _isUnavailable) return;

        try
        {
            // _Total instance aggregates across all physical disks
            _readCounter = new PerformanceCounter("PhysicalDisk", "Disk Read Bytes/sec", "_Total");
            _writeCounter = new PerformanceCounter("PhysicalDisk", "Disk Write Bytes/sec", "_Total");
            
            // Call NextValue() once to initialize the counters
            _readCounter.NextValue();
            _writeCounter.NextValue();
            
            _isInitialized = true;
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException || ex is InvalidOperationException || ex is PlatformNotSupportedException)
        {
            _isUnavailable = true;
            Dispose();
        }
    }

    public DiskMetrics GetMetrics()
    {
        Initialize();

        if (_isUnavailable || _readCounter == null || _writeCounter == null)
        {
            return new DiskMetrics { ReadBytesPerSec = 0, WriteBytesPerSec = 0 };
        }

        try
        {
            return new DiskMetrics
            {
                ReadBytesPerSec = _readCounter.NextValue(),
                WriteBytesPerSec = _writeCounter.NextValue()
            };
        }
        catch (Exception ex) when (ex is InvalidOperationException || ex is UnauthorizedAccessException)
        {
            _isUnavailable = true;
            Dispose();
            return new DiskMetrics { ReadBytesPerSec = 0, WriteBytesPerSec = 0 };
        }
    }

    public void Dispose()
    {
        _readCounter?.Dispose();
        _writeCounter?.Dispose();
        _readCounter = null;
        _writeCounter = null;
        _isInitialized = false;
    }
}
