using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Data.Sqlite;
using Celeste;
using Monocle;

namespace Celeste.Mod.TheCelesteTracker_Mod
{
    public static class DatabaseManager
    {
        private static string DbPath => Path.Combine(Everest.PathGame, "Saves", "TheCelesteTracker.db");
        private static string ConnectionString => $"Data Source={DbPath}";

        public static void Init()
        {
            using (var connection = new SqliteConnection(ConnectionString))
            {
                connection.Open();

                var command = connection.CreateCommand();
                command.CommandText = @"
                    CREATE TABLE IF NOT EXISTS Users (
                        id INTEGER PRIMARY KEY AUTOINCREMENT,
                        name TEXT UNIQUE
                    );

                    CREATE TABLE IF NOT EXISTS SaveData (
                        id INTEGER PRIMARY KEY AUTOINCREMENT,
                        user_id INTEGER,
                        slot_number INTEGER,
                        file_name TEXT,
                        FOREIGN KEY(user_id) REFERENCES Users(id)
                    );

                    CREATE TABLE IF NOT EXISTS Campaigns (
                        id INTEGER PRIMARY KEY AUTOINCREMENT,
                        name TEXT UNIQUE
                    );

                    CREATE TABLE IF NOT EXISTS SaveData_Campaign_Has (
                        save_id INTEGER,
                        campaign_id INTEGER,
                        PRIMARY KEY(save_id, campaign_id),
                        FOREIGN KEY(save_id) REFERENCES SaveData(id),
                        FOREIGN KEY(campaign_id) REFERENCES Campaigns(id)
                    );

                    CREATE TABLE IF NOT EXISTS Chapters (
                        id INTEGER PRIMARY KEY AUTOINCREMENT,
                        campaign_id INTEGER,
                        sid TEXT,
                        name TEXT,
                        mode TEXT,
                        UNIQUE(campaign_id, sid, mode),
                        FOREIGN KEY(campaign_id) REFERENCES Campaigns(id)
                    );

                    CREATE TABLE IF NOT EXISTS Runs (
                        id INTEGER PRIMARY KEY AUTOINCREMENT,
                        save_id INTEGER,
                        chapter_id INTEGER,
                        completion_time TEXT,
                        time_ticks INTEGER,
                        screens INTEGER,
                        deaths INTEGER,
                        strawberries INTEGER,
                        golden INTEGER,
                        FOREIGN KEY(save_id) REFERENCES SaveData(id),
                        FOREIGN KEY(chapter_id) REFERENCES Chapters(id)
                    );

                    CREATE TABLE IF NOT EXISTS RoomDeaths (
                        id INTEGER PRIMARY KEY AUTOINCREMENT,
                        run_id INTEGER,
                        room_name TEXT,
                        deaths INTEGER,
                        FOREIGN KEY(run_id) REFERENCES Runs(id)
                    );
                ";
                command.ExecuteNonQuery();

                // Create default user
                command.CommandText = "INSERT OR IGNORE INTO Users (name) VALUES ('Kristan');";
                command.ExecuteNonQuery();
            }
        }

