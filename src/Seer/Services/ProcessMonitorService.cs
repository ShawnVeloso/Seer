using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using Seer.Models;

namespace Seer.Services;

public class ProcessMonitorService
{
    private class ProcessState
    {
        public TimeSpan LastTotalProcessorTime { get; set; }
        public DateTime LastCheckTime { get; set; }
    }

    private readonly Dictionary<int, ProcessState> _processStates = new();

    public List<ProcessMetrics> GetTopProcesses(int count = 5)
    {
        var processes = Process.GetProcesses();
        var metricsList = new List<ProcessMetrics>(processes.Length);
        var now = DateTime.UtcNow;
        int processorCount = Environment.ProcessorCount;

        foreach (var p in processes)
        {
            try
            {
                if (p.HasExited) continue;

                double cpuPercent = 0;
                double memoryMb = 0;
                string processName = p.ProcessName;
                int pid = p.Id;

                // Attempt to read Working Set (RAM)
                try
                {
                    memoryMb = p.WorkingSet64 / (1024.0 * 1024.0);
                }
                catch (Win32Exception) { /* Access Denied */ }
                catch (InvalidOperationException) { /* Exited */ }

                // Attempt to read CPU
                try
                {
                    var currentCpuTime = p.TotalProcessorTime;
                    if (_processStates.TryGetValue(pid, out var state))
                    {
                        var cpuUsedMs = (currentCpuTime - state.LastTotalProcessorTime).TotalMilliseconds;
                        var timePassedMs = (now - state.LastCheckTime).TotalMilliseconds;
                        
                        if (timePassedMs > 0)
                        {
                            cpuPercent = (cpuUsedMs / timePassedMs) * 100.0 / processorCount;
                        }
                    }
                    else
                    {
                        state = new ProcessState();
                        _processStates[pid] = state;
                    }

                    state.LastTotalProcessorTime = currentCpuTime;
                    state.LastCheckTime = now;
                }
                catch (Win32Exception)
                {
                    // Access denied. 
                    // Expected for System processes when not running elevated.
                    // Keep tracking empty state to prevent KeyNotFound but leave CPU at 0.
                    if (!_processStates.ContainsKey(pid))
                    {
                        _processStates[pid] = new ProcessState 
                        { 
                            LastTotalProcessorTime = TimeSpan.Zero, 
                            LastCheckTime = now 
                        };
                    }
                }
                catch (InvalidOperationException)
                {
                    // Exited
                }
                
                // Exclude Idle process to reduce noise
                if (processName.Equals("Idle", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                metricsList.Add(new ProcessMetrics
                {
                    Pid = pid,
                    Name = processName,
                    CpuPercent = cpuPercent,
                    WorkingSetMb = memoryMb
                });
            }
            catch (Exception)
            {
                // Fallback for any other process access errors
            }
            finally
            {
                // Critical: Must dispose process to avoid handle leak!
                p.Dispose();
            }
        }

        // Clean up dead processes from state tracking
        var currentPids = new HashSet<int>(metricsList.Select(m => m.Pid));
        var pidsToRemove = _processStates.Keys.Where(k => !currentPids.Contains(k)).ToList();
        foreach (var pid in pidsToRemove)
        {
            _processStates.Remove(pid);
        }

        // Sort by CPU first, then RAM
        return metricsList
            .OrderByDescending(m => m.CpuPercent)
            .ThenByDescending(m => m.WorkingSetMb)
            .Take(count)
            .ToList();
    }
}
