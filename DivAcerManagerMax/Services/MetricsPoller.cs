using System;
using System.Threading;
using System.Threading.Tasks;

namespace DivAcerManagerMax.Services;

/// <summary>
///     Single source of live hardware telemetry for the whole GUI.
///     One background read per interval is shared by the dashboard, the battery
///     page, diagnostics and the fan-curve engine instead of every timer
///     spawning its own /proc reads and nvidia-smi processes.
/// </summary>
public static class MetricsPoller
{
    private const int VisibleIntervalSeconds = 2;
    private const int BackgroundIntervalSeconds = 15;

    private static readonly object Sync = new();
    private static Timer? _timer;
    private static SystemSnapshot? _latest;
    private static int _intervalSeconds = VisibleIntervalSeconds;
    private static bool _busy;

    /// <summary>Most recent successful snapshot (may be null before the first tick).</summary>
    public static SystemSnapshot? Latest
    {
        get
        {
            lock (Sync)
            {
                return _latest;
            }
        }
    }

    /// <summary>Raised on a thread-pool thread after every successful read.</summary>
    public static event Action<SystemSnapshot>? Updated;

    public static void Start()
    {
        SetInterval(_intervalSeconds);
    }

    public static void Stop()
    {
        lock (Sync)
        {
            _timer?.Dispose();
            _timer = null;
        }
    }

    /// <summary>
    ///     Switch between the active interval (2 s) and the background interval
    ///     (15 s) when the window is hidden and no fan curve needs sampling.
    /// </summary>
    public static void SetVisible(bool visible, bool curveActive)
    {
        SetInterval(visible || curveActive ? VisibleIntervalSeconds : BackgroundIntervalSeconds);
    }

    public static void SetInterval(int seconds)
    {
        if (seconds < 1) seconds = 1;

        lock (Sync)
        {
            if (_timer != null && _intervalSeconds == seconds) return;
            _intervalSeconds = seconds;
            _timer?.Dispose();
            _timer = new Timer(OnTick, null, TimeSpan.FromMilliseconds(250), TimeSpan.FromSeconds(seconds));
        }
    }

    /// <summary>Run one read immediately (diagnostics refresh, page switches).</summary>
    public static async Task<SystemSnapshot> RefreshNowAsync()
    {
        var snapshot = await Task.Run(MetricsService.Read).ConfigureAwait(false);
        lock (Sync)
        {
            _latest = snapshot;
        }

        Updated?.Invoke(snapshot);
        return snapshot;
    }

    private static async void OnTick(object? state)
    {
        lock (Sync)
        {
            if (_busy) return;
            _busy = true;
        }

        try
        {
            var snapshot = await Task.Run(MetricsService.Read).ConfigureAwait(false);
            lock (Sync)
            {
                _latest = snapshot;
            }

            Updated?.Invoke(snapshot);
        }
        catch
        {
            // Keep the previous snapshot; the next tick retries.
        }
        finally
        {
            lock (Sync)
            {
                _busy = false;
            }
        }
    }
}
