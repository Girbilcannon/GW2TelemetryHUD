using System;
using System.Threading.Tasks;

/*
    MumbleBridgeService.cs

    Compatibility wrapper around TelemetryRuntime.

    Responsibilities:
    - Exposes bridge-style methods expected by the rest of the app
    - Starts and stops TelemetryRuntime
    - Returns a bridge-shaped state object for callers
    - Forwards burst refresh requests
    - Keeps older telemetry code readable while runtime ownership stays centralized
*/

namespace GW2Telemetry
{
    internal sealed class MumbleBridgeState
    {
        public bool IsConnected { get; init; }
        public bool IsGameRunning { get; init; }
        public string FailureReason { get; init; } = string.Empty;
        public MumbleService.MumbleSnapshot? Snapshot { get; init; }
        public DateTime LastUpdateUtc { get; init; }
    }

    internal static class MumbleBridgeService
    {
        public static void Start()
        {
            TelemetryRuntime.Start();
        }

        public static Task StopAsync()
        {
            return TelemetryRuntime.StopAsync();
        }

        public static MumbleBridgeState GetState()
        {
            var state = TelemetryRuntime.GetState();

            return new MumbleBridgeState
            {
                IsConnected = state.IsMumbleAvailable,
                IsGameRunning = state.IsGameRunning,
                FailureReason = state.FailureReason,
                Snapshot = state.Snapshot,
                LastUpdateUtc = state.LastUpdateUtc
            };
        }

        public static Task<bool> ForceRefreshBurstAsync(int attempts = 20, int delayMs = 250)
        {
            return TelemetryRuntime.ForceRefreshBurstAsync(attempts, delayMs);
        }
    }
}