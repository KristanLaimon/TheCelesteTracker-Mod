using System;
using System.Collections.Generic;

namespace Celeste.Mod.TheCelesteTracker_Mod.Database
{
    public class User
    {
        public int id { get; set; }
        public string name { get; set; } = "";
    }

    public class SaveData
    {
        public int id { get; set; }
        public int user_id { get; set; }
        public int slot_number { get; set; }
        public string file_name { get; set; } = "";
    }

    public class Campaign
    {
        public int id { get; set; }
        public int save_data_id { get; set; }
        public string campaign_name_id { get; set; } = "";
    }

    public class Chapter
    {
        public string sid { get; set; } = "";
        public int campaign_id { get; set; }
        public string name { get; set; } = "";
    }

    public class ChapterRoom
    {
        public string chapter_sid { get; set; } = "";
        public string name { get; set; } = "";
        public int order { get; set; }
        public int strawberries_available { get; set; }
    }

    public class GameSession
    {
        /// <summary>
        /// Unique identifier like 23848-4832-8casfdj-8388dfj
        /// </summary>
        public string id { get; set; } = "";
        public int chapter_side_id { get; set; }
        public DateTime date_time_start { get; set; }
        public long duration_ms { get; set; }
        public bool is_goldenberry_attempt { get; set; }
        public bool is_goldenberry_completed { get; set; }

        /// <summary>
        /// Dictionary mapping room names to their specific session statistics.
        /// </summary>
        public Dictionary<string, GameSessionChapterRoomStats> room_stats { get; set; } = new();

        /// <summary>
        /// Adds a new statistics record for the current room or updates an existing one by incrementing the specified counters.
        /// </summary>
        /// <remarks>
        /// <strong>CRITICAL:</strong> This method returns <c>null</c> if there is no active game session (i.e., <c>TheCelesteTracker_ModModule.SessionRAM.CurrentSession</c> is null).
        /// </remarks>
        /// <param name="currentLevel">The current <see cref="global::Celeste.Level"/> instance containing the session and room identifier.</param>
        /// <param name="incrementDeathCount">If <c>true</c>, increments the room's death counter.</param>
        /// <param name="incrementDashCount">If <c>true</c>, increments the room's dash counter.</param>
        /// <param name="incrementHeartAchievedCount">If <c>true</c>, increments the room's heart achievement counter.</param>
        /// <param name="incrementStrawberryAchieved">If <c>true</c>, increments the room's strawberry achievement counter.</param>
        /// <returns>The updated or newly created <see cref="GameSessionChapterRoomStats"/> instance; or <c>null</c> if no session is active.</returns>
        public GameSessionChapterRoomStats? AddOrUpdateRoomStat(global::Celeste.Level currentLevel, bool incrementDeathCount = false, bool incrementDashCount = false, bool incrementHeartAchievedCount = false, bool incrementStrawberryAchieved = false, bool incrementJumpCount = false)
        {
            // Use currentLevel.Session.Level to get the specific room name (e.g., "a-00").
            string roomName = currentLevel.Session.Level;

            if (room_stats.TryGetValue(roomName, out GameSessionChapterRoomStats? stats))
            {
                if (incrementDeathCount) stats.deaths_in_room++;
                if (incrementDashCount) stats.dashes_in_room++;
                if (incrementHeartAchievedCount) stats.hearts_achieved_in_room++;
                if (incrementStrawberryAchieved) stats.strawberries_achieved_in_room++;
                if (incrementJumpCount) stats.jumps_in_room++;
                return stats;
            }
            else
            {
                var currentSession = TheCelesteTracker_ModModule.SessionRAM.CurrentSession;
                if (currentSession is null)
                    return null;

                GameSessionChapterRoomStats toAdd = new GameSessionChapterRoomStats
                {
                    gamesession_id = currentSession.id,
                    room_name = roomName,
                    visited_order = room_stats.Count + 1, // Store the visit order while playing
                    deaths_in_room = incrementDeathCount ? 1 : 0,
                    dashes_in_room = incrementDashCount ? 1 : 0,
                    hearts_achieved_in_room = incrementHeartAchievedCount ? 1 : 0,
                    strawberries_achieved_in_room = incrementStrawberryAchieved ? 1 : 0
                };

                room_stats.Add(roomName, toAdd);
                return toAdd;
            }
        }
    }

    public class ChapterSide
    {
        public int id { get; set; }
        public string chapter_sid { get; set; } = "";
        public string side_id { get; set; } = "";
        public int berries_available { get; set; }
        public int berries_collected { get; set; }
    }

    public class GameSessionChapterRoomStats
    {
        public string gamesession_id { get; set; } = "";
        public string room_name { get; set; } = "";
        public int visited_order { get; set; }

        public int deaths_in_room { get; set; }
        public int dashes_in_room { get; set; }

        public int jumps_in_room { get; set; }

        public int strawberries_achieved_in_room { get; set; }
        public int hearts_achieved_in_room { get; set; }
    }

}
