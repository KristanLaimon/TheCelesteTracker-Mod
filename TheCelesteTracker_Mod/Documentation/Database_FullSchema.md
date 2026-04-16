# TheCelesteTracker Database Schema

Relational model for tracking Celeste gameplay statistics.
**Location:** `Saves/TheCelesteTracker.db` (SQLite)

---

## ER Diagram (Simplified)

- **User** (1) <-> (N) **SaveData**
- **SaveData** (N) <-> (M) **Campaign** (via **SaveData_Campaign_has**)
- **Campaign** (1) <-> (N) **Chapter**
- **SaveData** (1) <-> (N) **Run**
- **Chapter** (1) <-> (N) **Run**
- **Run** (1) <-> (N) **RoomDeath**

---

## Tables

### `User`
Multi-user support for the tracker.
| Column | Type | Notes |
| :--- | :--- | :--- |
| `id` | INTEGER | PRIMARY KEY, AUTOINCREMENT |
| `name` | TEXT | UNIQUE |

### `SaveData`
Links statistics to specific Celeste save slots.
| Column | Type | Notes |
| :--- | :--- | :--- |
| `id` | INTEGER | PRIMARY KEY, AUTOINCREMENT |
| `user_id` | INTEGER | FOREIGN KEY (`User.id`) |
| `slot_number` | INTEGER | Celeste save slot (0, 1, 2) |
| `file_name` | TEXT | Name displayed on save file |

### `Campaign`
Tracks vanilla Celeste and mods separately (LevelSets).
| Column | Type | Notes |
| :--- | :--- | :--- |
| `id` | INTEGER | PRIMARY KEY, AUTOINCREMENT |
| `name` | TEXT | UNIQUE (e.g., "Celeste", "SpringCollab2020") |

### `SaveData_Campaign_has`
Junction table for M:N relationship between save files and campaigns.
| Column | Type | Notes |
| :--- | :--- | :--- |
| `savedata_id` | INTEGER | PRIMARY KEY, FOREIGN KEY (`SaveData.id`) |
| `campaign_id` | INTEGER | PRIMARY KEY, FOREIGN KEY (`Campaign.id`) |

### `Chapter`
Individual levels within a campaign.
| Column | Type | Notes |
| :--- | :--- | :--- |
| `id` | INTEGER | PRIMARY KEY, AUTOINCREMENT |
| `campaign_id` | INTEGER | FOREIGN KEY (`Campaign.id`) |
| `sid` | TEXT | String ID of the chapter |
| `name` | TEXT | Translated display name |
| `mode` | TEXT | "Normal" (A), "BSide", "CSide" |

### `Run`
A single attempt or completion of a chapter. Can be incomplete (`completion_time` is NULL).
| Column | Type | Notes |
| :--- | :--- | :--- |
| `id` | INTEGER | PRIMARY KEY, AUTOINCREMENT |
| `save_id` | INTEGER | FOREIGN KEY (`SaveData.id`) |
| `chapter_id` | INTEGER | FOREIGN KEY (`Chapter.id`) |
| `completion_time` | TEXT | ISO8601 string or NULL if unfinished |
| `time_ticks` | INTEGER | Total time spent (raw ticks) |
| `screens` | INTEGER | Number of unique screens entered |
| `deaths` | INTEGER | Total death count |
| `strawberries` | INTEGER | Total strawberries collected |
| `golden` | INTEGER | 1 if golden berry collected, else 0 |

### `RoomDeath`
Per-room death history for a specific run.
| Column | Type | Notes |
| :--- | :--- | :--- |
| `id` | INTEGER | PRIMARY KEY, AUTOINCREMENT |
| `run_id` | INTEGER | FOREIGN KEY (`Run.id`) |
| `room_name` | TEXT | Name/ID of the room |
| `deaths` | INTEGER | Number of deaths in this room |
