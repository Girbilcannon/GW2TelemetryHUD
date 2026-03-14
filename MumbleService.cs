using System;
using System.IO.MemoryMappedFiles;
using System.Numerics;
using System.Runtime.InteropServices;
using Newtonsoft.Json.Linq;

namespace GW2Telemetry
{
    /// <summary>
    /// Provides direct raw access to Guild Wars 2 MumbleLink shared memory.
    /// </summary>
    public static class MumbleService
    {
        private const string MumbleLinkName = "MumbleLink";

        [StructLayout(LayoutKind.Sequential)]
        private struct LinkVector3
        {
            public float X;
            public float Y;
            public float Z;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct LinkedMemRaw
        {
            public uint UiVersion;
            public uint UiTick;

            public LinkVector3 AvatarPosition;
            public LinkVector3 AvatarFront;
            public LinkVector3 AvatarTop;

            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
            public string Name;

            public LinkVector3 CameraPosition;
            public LinkVector3 CameraFront;
            public LinkVector3 CameraTop;

            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
            public string Identity;

            public uint ContextLength;

            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 256)]
            public byte[] Context;

            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 2048)]
            public string Description;
        }

        public sealed class MumbleSnapshot
        {
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
            snapshot = null;

            try
            {
                int size = Marshal.SizeOf<LinkedMemRaw>();

                using MemoryMappedFile mmf = MemoryMappedFile.OpenExisting(
                    MumbleLinkName,
                    MemoryMappedFileRights.Read);

                using MemoryMappedViewStream stream = mmf.CreateViewStream(
                    0,
                    size,
                    MemoryMappedFileAccess.Read);

                byte[] buffer = new byte[size];
                int bytesRead = stream.Read(buffer, 0, size);
                if (bytesRead < size)
                    return false;

                LinkedMemRaw raw = ByteArrayToStructure<LinkedMemRaw>(buffer);

                string rawIdentity = (raw.Identity ?? string.Empty).TrimEnd('\0').Trim();

                float? fov = null;
                int? uiSize = null;
                string? characterName = null;
                int? profession = null;
                int? race = null;
                int mapId = 0;

                try
                {
                    if (!string.IsNullOrWhiteSpace(rawIdentity))
                    {
                        JObject ident = JObject.Parse(rawIdentity);

                        fov = ident["fov"]?.Value<float?>();
                        uiSize = ident["uisz"]?.Value<int?>();

                        characterName = ident["name"]?.Value<string>();
                        profession = ident["profession"]?.Value<int?>();
                        race = ident["race"]?.Value<int?>();

                        mapId = ident["map_id"]?.Value<int?>()
                             ?? ident["map"]?.Value<int?>()
                             ?? 0;
                    }
                }
                catch
                {
                    // Identity parsing is best-effort only.
                }

                if (mapId == 0)
                    return false;

                Vector3 front = new Vector3(
                    raw.CameraFront.X,
                    raw.CameraFront.Y,
                    raw.CameraFront.Z
                );

                if (front.LengthSquared() < 0.000001f)
                    return false;

                front = Vector3.Normalize(front);

                Vector3 top = new Vector3(
                    raw.CameraTop.X,
                    raw.CameraTop.Y,
                    raw.CameraTop.Z
                );

                if (top.LengthSquared() < 0.000001f)
                {
                    Vector3 worldUp = new Vector3(0f, 1f, 0f);

                    if (MathF.Abs(Vector3.Dot(front, worldUp)) > 0.98f)
                        worldUp = new Vector3(1f, 0f, 0f);

                    Vector3 right = Vector3.Cross(worldUp, front);

                    if (right.LengthSquared() < 0.000001f)
                        right = new Vector3(1f, 0f, 0f);
                    else
                        right = Vector3.Normalize(right);

                    top = Vector3.Cross(front, right);

                    if (top.LengthSquared() < 0.000001f)
                        top = new Vector3(0f, 1f, 0f);
                    else
                        top = Vector3.Normalize(top);
                }
                else
                {
                    top = Vector3.Normalize(top);
                }

                snapshot = new MumbleSnapshot
                {
                    MapId = mapId,

                    PlayerX = raw.AvatarPosition.X,
                    PlayerY = raw.AvatarPosition.Y,
                    PlayerZ = raw.AvatarPosition.Z,

                    CameraX = raw.CameraPosition.X,
                    CameraY = raw.CameraPosition.Y,
                    CameraZ = raw.CameraPosition.Z,

                    CameraFrontX = front.X,
                    CameraFrontY = front.Y,
                    CameraFrontZ = front.Z,

                    CameraTopX = top.X,
                    CameraTopY = top.Y,
                    CameraTopZ = top.Z,

                    RawIdentity = rawIdentity,
                    Fov = fov,
                    UiSize = uiSize,
                    CharacterName = characterName,
                    Profession = profession,
                    Race = race
                };

                return true;
            }
            catch
            {
                return false;
            }
        }

        public static bool TryGet(out int mapId, out float x, out float y, out float z)
        {
            mapId = 0;
            x = y = z = 0;

            if (!TryGetSnapshot(out var snapshot) || snapshot == null)
                return false;

            mapId = snapshot.MapId;
            x = snapshot.PlayerX;
            y = snapshot.PlayerY;
            z = snapshot.PlayerZ;

            return true;
        }

        private static T ByteArrayToStructure<T>(byte[] bytes) where T : struct
        {
            IntPtr ptr = Marshal.AllocHGlobal(bytes.Length);

            try
            {
                Marshal.Copy(bytes, 0, ptr, bytes.Length);
                return Marshal.PtrToStructure<T>(ptr);
            }
            finally
            {
                Marshal.FreeHGlobal(ptr);
            }
        }
    }
}