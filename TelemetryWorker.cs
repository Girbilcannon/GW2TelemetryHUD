using Newtonsoft.Json;
using System;
using System.Threading;
using System.Threading.Tasks;

/*
    TelemetryWorker.cs

    Runtime publishing loop for outgoing telemetry.

    Responsibilities:
    - Connects to the MQTT broker through MqttPublisher
    - Reads current state from MumbleBridgeService / TelemetryRuntime
    - Publishes player position telemetry when data is usable
    - Avoids duplicate publishes when position/name/map have not changed
    - Tracks last published state for movement detection
    - Exposes user-facing status text back to MainForm
    - Enforces the runtime publish interval floor
*/

namespace GW2Telemetry
{
    internal sealed class TelemetryWorker
    {
        private readonly TelemetryConfig _config;
        private readonly MqttPublisher _publisher;
        private readonly Action<string>? _statusCallback;

        private CancellationTokenSource? _cts;
        private Task? _loopTask;
        private string? _lastStatus;

        private bool _hasLastPublishedPosition;
        private float _lastPublishedX;
        private float _lastPublishedY;
        private float _lastPublishedZ;
        private int _lastPublishedMapId;
        private string _lastPublishedName = string.Empty;
        private bool _forceRefresh;

        public bool IsRunning => _loopTask != null && !_loopTask.IsCompleted;
        public bool IsMumbleConnected => MumbleBridgeService.GetState().IsConnected;
        public string EffectiveTopic => _publisher.GetEffectiveTopic();

        public string LastCharacterName { get; private set; } = "-";
        public int LastMapId { get; private set; }
        public string LastPositionText { get; private set; } = "-";
        public string LastMumbleFailureReason { get; private set; } = string.Empty;

        public TelemetryWorker(TelemetryConfig config, Action<string>? statusCallback = null)
        {
            _config = config;
            _publisher = new MqttPublisher(config);
            _statusCallback = statusCallback;
        }

        public async Task StartAsync()
        {
            if (IsRunning)
                return;

            MumbleBridgeService.Start();

            _cts = new CancellationTokenSource();

            _hasLastPublishedPosition = false;
            _lastPublishedName = string.Empty;
            _forceRefresh = true;

            SetStatus("Connecting to MQTT broker...");
            await _publisher.ConnectAsync();
            SetStatus($"MQTT connected. Publishing to {EffectiveTopic}. Waiting for Guild Wars 2 / MumbleLink...");

            _loopTask = Task.Run(() => TelemetryLoopAsync(_cts.Token));
        }

        public async Task StopAsync()
        {
            if (!IsRunning)
                return;

            SetStatus("Stopping telemetry...");

            _cts?.Cancel();

            if (_loopTask != null)
                await _loopTask;

            await _publisher.DisconnectAsync();
            SetStatus("Telemetry stopped.");
        }

        public static bool ProbeMumble()
        {
            return MumbleBridgeService.GetState().IsConnected;
        }

        public static Task<bool> ProbeMumbleBurstAsync()
        {
            MumbleBridgeService.Start();
            return MumbleBridgeService.ForceRefreshBurstAsync();
        }

        private async Task TelemetryLoopAsync(CancellationToken token)
        {
            int publishIntervalMs = Math.Max(200, _config.PublishIntervalMs);

            while (!token.IsCancellationRequested)
            {
                try
                {
                    var bridge = MumbleBridgeService.GetState();
                    var snapshot = bridge.Snapshot;

                    LastMumbleFailureReason = bridge.FailureReason;

                    if (bridge.IsConnected && snapshot != null && snapshot.IsUsableForTelemetry)
                    {
                        string currentName = snapshot.CharacterName ?? string.Empty;

                        LastCharacterName = string.IsNullOrWhiteSpace(currentName) ? "-" : currentName;
                        LastMapId = snapshot.MapId;
                        LastPositionText = $"X:{snapshot.PlayerX:0.##}  Y:{snapshot.PlayerY:0.##}  Z:{snapshot.PlayerZ:0.##}";

                        bool hasMoved =
                            _forceRefresh ||
                            !_hasLastPublishedPosition ||
                            snapshot.MapId != _lastPublishedMapId ||
                            !AreClose(snapshot.PlayerX, _lastPublishedX) ||
                            !AreClose(snapshot.PlayerY, _lastPublishedY) ||
                            !AreClose(snapshot.PlayerZ, _lastPublishedZ) ||
                            !string.Equals(currentName, _lastPublishedName, StringComparison.Ordinal);

                        if (hasMoved)
                        {
                            var payload = new
                            {
                                option = "position",
                                x = snapshot.PlayerX,
                                y = snapshot.PlayerY,
                                z = snapshot.PlayerZ,
                                user = currentName,
                                map = snapshot.MapId,
                                color = IntToHexColor(_config.Color)
                            };

                            string json = JsonConvert.SerializeObject(payload);
                            await _publisher.PublishAsync(json);

                            _hasLastPublishedPosition = true;
                            _lastPublishedX = snapshot.PlayerX;
                            _lastPublishedY = snapshot.PlayerY;
                            _lastPublishedZ = snapshot.PlayerZ;
                            _lastPublishedMapId = snapshot.MapId;
                            _lastPublishedName = currentName;
                            _forceRefresh = false;

                            string displayName = string.IsNullOrWhiteSpace(currentName)
                                ? "(unknown character)"
                                : currentName;

                            SetStatus(
                                $"Publishing telemetry for {displayName} on map {snapshot.MapId} " +
                                $"to {EffectiveTopic} at X:{snapshot.PlayerX:0.##} Y:{snapshot.PlayerY:0.##} Z:{snapshot.PlayerZ:0.##}");
                        }
                    }
                    else if (bridge.IsConnected && snapshot != null)
                    {
                        LastCharacterName = "-";
                        LastMapId = 0;
                        LastPositionText = "-";

                        string suffix = string.IsNullOrWhiteSpace(bridge.FailureReason)
                            ? string.Empty
                            : $" ({bridge.FailureReason})";

                        SetStatus($"MumbleLink detected, waiting for usable telemetry...{suffix}");
                    }
                    else
                    {
                        LastCharacterName = "-";
                        LastMapId = 0;
                        LastPositionText = "-";

                        string suffix = string.IsNullOrWhiteSpace(bridge.FailureReason)
                            ? string.Empty
                            : $" ({bridge.FailureReason})";

                        SetStatus($"MQTT connected. Publishing to {EffectiveTopic}. Waiting for Guild Wars 2 / MumbleLink...{suffix}");
                    }
                }
                catch (Exception ex)
                {
                    SetStatus($"Telemetry error: {ex.Message}");
                }

                try
                {
                    await Task.Delay(publishIntervalMs, token);
                }
                catch
                {
                }
            }
        }

        private static bool AreClose(float a, float b)
        {
            return MathF.Abs(a - b) < 0.001f;
        }

        private static string IntToHexColor(int color)
        {
            int r = (color >> 16) & 0xFF;
            int g = (color >> 8) & 0xFF;
            int b = color & 0xFF;

            return $"#{r:X2}{g:X2}{b:X2}";
        }

        private void SetStatus(string message)
        {
            if (string.Equals(_lastStatus, message, StringComparison.Ordinal))
                return;

            _lastStatus = message;
            _statusCallback?.Invoke(message);
        }
    }
}