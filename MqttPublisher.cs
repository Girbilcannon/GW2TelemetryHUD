using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Protocol;
using System;
using System.Text;
using System.Threading.Tasks;

/*
    MqttPublisher.cs

    MQTT transport layer for GW2Telemetry.

    Responsibilities:
    - Creates and manages the MQTT client connection
    - Connects to the configured broker and port
    - Reconnects automatically if the connection drops
    - Normalizes and returns the effective publish topic
    - Publishes JSON telemetry payloads to the MQTT broker
    - Disconnects cleanly when telemetry stops or the app exits
*/

namespace GW2Telemetry
{
    public sealed class MqttPublisher
    {
        private readonly TelemetryConfig _config;
        private readonly IMqttClient _client;
        private bool _connecting;

        public bool IsConnected => _client.IsConnected;

        public MqttPublisher(TelemetryConfig config)
        {
            _config = config;

            var factory = new MqttFactory();
            _client = factory.CreateMqttClient();

            _client.DisconnectedAsync += async _ => await AttemptReconnectAsync().ConfigureAwait(false);
        }

        public string GetEffectiveTopic()
        {
            return _config.ResolvedMqttTopic;
        }

        public string GetEndpointDisplay()
        {
            string broker = (_config.MqttBroker ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(broker) || _config.MqttPort <= 0)
                return string.Empty;

            return $"{broker}:{_config.MqttPort}";
        }

        public async Task ConnectAsync()
        {
            if (_client.IsConnected)
                return;

            await AttemptReconnectAsync().ConfigureAwait(false);
        }

        public async Task PublishAsync(string payload)
        {
            if (!_client.IsConnected)
                return;

            string topic = GetEffectiveTopic();
            if (string.IsNullOrWhiteSpace(topic))
                return;

            var message = new MqttApplicationMessageBuilder()
                .WithTopic(topic)
                .WithPayload(Encoding.UTF8.GetBytes(payload))
                .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtMostOnce)
                .Build();

            await _client.PublishAsync(message).ConfigureAwait(false);
        }

        public async Task DisconnectAsync()
        {
            if (_client.IsConnected)
                await _client.DisconnectAsync().ConfigureAwait(false);
        }

        private async Task AttemptReconnectAsync()
        {
            if (_connecting)
                return;

            _connecting = true;

            try
            {
                while (!_client.IsConnected)
                {
                    try
                    {
                        var options = new MqttClientOptionsBuilder()
                            .WithTcpServer(_config.MqttBroker, _config.MqttPort)
                            .WithClientId($"gw2telemetry_{Environment.MachineName}")
                            .Build();

                        await _client.ConnectAsync(options).ConfigureAwait(false);
                    }
                    catch
                    {
                        await Task.Delay(5000).ConfigureAwait(false);
                    }
                }
            }
            finally
            {
                _connecting = false;
            }
        }
    }
}
