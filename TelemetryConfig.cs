/*
    TelemetryConfig.cs

    Simple settings model for GW2Telemetry.

    Responsibilities:
    - Stores broker, port, topic, and event code settings
    - Stores publish interval and color settings
    - Provides default values for first run / missing config cases
    - Acts as the config object used by UI, worker, and MQTT publisher
*/

namespace GW2Telemetry
{
    public class TelemetryConfig
    {
        /// MQTT broker hostname/IP
        public string Broker { get; set; } = "www.beetlerank.com";

        /// MQTT port
        public int Port { get; set; } = 1883;

        /// Base MQTT topic (legacy / fallback)
        public string Topic { get; set; } = "gw2/players/position";

        /// Event code used to build dynamic race topic
        public string EventCode { get; set; } = "";

        /// Minimum interval between telemetry updates (ms)
        public int PublishIntervalMs { get; set; } = 500;

        /// Player color (used by viewer for marker color)
        public int Color { get; set; } = 65280;

        /// Builds the active topic based on event code
        public string ResolvedTopic
        {
            get
            {
                if (!string.IsNullOrWhiteSpace(EventCode))
                {
                    return $"/gw2/speedometer/race/{EventCode}";
                }

                return Topic;
            }
        }
    }
}