using Newtonsoft.Json;
using System;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

/*
    TelemetryLocalServer.cs

    Lightweight local HTTP server for status and debug access.

    Responsibilities:
    - Hosts a local listener on the configured port
    - Exposes JSON endpoints for app/runtime status
    - Returns current Mumble/telemetry snapshot data
    - Adds permissive CORS headers for browser/web access
    - Supports:
        /
        /status
        /mumble
    - Mirrors TelemetryRuntime state without owning telemetry logic
*/

namespace GW2Telemetry
{
    internal sealed class TelemetryLocalServer
    {
        private readonly int _port;
        private HttpListener? _listener;
        private CancellationTokenSource? _cts;
        private Task? _serverTask;

        public TelemetryLocalServer(int port)
        {
            _port = port;
        }

        public void Start()
        {
            if (_serverTask != null && !_serverTask.IsCompleted)
                return;

            _cts = new CancellationTokenSource();
            _listener = new HttpListener();
            _listener.Prefixes.Add($"http://localhost:{_port}/");
            _listener.Start();

            _serverTask = Task.Run(() => ListenLoopAsync(_cts.Token));
        }

        public async Task StopAsync()
        {
            try
            {
                _cts?.Cancel();
            }
            catch
            {
            }

            try
            {
                _listener?.Stop();
                _listener?.Close();
            }
            catch
            {
            }

            if (_serverTask != null)
            {
                try
                {
                    await _serverTask.ConfigureAwait(false);
                }
                catch
                {
                }
            }

            _serverTask = null;
            _listener = null;
            _cts = null;
        }

        private async Task ListenLoopAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested && _listener != null && _listener.IsListening)
            {
                HttpListenerContext? context = null;

                try
                {
                    context = await _listener.GetContextAsync().ConfigureAwait(false);
                }
                catch
                {
                    if (token.IsCancellationRequested)
                        break;

                    continue;
                }

                _ = Task.Run(() => HandleRequestAsync(context), token);
            }
        }

        private async Task HandleRequestAsync(HttpListenerContext context)
        {
            try
            {
                AddCorsHeaders(context.Response);

                if (context.Request.HttpMethod.Equals("OPTIONS", StringComparison.OrdinalIgnoreCase))
                {
                    context.Response.StatusCode = 204;
                    context.Response.Close();
                    return;
                }

                string path = context.Request.Url?.AbsolutePath?.TrimEnd('/').ToLowerInvariant() ?? "/";
                if (string.IsNullOrWhiteSpace(path))
                    path = "/";

                object payload = path switch
                {
                    "/" => BuildRootPayload(),
                    "/status" => BuildStatusPayload(),
                    "/mumble" => BuildMumblePayload(),
                    _ => new { error = "Not found" }
                };

                context.Response.StatusCode = path is "/" or "/status" or "/mumble" ? 200 : 404;

                string json = JsonConvert.SerializeObject(payload, Formatting.Indented);
                byte[] bytes = Encoding.UTF8.GetBytes(json);

                context.Response.ContentType = "application/json; charset=utf-8";
                context.Response.ContentLength64 = bytes.Length;

                await context.Response.OutputStream.WriteAsync(bytes, 0, bytes.Length).ConfigureAwait(false);
                context.Response.Close();
            }
            catch
            {
                try
                {
                    context.Response.StatusCode = 500;
                    context.Response.Close();
                }
                catch
                {
                }
            }
        }

        private static void AddCorsHeaders(HttpListenerResponse response)
        {
            response.Headers["Access-Control-Allow-Origin"] = "*";
            response.Headers["Access-Control-Allow-Methods"] = "GET, OPTIONS";
            response.Headers["Access-Control-Allow-Headers"] = "Content-Type";
            response.Headers["Cache-Control"] = "no-store, no-cache, must-revalidate";
        }

        private static object BuildRootPayload()
        {
            return new
            {
                app = "GW2Telemetry",
                version = "1.2.0",
                endpoints = new[]
                {
                    "/",
                    "/status",
                    "/mumble"
                }
            };
        }

        private static object BuildStatusPayload()
        {
            var state = TelemetryRuntime.GetState();

            return new
            {
                gameRunning = state.IsGameRunning,
                mumbleAvailable = state.IsMumbleAvailable,
                telemetryReady = state.IsTelemetryReady,
                failureReason = state.FailureReason,
                lastUpdateUtc = state.LastUpdateUtc
            };
        }

        private static object BuildMumblePayload()
        {
            var state = TelemetryRuntime.GetState();
            var snapshot = state.Snapshot;

            if (!state.IsMumbleAvailable || snapshot == null)
            {
                return new
                {
                    available = false,
                    gameRunning = state.IsGameRunning,
                    telemetryReady = false,
                    failureReason = state.FailureReason,
                    lastUpdateUtc = state.LastUpdateUtc
                };
            }

            return new
            {
                available = true,
                gameRunning = state.IsGameRunning,
                telemetryReady = state.IsTelemetryReady,
                failureReason = state.FailureReason,
                lastUpdateUtc = state.LastUpdateUtc,

                mapId = snapshot.MapId,

                position = new
                {
                    x = snapshot.PlayerX,
                    y = snapshot.PlayerY,
                    z = snapshot.PlayerZ
                },

                cameraPosition = new
                {
                    x = snapshot.CameraX,
                    y = snapshot.CameraY,
                    z = snapshot.CameraZ
                },

                cameraFront = new
                {
                    x = snapshot.CameraFrontX,
                    y = snapshot.CameraFrontY,
                    z = snapshot.CameraFrontZ
                },

                cameraTop = new
                {
                    x = snapshot.CameraTopX,
                    y = snapshot.CameraTopY,
                    z = snapshot.CameraTopZ
                },

                fov = snapshot.Fov,
                uiSize = snapshot.UiSize,
                identity = snapshot.RawIdentity,
                name = snapshot.CharacterName,
                profession = snapshot.Profession,
                race = snapshot.Race
            };
        }
    }
}