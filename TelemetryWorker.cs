using Newtonsoft.Json;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace GW2Telemetry
{
    internal class TelemetryWorker
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

        public bool IsRunning => _loopTask != null && !_loopTask.IsCompleted;

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

            _cts = new CancellationTokenSource();

            _hasLastPublishedPosition = false;
            _lastPublishedName = string.Empty;

            SetStatus("Connecting to MQTT broker...");
            await _publisher.ConnectAsync();
            SetStatus("MQTT connected. Waiting for Guild Wars 2 / MumbleLink...");

            _loopTask = Task.Run(() => TelemetryLoop(_cts.Token));
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

        private async Task TelemetryLoop(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    if (MumbleService.TryGetSnapshot(out var snapshot) && snapshot != null)
                    {
                        string currentName = snapshot.CharacterName ?? string.Empty;

                        bool hasMoved =
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
                                x = snapshot.PlayerX,
                                y = snapshot.PlayerY,
                                z = snapshot.PlayerZ,
                                mapId = snapshot.MapId,
                                name = currentName,
                                color = _config.Color,
                                timestamp = DateTime.UtcNow.ToString("o")
                            };

                            var json = JsonConvert.SerializeObject(payload);

                            await _publisher.PublishAsync(json);

                            _hasLastPublishedPosition = true;
                            _lastPublishedX = snapshot.PlayerX;
                            _lastPublishedY = snapshot.PlayerY;
                            _lastPublishedZ = snapshot.PlayerZ;
                            _lastPublishedMapId = snapshot.MapId;
                            _lastPublishedName = currentName;

                            var displayName = string.IsNullOrWhiteSpace(currentName)
                                ? "(unknown character)"
                                : currentName;

                            SetStatus(
                                $"Publishing telemetry for {displayName} on map {snapshot.MapId} " +
                                $"at X:{snapshot.PlayerX:0.##} Y:{snapshot.PlayerY:0.##} Z:{snapshot.PlayerZ:0.##}"
                            );
                        }
                    }
                    else
                    {
                        SetStatus("MQTT connected. Waiting for Guild Wars 2 / MumbleLink...");
                    }
                }
                catch (Exception ex)
                {
                    SetStatus($"Telemetry error: {ex.Message}");
                }

                try
                {
                    await Task.Delay(_config.PublishIntervalMs, token);
                }
                catch
                {
                    // Shutdown cancellation
                }
            }
        }

        private static bool AreClose(float a, float b)
        {
            return MathF.Abs(a - b) < 0.001f;
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