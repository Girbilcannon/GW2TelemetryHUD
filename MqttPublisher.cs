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
        private readonly MqttClientOptions _options;

        private bool _connecting;

        public bool IsConnected => _client.IsConnected;

        public MqttPublisher(TelemetryConfig config)
        {
            _config = config;

            var factory = new MqttFactory();
            _client = factory.CreateMqttClient();

            _options = new MqttClientOptionsBuilder()
                .WithTcpServer(_config.Broker, _config.Port)
                .WithClientId($"gw2telemetry_{Environment.MachineName}")
                .Build();

            _client.DisconnectedAsync += async _ => await AttemptReconnectAsync();
        }

        public string GetEffectiveTopic()
        {
            string baseTopic = (_config.Topic ?? string.Empty).Trim().Trim('/');
            string eventCode = (_config.EventCode ?? string.Empty).Trim().Trim('/');

            if (string.IsNullOrWhiteSpace(baseTopic))
                return string.Empty;

            return string.IsNullOrWhiteSpace(eventCode)
                ? "/" + baseTopic
                : "/" + baseTopic + "/" + eventCode;
        }

        public async Task ConnectAsync()
        {
            if (_client.IsConnected)
                return;

            await AttemptReconnectAsync();
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

            await _client.PublishAsync(message);
        }

        public async Task DisconnectAsync()
        {
            if (_client.IsConnected)
                await _client.DisconnectAsync();
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
                        await _client.ConnectAsync(_options);
                    }
                    catch
                    {
                        await Task.Delay(5000);
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