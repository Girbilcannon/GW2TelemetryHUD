using Gw2Sharp;
using Gw2Sharp.Mumble;
using Newtonsoft.Json.Linq;
using System;

/*
    MumbleService.cs

    Low-level MumbleLink reader using Gw2Sharp.

    Responsibilities:
    - Connects to Guild Wars 2 MumbleLink through Gw2Sharp
    - Calls Update() to refresh shared-memory data
    - Reads current player/map/camera/identity values
    - Parses RawIdentity JSON for name, profession, race, FOV, and UI size
    - Uses tick advancement to confirm fresh telemetry data
    - Converts raw Mumble data into a normalized MumbleSnapshot
    - Determines whether the current snapshot is usable for telemetry publishing
*/

namespace GW2Telemetry
{
    public static class MumbleService
    {
        private const string MumbleLinkName = "MumbleLink";

        private static readonly object _readLock = new();
        private static readonly IGw2MumbleClient _client = new Gw2Client().Mumble[MumbleLinkName];

        private static int? _lastTick;

        public sealed class MumbleSnapshot
        {
            public bool IsUsableForTelemetry { get; set; }

            public uint UiVersion { get; set; }
            public uint UiTick { get; set; }

            public int MapId { get; set; }

            public float PlayerX { get; set; }
            public float PlayerY { get; set; }
            public float PlayerZ { get; set; }

            public float CameraX { get; set; }
            public float CameraY { get; set; }
            public float CameraZ { get; set; }

            public float CameraFrontX { get; set; }
            public float CameraFrontY { get; set; }
            public float CameraFrontZ { get; set; }

            public float CameraTopX { get; set; }
            public float CameraTopY { get; set; }
            public float CameraTopZ { get; set; }

            public string RawIdentity { get; set; } = string.Empty;

            public float? Fov { get; set; }
            public int? UiSize { get; set; }

            public string? CharacterName { get; set; }
            public int? Profession { get; set; }
            public int? Race { get; set; }
        }

        public static bool TryGetSnapshot(out MumbleSnapshot? snapshot)
        {
            return TryGetSnapshotDetailed(out snapshot, out _);
        }

        public static bool TryGetSnapshotDetailed(out MumbleSnapshot? snapshot, out string failureReason)
        {
            lock (_readLock)
            {
                snapshot = null;
                failureReason = string.Empty;

                try
                {
                    _client.Update();

                    if (!_client.IsAvailable)
                    {
                        failureReason = "Gw2Sharp Mumble client is not available.";
                        return false;
                    }

                    int mapId = _client.MapId;
                    int currentTick = _client.Tick;
                    string rawIdentity = _client.RawIdentity ?? string.Empty;

                    float? fov = null;
                    int? uiSize = null;
                    string? characterName = null;
                    int? profession = null;
                    int? race = null;

                    if (!string.IsNullOrWhiteSpace(rawIdentity))
                    {
                        try
                        {
                            JObject json = JObject.Parse(rawIdentity);
                            characterName = (string?)json["name"];
                            profession = (int?)json["profession"];
                            race = (int?)json["race"];
                            fov = (float?)json["fov"];
                            uiSize = (int?)json["uisz"];
                        }
                        catch
                        {
                        }
                    }

                    bool tickAdvanced = !_lastTick.HasValue || currentTick != _lastTick.Value;
                    _lastTick = currentTick;

                    float playerX = (float)_client.AvatarPosition.X;
                    float playerY = (float)_client.AvatarPosition.Y;
                    float playerZ = (float)_client.AvatarPosition.Z;

                    bool hasValidPosition =
                        !float.IsNaN(playerX) &&
                        !float.IsNaN(playerY) &&
                        !float.IsNaN(playerZ) &&
                        !float.IsInfinity(playerX) &&
                        !float.IsInfinity(playerY) &&
                        !float.IsInfinity(playerZ);

                    bool usableForTelemetry =
                        mapId > 0 &&
                        tickAdvanced &&
                        hasValidPosition;

                    snapshot = new MumbleSnapshot
                    {
                        IsUsableForTelemetry = usableForTelemetry,
                        UiVersion = 0,
                        UiTick = (uint)currentTick,
                        MapId = mapId,

                        PlayerX = playerX,
                        PlayerY = playerY,
                        PlayerZ = playerZ,

                        CameraX = (float)_client.CameraPosition.X,
                        CameraY = (float)_client.CameraPosition.Y,
                        CameraZ = (float)_client.CameraPosition.Z,

                        CameraFrontX = (float)_client.CameraFront.X,
                        CameraFrontY = (float)_client.CameraFront.Y,
                        CameraFrontZ = (float)_client.CameraFront.Z,

                        CameraTopX = 0f,
                        CameraTopY = 0f,
                        CameraTopZ = 1f,

                        RawIdentity = rawIdentity,
                        Fov = fov,
                        UiSize = uiSize,
                        CharacterName = characterName,
                        Profession = profession,
                        Race = race
                    };

                    failureReason = usableForTelemetry
                        ? string.Empty
                        : $"Gw2Sharp Mumble available, waiting for fresh telemetry (mapId={mapId}, tick={currentTick}, tickAdvanced={tickAdvanced}, pos=({playerX:0.###}, {playerY:0.###}, {playerZ:0.###}))";

                    return true;
                }
                catch (Exception ex)
                {
                    failureReason = $"{ex.GetType().Name}: {ex.Message}";
                    return false;
                }
            }
        }

        public static void ResetTickGate()
        {
            lock (_readLock)
            {
                _lastTick = null;
            }
        }
    }
}