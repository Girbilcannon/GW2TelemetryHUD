using Newtonsoft.Json;
using System;
using System.IO;

/*
    ConfigManager.cs

    Config file loader and saver for GW2Telemetry.

    Responsibilities:
    - Resolves the application config folder inside AppData
    - Loads config.json into a TelemetryConfig object
    - Returns default config values if the file is missing or invalid
    - Creates the config directory when saving
    - Writes the current TelemetryConfig back to disk as formatted JSON
*/

namespace GW2Telemetry
{
    public static class ConfigManager
    {
        private static readonly string ConfigDirectory =
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "GW2Telemetry"
            );

        private static readonly string ConfigPath =
            Path.Combine(ConfigDirectory, "config.json");

        public static TelemetryConfig Load()
        {
            try
            {
                if (!File.Exists(ConfigPath))
                    return new TelemetryConfig();

                var text = File.ReadAllText(ConfigPath);

                return JsonConvert.DeserializeObject<TelemetryConfig>(text)
                       ?? new TelemetryConfig();
            }
            catch
            {
                return new TelemetryConfig();
            }
        }

        public static void Save(TelemetryConfig config)
        {
            Directory.CreateDirectory(ConfigDirectory);

            var json = JsonConvert.SerializeObject(
                config,
                Newtonsoft.Json.Formatting.Indented
            );

            File.WriteAllText(ConfigPath, json);
        }
    }
}