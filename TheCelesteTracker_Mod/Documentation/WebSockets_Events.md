# WebSocket API Reference

The mod hosts a WebSocket server at `ws://localhost:50500` (auto-increments up to `50600` if port is busy) for real-time integration with external trackers and overlays.

---

## Connection Initialization
When a client connects, the server immediately sends a greeting with the database location and version info.

### `DatabaseLocation`
- **Trigger:** Immediate upon connection.
- **Payload:**
    ```json
    {
        "Type": "DatabaseLocation",
        "DatabasePath": "C:\\Games\\Celeste\\Saves\\TheCelesteTracker_DB.db",
        "EverestVersion": "1.4567.0",
        "ModVersion": "1.0.0"
    }
    ```

---

## Global Lifecycle Events

### `ModStarted`
- **Trigger:** When the mod is loaded by Everest.
- **Payload:**
    ```json
    {
        "Type": "ModStarted",
        "DatabasePath": "...",
        "Timestamp": "2026-04-19T..."
    }
    ```

### `GameClosing`
- **Trigger:** During `Everest.OnShutdown` or an unhandled crash.
- **Payload:**
    ```json
    {
        "Type": "GameClosing",
        "IsClosing": true,
        "Reason": "Shutdown" | "Crash",
        "Exception": "..." // Only on Crash
    }
    ```

---

## Session & Level Events

### `SessionStarted`
- **Trigger:** When entering a chapter (A/B/C side).
- **Payload:** Contains the full `SessionRAM` object including current chapter SID, SaveData, and active session ID.

### `RoomEntered`
- **Trigger:** When entering a new screen/room.
- **Payload:**
    ```json
    {
        "Type": "RoomEntered",
        "Room": "a-00",
        "SessionId": "GUID"
    }
    ```

### `SessionExited`
- **Trigger:** When returning to map, saving and quitting, or finishing a chapter.
- **Payload:**
    ```json
    {
        "Type": "SessionExited",
        "SessionId": "GUID"
    }
    ```

---

## Gameplay Events

### `Death`
- **Trigger:** When the player dies.
- **Payload:**
    ```json
    {
        "Type": "Death",
        "TotalDeaths": 42,
        "Room": "a-01"
    }
    ```

### `Jump` / `Dash`
- **Trigger:** On every successful jump or dash.
- **Payload:**
    ```json
    {
        "Type": "Jump",
        "TotalJumps": 150,
        "RoomJumps": 5
    }
    ```

### `StrawberryGrabbed`
- **Trigger:** When the player first touches a strawberry (it starts following).
- **Payload:**
    ```json
    {
        "Type": "StrawberryGrabbed",
        "IsGolden": false,
        "Room": "a-05"
    }
    ```

### `StrawberryCollected`
- **Trigger:** When the strawberry collection animation completes and the count increments.
- **Payload:**
    ```json
    {
        "Type": "StrawberryCollected",
        "IsGolden": false,
        "IsGhost": false, // True if previously collected in this save
        "Room": "a-05"
    }
    ```
