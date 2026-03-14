using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Protocol;
using System;
using System.Text;
using System.Threading.Tasks;

namespace GW2Telemetry
{
    public class MqttPublisher
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

            // Trigger reconnect when connection drops
            _client.DisconnectedAsync += async e =>
            {
                await AttemptReconnectAsync();
            };
        }

        /// <summary>
        /// Establish initial connection to the MQTT broker.
        /// </summary>
        public async Task ConnectAsync()
        {
            if (_client.IsConnected)
                return;

            await AttemptReconnectAsync();
        }

        /// <summary>
        /// Attempt to reconnect until successful.
        /// </summary>
        private async Task AttemptReconnectAsync()
        {
            if (_connecting)
                return;

            _connecting = true;

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

            _connecting = false;
        }

        /// <summary>
        /// Publish a telemetry payload to the configured topic.
        /// </summary>
        public async Task PublishAsync(string payload)
        {
            if (!_client.IsConnected)
                return;

            var message = new MqttApplicationMessageBuilder()
                .WithTopic(_config.Topic)
                .WithPayload(Encoding.UTF8.GetBytes(payload))
                .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtMostOnce)
                .Build();

            await _client.PublishAsync(message);
        }

        /// <summary>
        /// Gracefully disconnect from the broker.
        /// </summary>
        public async Task DisconnectAsync()
        {
            if (_client.IsConnected)
            {
                await _client.DisconnectAsync();
            }
        }
    }
}