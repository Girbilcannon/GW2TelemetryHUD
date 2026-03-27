using Newtonsoft.Json;
using System;
using System.IO;

/*
    ConfigManager.cs

    Config file loader and saver for GW2Telemetry.

    Responsibilities:
    - Resolves the application config folder inside AppData
    - Loads config.json into a TelemetryConfig object
    - Migrates legacy v1.1.x settings into the new v1.2.0 model
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
                {
                    var fresh = new TelemetryConfig();
                    fresh.NormalizeAfterLoad();
                    return fresh;
                }

                var text = File.ReadAllText(ConfigPath);
                var config = JsonConvert.DeserializeObject<TelemetryConfig>(text) ?? new TelemetryConfig();
                config.NormalizeAfterLoad();
                return config;
            }
            catch
            {
                var fallback = new TelemetryConfig();
                fallback.NormalizeAfterLoad();
                return fallback;
            }
        }

        public static void Save(TelemetryConfig config)
        {
            config.NormalizeAfterLoad();
            Directory.CreateDirectory(ConfigDirectory);

            var json = JsonConvert.SerializeObject(
                config,
                Newtonsoft.Json.Formatting.Indented
            );

            File.WriteAllText(ConfigPath, json);
        }
    }
}
