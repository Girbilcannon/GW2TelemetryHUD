namespace GW2Telemetry
{
    public class TelemetryConfig
    {
        /// MQTT broker hostname/IP
        public string Broker { get; set; } = "www.beetlerank.com";

        /// MQTT port
        public int Port { get; set; } = 1883;

        /// MQTT topic
        public string Topic { get; set; } = "gw2/players/position";

        /// Minimum interval between telemetry updates (ms)
        public int PublishIntervalMs { get; set; } = 500;

        /// Player color (used by viewer for marker color)
        public int Color { get; set; } = 65280;
    }
}