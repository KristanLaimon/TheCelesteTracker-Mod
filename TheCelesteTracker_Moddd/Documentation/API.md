# TheCelesteTracker API Documentation

Real-time gameplay events streamed via WebSockets.

## Connection
- **URL:** `ws://localhost:50500/` (or next available port up to 50600)
- **Protocol:** WebSocket (JSON payloads)

### Handshake
The server starts as an HTTP listener. To connect, clients must send a standard WebSocket Upgrade request.

---

## Event Types

### `DatabaseLocation`
**Sent immediately upon connection.**
Provides the full filesystem path to the local SQLite database and version information.
```json
{
  "Type": "DatabaseLocation",
  "Path": "C:\\Program Files (x86)\\Steam\\steamapps\\common\\Celeste\\Saves\\TheCelesteTracker.db",
  "EverestVersion": "1.4532.0",
  "ModVersion": "1.0.0"
}
```

---

## Gameplay Events

### `LevelStart`
Sent when a chapter is loaded.
```json
{
  "Type": "LevelStart",
  "AreaSid": "Celeste/1-ForsakenCity",
  "RoomName": "start",
  "Mode": "Normal"
}
```

### `LevelInfo`
Sent when transitioning between rooms.
```json
{
  "Type": "LevelInfo",
  "AreaSid": "Celeste/1-ForsakenCity",
  "RoomName": "1",
  "Mode": "Normal"
}
```

### `Death`
Sent when Madeline dies.
```json
{
  "Type": "Death",
  "TotalDeaths": 15,
  "RoomDeaths": 2,
  "RoomName": "6a"
}
```

### `Dash`
Sent when Madeline dashes.
```json
{
  "Type": "Dash",
  "TotalDashes": 42
}
```

### `MenuAction`
Sent when exiting a level or changing state.
- **Actions:** `SAVE_AND_QUIT`, `RETURN_TO_MAP`, `MAIN_MENU`
```json
{
  "Type": "MenuAction",
  "Action": "SAVE_AND_QUIT"
}
```

### `AreaComplete`
Sent when a chapter is finished (heart touch/portal).
```json
{
  "Type": "AreaComplete",
  "Stats": {
    "AreaSID": "Celeste/1-ForsakenCity",
    "Mode": "Normal",
    "CompletionTime": "2026-04-11 10:38:34",
    "Screens": 20,
    "TimeTicks": 2062950000,
    "Deaths": 15,
    "DeathsPerScreen": { "1": 0, "2": 1, "6a": 1 },
    "PersonalBestTime": 341870000,
    "PersonalBestDeaths": 2,
    "Golden": false
  }
}
```
