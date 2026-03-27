/*
    TelemetryConfig.cs

    Settings model for GW2Telemetry v1.2.0.

    Responsibilities:
    - Stores the selected outgoing transport type
    - Stores transport-specific settings for MQTT and UDP
    - Stores shared publish interval, event code, and color settings
    - Keeps local JSON server settings available regardless of transport mode
    - Preserves legacy MQTT fields for migration from older configs
*/

namespace GW2Telemetry
{
    public class TelemetryConfig
    {
        public const string ServerTypeMqtt = "MQTT";
        public const string ServerTypeUdp = "UDP";
        public const string ServerTypeJsonOnly = "JSON Output Only";

        /// Selected outgoing transport mode.
        public string ServerType { get; set; } = ServerTypeUdp;

        /// Shared event/session code.
        public string EventCode { get; set; } = "";

        /// Minimum interval between telemetry updates (ms).
        public int PublishIntervalMs { get; set; } = 500;

        /// Player color (used by viewer for marker color).
        public int Color { get; set; } = 65280;

        /// Local localhost JSON server port.
        public int LocalServerPort { get; set; } = 61338;

        /// MQTT broker hostname/IP.
        public string MqttBroker { get; set; } = "www.beetlerank.com";

        /// MQTT port.
        public int MqttPort { get; set; } = 1883;

        /// Base MQTT topic (legacy / fallback).
        public string MqttTopic { get; set; } = "gw2/players/position";

        /// UDP server hostname/IP.
        public string UdpHost { get; set; } = "www.beetlerank.com";

        /// UDP port.
        public int UdpPort { get; set; } = 41234;

        // Legacy v1.1.x fields kept for config migration.
        public string? Broker { get; set; }
        public int? Port { get; set; }
        public string? Topic { get; set; }

        public string NormalizedServerType
        {
            get
            {
                if (string.Equals(ServerType, ServerTypeMqtt, System.StringComparison.OrdinalIgnoreCase))
                    return ServerTypeMqtt;

                if (string.Equals(ServerType, ServerTypeJsonOnly, System.StringComparison.OrdinalIgnoreCase))
                    return ServerTypeJsonOnly;

                return ServerTypeUdp;
            }
        }

        public bool IsMqttSelected =>
            string.Equals(NormalizedServerType, ServerTypeMqtt, System.StringComparison.OrdinalIgnoreCase);

        public bool IsUdpSelected =>
            string.Equals(NormalizedServerType, ServerTypeUdp, System.StringComparison.OrdinalIgnoreCase);

        public bool IsJsonOnlySelected =>
            string.Equals(NormalizedServerType, ServerTypeJsonOnly, System.StringComparison.OrdinalIgnoreCase);

        public string ResolvedMqttTopic
        {
            get
            {
                string baseTopic = (MqttTopic ?? string.Empty).Trim().Trim('/');
                string eventCode = (EventCode ?? string.Empty).Trim().Trim('/');

                if (string.IsNullOrWhiteSpace(baseTopic))
                    return string.Empty;

                return string.IsNullOrWhiteSpace(eventCode)
                    ? "/" + baseTopic
                    : "/" + baseTopic + "/" + eventCode;
            }
        }

        public void NormalizeAfterLoad()
        {
            if (string.IsNullOrWhiteSpace(MqttBroker) && !string.IsNullOrWhiteSpace(Broker))
                MqttBroker = Broker!;

            if (MqttPort <= 0 && Port.HasValue && Port.Value > 0)
                MqttPort = Port.Value;

            if (string.IsNullOrWhiteSpace(MqttTopic) && !string.IsNullOrWhiteSpace(Topic))
                MqttTopic = Topic!;

            if (MqttPort <= 0)
                MqttPort = 1883;

            if (UdpPort <= 0)
                UdpPort = 41234;

            if (LocalServerPort <= 0)
                LocalServerPort = 61338;

            if (PublishIntervalMs < 200)
                PublishIntervalMs = 200;

            if (string.IsNullOrWhiteSpace(ServerType))
            {
                bool hasLegacyMqttData =
                    !string.IsNullOrWhiteSpace(Broker) ||
                    (Port.HasValue && Port.Value > 0) ||
                    !string.IsNullOrWhiteSpace(Topic);

                ServerType = hasLegacyMqttData ? ServerTypeMqtt : ServerTypeUdp;
            }
            else
            {
                ServerType = NormalizedServerType;
            }
        }
    }
}