        public static void SaveRun(Level level, LevelCompletionData data)
        {
            try
            {
                using (var connection = new SqliteConnection(ConnectionString))
                {
                    connection.Open();
                    using (var transaction = connection.BeginTransaction())
                    {
                        // 1. Get User ID
                        long userId = GetOrCreateUser(connection, transaction, "Kristan");

                        // 2. Get SaveData ID
                        var celesteSave = global::Celeste.SaveData.Instance;
                        long saveId = GetOrCreateSaveData(connection, transaction, userId, celesteSave.FileSlot, celesteSave.Name);

                        // 3. Get Campaign ID
                        string levelSetName = level.Session.Area.GetLevelSet();
                        long campaignId = GetOrCreateCampaign(connection, transaction, levelSetName);

                        // 4. Update Junction Table
                        UpdateJunction(connection, transaction, saveId, campaignId);

                        // 5. Get Chapter ID
                        string sid = level.Session.Area.GetSID();
                        string chapterName = Dialog.Clean(level.Session.Area.GetSID());
                        long chapterId = GetOrCreateChapter(connection, transaction, campaignId, sid, chapterName, level.Session.Area.Mode.ToString());

                        // 6. Insert Run
                        long runId = InsertRun(connection, transaction, saveId, chapterId, data);

                        // 7. Insert Room Deaths
                        InsertRoomDeaths(connection, transaction, runId, data.DeathsPerScreen);

                        transaction.Commit();
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Log(LogLevel.Error, "TheCelesteTracker_Mod", $"Database Save Error: {ex}");
            }
        }

        private static long GetOrCreateUser(SqliteConnection conn, SqliteTransaction trans, string name)
        {
            var cmd = conn.CreateCommand();
            cmd.Transaction = trans;
            cmd.CommandText = "SELECT id FROM Users WHERE name = @name";
            cmd.Parameters.AddWithValue("@name", name);
            var result = cmd.ExecuteScalar();
            if (result != null) return (long)result;

            cmd.CommandText = "INSERT INTO Users (name) VALUES (@name); SELECT last_insert_rowid();";
            return (long)cmd.ExecuteScalar();
        }

        private static long GetOrCreateSaveData(SqliteConnection conn, SqliteTransaction trans, long userId, int slot, string name)
        {
            var cmd = conn.CreateCommand();
            cmd.Transaction = trans;
            cmd.CommandText = "SELECT id FROM SaveData WHERE user_id = @uid AND slot_number = @slot";
            cmd.Parameters.AddWithValue("@uid", userId);
            cmd.Parameters.AddWithValue("@slot", slot);
            var result = cmd.ExecuteScalar();
            if (result != null) return (long)result;

            cmd.CommandText = "INSERT INTO SaveData (user_id, slot_number, file_name) VALUES (@uid, @slot, @name); SELECT last_insert_rowid();";
            cmd.Parameters.AddWithValue("@name", name);
            return (long)cmd.ExecuteScalar();
        }

        private static long GetOrCreateCampaign(SqliteConnection conn, SqliteTransaction trans, string name)
        {
            var cmd = conn.CreateCommand();
            cmd.Transaction = trans;
            cmd.CommandText = "SELECT id FROM Campaigns WHERE name = @name";
            cmd.Parameters.AddWithValue("@name", name);
            var result = cmd.ExecuteScalar();
            if (result != null) return (long)result;

            cmd.CommandText = "INSERT INTO Campaigns (name) VALUES (@name); SELECT last_insert_rowid();";
            return (long)cmd.ExecuteScalar();
        }

        private static void UpdateJunction(SqliteConnection conn, SqliteTransaction trans, long saveId, long campaignId)
        {
            var cmd = conn.CreateCommand();
            cmd.Transaction = trans;
            cmd.CommandText = "INSERT OR IGNORE INTO SaveData_Campaign_Has (save_id, campaign_id) VALUES (@sid, @cid)";
            cmd.Parameters.AddWithValue("@sid", saveId);
            cmd.Parameters.AddWithValue("@cid", campaignId);
            cmd.ExecuteNonQuery();
        }

        private static long GetOrCreateChapter(SqliteConnection conn, SqliteTransaction trans, long campaignId, string sid, string name, string mode)
        {
            var cmd = conn.CreateCommand();
            cmd.Transaction = trans;
            cmd.CommandText = "SELECT id FROM Chapters WHERE campaign_id = @cid AND sid = @sid AND mode = @mode";
            cmd.Parameters.AddWithValue("@cid", campaignId);
            cmd.Parameters.AddWithValue("@sid", sid);
            cmd.Parameters.AddWithValue("@mode", mode);
            var result = cmd.ExecuteScalar();
            if (result != null) return (long)result;

            cmd.CommandText = "INSERT INTO Chapters (campaign_id, sid, name, mode) VALUES (@cid, @sid, @name, @mode); SELECT last_insert_rowid();";
            cmd.Parameters.AddWithValue("@name", name);
            return (long)cmd.ExecuteScalar();
        }

        private static long InsertRun(SqliteConnection conn, SqliteTransaction trans, long saveId, long chapterId, LevelCompletionData data)
        {
            var cmd = conn.CreateCommand();
            cmd.Transaction = trans;
            cmd.CommandText = @"
                INSERT INTO Runs (save_id, chapter_id, completion_time, time_ticks, screens, deaths, strawberries, golden)
                VALUES (@sid, @chid, @time, @ticks, @screens, @deaths, @strawberries, @golden);
                SELECT last_insert_rowid();";
            cmd.Parameters.AddWithValue("@sid", saveId);
            cmd.Parameters.AddWithValue("@chid", chapterId);
            cmd.Parameters.AddWithValue("@time", data.CompletionTime);
            cmd.Parameters.AddWithValue("@ticks", data.TimeTicks);
            cmd.Parameters.AddWithValue("@screens", data.Screens);
            cmd.Parameters.AddWithValue("@deaths", data.Deaths);
            cmd.Parameters.AddWithValue("@strawberries", data.Strawberries);
            cmd.Parameters.AddWithValue("@golden", data.Golden ? 1 : 0);
            return (long)cmd.ExecuteScalar();
        }

        private static void InsertRoomDeaths(SqliteConnection conn, SqliteTransaction trans, long runId, Dictionary<string, int> roomDeaths)
        {
            foreach (var kvp in roomDeaths)
            {
                var cmd = conn.CreateCommand();
                cmd.Transaction = trans;
                cmd.CommandText = "INSERT INTO RoomDeaths (run_id, room_name, deaths) VALUES (@rid, @room, @deaths)";
                cmd.Parameters.AddWithValue("@rid", runId);
                cmd.Parameters.AddWithValue("@room", kvp.Key);
                cmd.Parameters.AddWithValue("@deaths", kvp.Value);
                cmd.ExecuteNonQuery();
            }
        }
    }
}