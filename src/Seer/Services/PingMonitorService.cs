using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.NetworkInformation;
using System.Threading;
using System.Threading.Tasks;
using Seer.Models;

namespace Seer.Services;

/// <summary>
/// Measures round-trip latency to a host, on demand.
///
/// Unlike every other reading in Seer, this one is not a passive sensor
/// read — it sends traffic. So it stays off until the user starts it, and
/// it is the only monitor here with a start/stop lifetime.
///
/// It also can't run on the polling timer: a ping takes as long as it
/// takes, and awaiting one on the UI thread would freeze the window for
/// up to the timeout on every unreachable host. Instead a background loop
/// owns the cadence and publishes an immutable
/// <see cref="PingSnapshot"/> that the UI picks up on its own schedule.
/// </summary>
public sealed class PingMonitorService : IDisposable
{
    /// <summary>How long to wait for a reply before counting it lost.</summary>
    private const int TimeoutMs = 1000;

    /// <summary>
    /// Window used for average, min, max and jitter. Rolling rather than
    /// cumulative so the figures track current conditions instead of
    /// being anchored by a spike from ten minutes ago.
    /// </summary>
    private const int WindowSize = 60;

    private readonly object _gate = new();
    private readonly Queue<long> _window = new();

    private CancellationTokenSource? _cts;
    private Task? _loop;

    private string _host = string.Empty;
    private long? _last;
    private int _sent;
    private int _received;
    private string? _lastError;
    private bool _running;
    private bool _disposed;

    /// <summary>Whether the background loop is currently pinging.</summary>
    public bool IsRunning
    {
        get { lock (_gate) return _running; }
    }

    /// <summary>
    /// Starts pinging <paramref name="host"/>. Restarts cleanly if already
    /// running, so changing the host is just another Start. Session
    /// statistics reset, because they describe one host.
    /// </summary>
    public void Start(string host, TimeSpan interval)
    {
        if (_disposed)
            return;

        if (string.IsNullOrWhiteSpace(host))
            return;

        Stop();

        var cts = new CancellationTokenSource();

        lock (_gate)
        {
            _cts = cts;
            _host = host.Trim();
            _window.Clear();
            _last = null;
            _sent = 0;
            _received = 0;
            _lastError = null;
            _running = true;
        }

        _loop = Task.Run(() => RunAsync(_host, interval, cts.Token));
    }

    /// <summary>
    /// Stops pinging. Returns immediately — the loop notices the
    /// cancellation and unwinds on its own, so this is safe to call from
    /// the UI thread without risking a deadlock on an in-flight request.
    /// </summary>
    public void Stop()
    {
        CancellationTokenSource? cts;

        lock (_gate)
        {
            cts = _cts;
            _cts = null;
            _running = false;
        }

        if (cts == null)
            return;

        try
        {
            cts.Cancel();
        }
        catch (ObjectDisposedException)
        {
            // Already torn down; nothing to cancel.
        }

        cts.Dispose();
    }

    /// <summary>Current statistics. Cheap enough to call every poll.</summary>
    public PingSnapshot GetSnapshot()
    {
        lock (_gate)
        {
            if (_window.Count == 0)
            {
                return new PingSnapshot(
                    _running, _host, _last, null, null, null, null,
                    _sent, _received, _lastError);
            }

            var samples = _window.ToArray();

            return new PingSnapshot(
                _running,
                _host,
                _last,
                samples.Average(),
                samples.Min(),
                samples.Max(),
                Jitter(samples),
                _sent,
                _received,
                _lastError);
        }
    }

    /// <summary>Mean absolute change between consecutive replies.</summary>
    private static double? Jitter(long[] samples)
    {
        if (samples.Length < 2)
            return null;

        double total = 0;
        for (var i = 1; i < samples.Length; i++)
            total += Math.Abs(samples[i] - samples[i - 1]);

        return total / (samples.Length - 1);
    }

    private async Task RunAsync(string host, TimeSpan interval, CancellationToken token)
    {
        // One Ping instance for the whole loop: it's reusable, and
        // creating one per request churns sockets needlessly.
        using var ping = new Ping();

        while (!token.IsCancellationRequested)
        {
            try
            {
                var reply = await ping.SendPingAsync(host, TimeoutMs).ConfigureAwait(false);
                Record(reply);
            }
            catch (PingException ex)
            {
                // Unresolvable host, or no route. Expected often enough
                // that it's a reading, not a fault: record it and keep
                // trying, so the panel shows loss climbing rather than
                // silently stopping.
                RecordFailure(ex.InnerException?.Message ?? ex.Message);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                RecordFailure(ex.Message);
            }

            try
            {
                await Task.Delay(interval, token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        lock (_gate)
        {
            _running = false;
        }
    }

    private void Record(PingReply reply)
    {
        lock (_gate)
        {
            _sent++;

            if (reply.Status == IPStatus.Success)
            {
                _received++;
                _last = reply.RoundtripTime;
                _lastError = null;

                _window.Enqueue(reply.RoundtripTime);
                while (_window.Count > WindowSize)
                    _window.Dequeue();
            }
            else
            {
                // A timeout is a lost packet, not an error to display as
                // a failure message — the loss percentage already says it.
                _lastError = reply.Status == IPStatus.TimedOut
                    ? null
                    : reply.Status.ToString();
            }
        }
    }

    private void RecordFailure(string message)
    {
        lock (_gate)
        {
            _sent++;
            _lastError = message;
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        Stop();

        // Give an in-flight request a moment to unwind so the socket is
        // closed tidily, but never block shutdown on a slow network.
        try
        {
            _loop?.Wait(TimeSpan.FromMilliseconds(1500));
        }
        catch
        {
            // A faulted or cancelled loop is fine; we're shutting down.
        }
    }
}
