# GW2Telemetry

Lightweight telemetry client for **Guild Wars 2** that publishes real-time player position data to an MQTT server using the game's **MumbleLink** shared memory.

This tool was created for the **2026 Super Adventure Box Tribulation Cup** to enable real-time 3D map visualization of racer positions during the event.

GW2Telemetry runs quietly in the **system tray** and automatically streams position updates while the game is running.

---

## Features

- Reads live position data directly from **Guild Wars 2 MumbleLink**
- Publishes telemetry to an **MQTT broker**
- Automatic start when the application launches
- Runs quietly in the **system tray**
- Only publishes updates when the player moves
- Adjustable publish interval
- Lightweight and portable
- No installation required

---

## Download and Usage

1. Download the latest release from the **Releases** section of this repository.
2. Extract the files anywhere on your computer.
3. Run:

```
GW2Telemetry.exe
```

That's it.

The application will:

- Start automatically
- Connect to the telemetry server
- Begin publishing position updates

No installation or configuration is required for event participants.

---

## Requirements

- Guild Wars 2 must be running
- The player must be fully loaded into a map
- MumbleLink must be available (enabled by default in GW2)

If the game is not running, the application will simply wait until it detects MumbleLink.

---

## How It Works

Guild Wars 2 exposes real-time gameplay information through a shared memory interface called **MumbleLink**.

GW2Telemetry reads this shared memory block and extracts:

- Player position (X, Y, Z)
- Current map ID
- Character name

The client then publishes this data as JSON messages to an MQTT topic.

Example telemetry message:

```json
{
  "x": 123.45,
  "y": 678.90,
  "z": 12.34,
  "mapId": 1234,
  "name": "Example Character",
  "color": 1,
  "timestamp": "2026-04-01T12:00:00Z"
}
```

Updates are only published when the player's position changes.

---

## System Tray Behavior

Closing the window **does not stop telemetry**.

The application continues running in the **system tray**.

Tray menu options:

- **Open** – Reopen the settings window
- **Exit** – Stop telemetry and close the application

---

## Configuration

The telemetry window allows modification of the following settings:

| Setting | Description |
|-------|-------------|
| Broker | MQTT server hostname |
| Port | MQTT server port |
| Topic | MQTT topic used for publishing |
| Publish Interval | Time between update checks |
| Color | Optional identifier for visualizers |

Changes are saved locally.

---

## Developer Information

GW2Telemetry can be used for many Guild Wars 2 visualization projects.

Possible uses include:

- Race tracking
- Live event visualizations
- 3D map overlays
- Spectator dashboards
- Heatmap analysis
- Player movement analytics

Because the data is published through MQTT, it can easily be consumed by:

- Web dashboards
- Node.js applications
- Python scripts
- Game engines
- Visualization tools

---

## MQTT Message Format

Telemetry messages are published as UTF-8 JSON.

| Field | Description |
|------|-------------|
| x | Player X coordinate |
| y | Player Y coordinate |
| z | Player Z coordinate |
| mapId | Current map ID |
| name | Character name |
| color | Optional display identifier |
| timestamp | UTC timestamp |

---

## Privacy Notes

GW2Telemetry only publishes gameplay telemetry.

The application **does not collect**:

- Account information
- Login credentials
- Chat messages
- Personal data
- API keys

The only transmitted data is the player position and character name required for event visualization.

---

## Credits

Created for the **2026 Super Adventure Box Tribulation Cup**.

Special thanks to the Guild Wars 2 community developers who helped test and integrate the telemetry pipeline.

Guild Wars 2 is a registered trademark of **ArenaNet**.

---

## License

This project is released under the MIT License.
