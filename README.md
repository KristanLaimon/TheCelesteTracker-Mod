# TheCelesteTracker

![TheCelesteTracker](.github/banner.png)

A real-time gameplay tracking mod for **Celeste** (Everest). It monitors your progress, deaths, dashes, and level completions, storing everything in a local SQLite database and streaming events via WebSockets. Works for generic use, but built to feed the [official 'TheCelesteTracker' client](https://github.com/KristanLaimon/TheCelesteTracker-Desktop).

\***_Under development, eventually there will be an official release in gamebanana_**

## Features

- 📊 **SQLite Persistence:** Stores detailed run history, including per-room death counts, without bloating your game save.
- 🌐 **Real-time WebSocket API:** Streams gameplay events (dashes, deaths, transitions) to port `50500` (auto-hunting up to `50600`).
- 🍓 **Berry Tracking:** Tracks strawberry counts per run.
- 🏆 **PB Reference:** Keeps quick access to your Personal Bests.
- 🗺️ **Mod Support:** Automatically identifies different campaigns (LevelSets) and custom maps.

## Installation

1. Download the latest release.
2. Place the `TheCelesteTracker_Mod` folder in your Celeste `Mods` directory.
3. Ensure you have the `Microsoft.Data.Sqlite` and `e_sqlite3.dll` dependencies (bundled in releases).

## Database Schema

The mod uses a relational SQLite database located at `Saves/TheCelesteTracker.db`:

- **Users:** Multi-user support.
- **SaveData:** Links stats to your Celeste save slots.
- **Campaigns:** Tracks mods/vanilla separately.
- **Chapters:** Organized by SID and Mode (A/B/C).
- **Runs:** Individual completion data.
- **RoomDeaths:** Persistent history of where you die.

## Real-time API

Connect to `ws://localhost:50500/` to receive JSON events. See [API.md](./API.md) for the full protocol documentation.

### Example Event (Death):

```json
{
  "Type": "Death",
  "TotalDeaths": 15,
  "RoomDeaths": 2,
  "RoomName": "6a"
}
```

## Developers

### Building

Requirements: .NET 8 SDK.

```bash
dotnet build Source/TheCelesteTracker_Mod.csproj
```

The build script automatically copies the DLLs and assets to the root directory for easy testing.

## License

MIT

