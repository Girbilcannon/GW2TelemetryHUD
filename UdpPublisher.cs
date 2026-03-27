using System;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

/*
    UdpPublisher.cs

    UDP transport layer for GW2Telemetry.

    Responsibilities:
    - Creates and manages the UDP socket
    - Sends compact JSON telemetry datagrams to the configured server
    - Exposes a simple endpoint preview for UI / status text
    - Cleans up the socket when telemetry stops or the app exits
*/

namespace GW2Telemetry
{
    public sealed class UdpPublisher : IDisposable
    {
        private readonly TelemetryConfig _config;
        private UdpClient? _client;

        public UdpPublisher(TelemetryConfig config)
        {
            _config = config;
        }

        public string GetEndpointDisplay()
        {
            string host = (_config.UdpHost ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(host) || _config.UdpPort <= 0)
                return string.Empty;

            return $"{host}:{_config.UdpPort}";
        }

        public Task ConnectAsync()
        {
            if (_client == null)
                _client = new UdpClient();

            return Task.CompletedTask;
        }

        public async Task PublishAsync(string payload)
        {
            if (_client == null)
                return;

            string host = (_config.UdpHost ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(host) || _config.UdpPort <= 0 || string.IsNullOrWhiteSpace(payload))
                return;

            byte[] bytes = Encoding.UTF8.GetBytes(payload);
            await _client.SendAsync(bytes, bytes.Length, host, _config.UdpPort).ConfigureAwait(false);
        }

        public Task DisconnectAsync()
        {
            Dispose();
            return Task.CompletedTask;
        }

        public void Dispose()
        {
            try
            {
                _client?.Close();
                _client?.Dispose();
            }
            catch
            {
            }

            _client = null;
        }
    }
}
