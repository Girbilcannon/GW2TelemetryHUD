using System;
using System.Threading;
using System.Threading.Tasks;

/*
    TelemetryRuntime.cs

    Single-source runtime state manager for game and Mumble polling.

    Responsibilities:
    - Owns the background polling loop for GW2 process + Mumble state
    - Calls MumbleService to read fresh snapshots
    - Stores the latest runtime state in one central place
    - Tracks:
        game running state
        Mumble availability
        telemetry readiness
        last failure reason
        latest snapshot
        last update time
    - Provides burst refresh support for manual MumbleLink probing
    - Acts as the internal source of truth for UI, worker, and local server
*/

namespace GW2Telemetry
{
    internal sealed class TelemetryRuntimeState
    {
        public bool IsGameRunning { get; init; }
        public bool IsMumbleAvailable { get; init; }
        public bool IsTelemetryReady { get; init; }
        public string FailureReason { get; init; } = string.Empty;
        public MumbleService.MumbleSnapshot? Snapshot { get; init; }
        public DateTime LastUpdateUtc { get; init; }
    }

    internal static class TelemetryRuntime
    {
        private static readonly object _lock = new();

        private static CancellationTokenSource? _cts;
        private static Task? _loopTask;

        private static bool _isGameRunning;
        private static bool _isMumbleAvailable;
        private static bool _isTelemetryReady;
        private static string _failureReason = "Runtime not started.";
        private static MumbleService.MumbleSnapshot? _snapshot;
        private static DateTime _lastUpdateUtc = DateTime.MinValue;

        public static void Start()
        {
            lock (_lock)
            {
                if (_loopTask != null && !_loopTask.IsCompleted)
                    return;

                MumbleService.ResetTickGate();

                _cts = new CancellationTokenSource();
                _loopTask = Task.Run(() => PollLoopAsync(_cts.Token));
            }
        }

        public static async Task StopAsync()
        {
            CancellationTokenSource? cts;
            Task? loopTask;

            lock (_lock)
            {
                cts = _cts;
                loopTask = _loopTask;
                _cts = null;
                _loopTask = null;
            }

            if (cts != null)
                cts.Cancel();

            if (loopTask != null)
            {
                try
                {
                    await loopTask.ConfigureAwait(false);
                }
                catch
                {
                }
            }
        }

        public static TelemetryRuntimeState GetState()
        {
            lock (_lock)
            {
                return new TelemetryRuntimeState
                {
                    IsGameRunning = _isGameRunning,
                    IsMumbleAvailable = _isMumbleAvailable,
                    IsTelemetryReady = _isTelemetryReady,
                    FailureReason = _failureReason,
                    Snapshot = _snapshot,
                    LastUpdateUtc = _lastUpdateUtc
                };
            }
        }

        public static async Task<bool> ForceRefreshBurstAsync(int attempts = 20, int delayMs = 250)
        {
            for (int i = 0; i < attempts; i++)
            {
                if (PollOnce())
                    return true;

                await Task.Delay(delayMs).ConfigureAwait(false);
            }

            return false;
        }

        private static async Task PollLoopAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    PollOnce();
                }
                catch
                {
                }

                try
                {
                    await Task.Delay(250, token).ConfigureAwait(false);
                }
                catch
                {
                }
            }
        }

        private static bool PollOnce()
        {
            bool gameRunning = ProcessHelpers.IsGw2Running();

            if (MumbleService.TryGetSnapshotDetailed(out var snapshot, out string reason) && snapshot != null)
            {
                lock (_lock)
                {
                    _isGameRunning = gameRunning;
                    _isMumbleAvailable = true;
                    _isTelemetryReady = snapshot.IsUsableForTelemetry;
                    _failureReason = reason;
                    _snapshot = snapshot;
                    _lastUpdateUtc = DateTime.UtcNow;
                }

                return snapshot.IsUsableForTelemetry;
            }

            lock (_lock)
            {
                _isGameRunning = gameRunning;
                _isMumbleAvailable = false;
                _isTelemetryReady = false;
                _failureReason = reason;
                _snapshot = null;
                _lastUpdateUtc = DateTime.UtcNow;
            }

            return false;
        }
    }
}