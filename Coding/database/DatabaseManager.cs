using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;

#nullable enable
namespace Celeste.Mod.TheCelesteTracker_Mod.Coding.Database
{
    /// <summary>
    /// Data Transfer Object for run statistics to avoid leaking internal mod logic or SQL types.
    /// </summary>
    public class RunStats
    {
        public string CampaignName { get; set; } = "";
        public string ChapterSID { get; set; } = "";
        public string ChapterName { get; set; } = "";
        public string Mode { get; set; } = "";
        public string? CompletionTime { get; set; }
        public long TimeTicks { get; set; }
        public int Screens { get; set; }
        public int Deaths { get; set; }
        public int Strawberries { get; set; }
        public bool Golden { get; set; }
        public int SaveSlot { get; set; }
        public string SaveName { get; set; } = "";
        public Dictionary<string, int> RoomDeaths { get; set; } = new();
    }

    public static class DatabaseManager
    {
        public static string DbPath => Path.Combine(Everest.PathGame, "Saves", "TheCelesteTracker.db");
        private static string ConnectionString => $"Data Source={DbPath}";

        public static void Init()
        {
            if (File.Exists(DbPath))
            {
                return;
            }

            try
            {
                // ===== Manual DLL loading for .NET 8 Everest compatibility =====
                if (TheCelesteTracker_ModModule.Instance?.Metadata?.PathDirectory != null)
                {
                    string modBinPath = Path.Combine(TheCelesteTracker_ModModule.Instance.Metadata.PathDirectory, "bin");
                    string dllPath = Path.Combine(modBinPath, "e_sqlite3.dll");

                    if (File.Exists(dllPath))
                    {
                        NativeLibrary.Load(dllPath);
                        Logger.Log(LogLevel.Info, "TheCelesteTracker_Mod", $"Manually loaded native SQLite from: {dllPath}");
                    }
                }
                SQLitePCL.Batteries_V2.Init();

                // ======= Init default database =========
                Logger.Log(LogLevel.Info, "TheCelesteTracker_Mod", $"Initializing database at: {DbPath}");
                using (var connection = new SqliteConnection(ConnectionString))
                {
                    connection.Open();
                    var command = connection.CreateCommand();
                    command.CommandText = @"
                        CREATE TABLE IF NOT EXISTS User (
                            id INTEGER PRIMARY KEY AUTOINCREMENT,
                            name TEXT UNIQUE
                        );

                        CREATE TABLE IF NOT EXISTS SaveData (
                            id INTEGER PRIMARY KEY AUTOINCREMENT,
                            user_id INTEGER,
                            slot_number INTEGER,
                            file_name TEXT,
                            FOREIGN KEY(user_id) REFERENCES User(id)
                        );

                        CREATE TABLE IF NOT EXISTS Campaign (
                            id INTEGER PRIMARY KEY AUTOINCREMENT,
                            name TEXT UNIQUE
                        );

                        CREATE TABLE IF NOT EXISTS SaveData_Campaign_has (
                            savedata_id INTEGER,
                            campaign_id INTEGER,
                            PRIMARY KEY(savedata_id, campaign_id),
                            FOREIGN KEY(savedata_id) REFERENCES SaveData(id),
                            FOREIGN KEY(campaign_id) REFERENCES Campaign(id)
                        );

                        CREATE TABLE IF NOT EXISTS Chapter (
                            id INTEGER PRIMARY KEY AUTOINCREMENT,
                            campaign_id INTEGER,
                            sid TEXT,
                            name TEXT,
                            mode TEXT,
                            UNIQUE(campaign_id, sid, mode),
                            FOREIGN KEY(campaign_id) REFERENCES Campaign(id)
                        );

                        CREATE TABLE IF NOT EXISTS Run (
                            id INTEGER PRIMARY KEY AUTOINCREMENT,
                            save_id INTEGER,
                            chapter_id INTEGER,
                            completion_time TEXT DEFAULT NULL,
                            time_ticks INTEGER DEFAULT 0,
                            screens INTEGER DEFAULT 0,
                            deaths INTEGER DEFAULT 0,
                            strawberries INTEGER DEFAULT 0,
                            golden INTEGER DEFAULT 0,
                            FOREIGN KEY(save_id) REFERENCES SaveData(id),
                            FOREIGN KEY(chapter_id) REFERENCES Chapter(id)
                        );

                        CREATE TABLE IF NOT EXISTS RoomDeath (
                            id INTEGER PRIMARY KEY AUTOINCREMENT,
                            run_id INTEGER,
                            room_name TEXT,
                            deaths INTEGER,
                            FOREIGN KEY(run_id) REFERENCES Run(id)
                        );
                    ";
                    command.ExecuteNonQuery();

                    // Create default user
                    command.CommandText = "INSERT OR IGNORE INTO User (name) VALUES ('Celeste Climber');";
                    command.ExecuteNonQuery();
                }
            }
            catch (Exception ex)
            {
                Logger.Log(LogLevel.Error, "TheCelesteTracker_Mod", $"Database Init Error: {ex}");
            }
        }

        //public static long SaveRun(RunStats stats)
        //{
        //    try
        //    {
        //        using (var connection = new SqliteConnection(ConnectionString))
        //        {
        //            connection.Open();
        //            using (var transaction = connection.BeginTransaction())
        //            {
        //                long userId = GetOrCreateUser(connection, transaction, "Celeste Climber");
        //                long saveId = GetOrCreateSaveData(connection, transaction, userId, stats.SaveSlot, stats.SaveName);
        //                long campaignId = GetOrCreateCampaign(connection, transaction, stats.CampaignName);
        //                UpdateJunction(connection, transaction, saveId, campaignId);
        //                long chapterId = GetOrCreateChapter(connection, transaction, campaignId, stats.ChapterSID, stats.ChapterName, stats.Mode);
        //                long runId = InsertRun(connection, transaction, saveId, chapterId, stats);
        //                InsertRoomDeaths(connection, transaction, runId, stats.RoomDeaths);

        //                transaction.Commit();
        //                return runId;
        //            }
        //        }
        //    }
        //    catch (Exception ex)
        //    {
        //        Logger.Log(LogLevel.Error, "TheCelesteTracker_Mod", $"Database Save Error: {ex}");
        //        return -1;
        //    }
        //}

        //private static long GetOrCreateUser(SqliteConnection conn, SqliteTransaction trans, string name)
        //{
        //    var cmd = conn.CreateCommand();
        //    cmd.Transaction = trans;
        //    cmd.CommandText = "SELECT id FROM User WHERE name = @name";
        //    cmd.Parameters.AddWithValue("@name", name);
        //    var result = cmd.ExecuteScalar();
        //    if (result != null && result != DBNull.Value) return (long)result;

        //    cmd.CommandText = "INSERT INTO User (name) VALUES (@name); SELECT last_insert_rowid();";
        //    object? insertResult = cmd.ExecuteScalar();
        //    return insertResult is long id ? id : -1;
        //}

        //private static long GetOrCreateSaveData(SqliteConnection conn, SqliteTransaction trans, long userId, int slot, string name)
        //{
        //    var cmd = conn.CreateCommand();
        //    cmd.Transaction = trans;
        //    cmd.CommandText = "SELECT id FROM SaveData WHERE user_id = @uid AND slot_number = @slot";
        //    cmd.Parameters.AddWithValue("@uid", userId);
        //    cmd.Parameters.AddWithValue("@slot", slot);
        //    var result = cmd.ExecuteScalar();
        //    if (result != null && result != DBNull.Value) return (long)result;

        //    cmd.CommandText = "INSERT INTO SaveData (user_id, slot_number, file_name) VALUES (@uid, @slot, @name); SELECT last_insert_rowid();";
        //    cmd.Parameters.AddWithValue("@name", name);
        //    object? insertResult = cmd.ExecuteScalar();
        //    return insertResult is long id ? id : -1;
        //}

        //private static long GetOrCreateCampaign(SqliteConnection conn, SqliteTransaction trans, string name)
        //{
        //    var cmd = conn.CreateCommand();
        //    cmd.Transaction = trans;
        //    cmd.CommandText = "SELECT id FROM Campaign WHERE name = @name";
        //    cmd.Parameters.AddWithValue("@name", name);
        //    var result = cmd.ExecuteScalar();
        //    if (result != null && result != DBNull.Value) return (long)result;

        //    cmd.CommandText = "INSERT INTO Campaign (name) VALUES (@name); SELECT last_insert_rowid();";
        //    object? insertResult = cmd.ExecuteScalar();
        //    return insertResult is long id ? id : -1;
        //}

        //private static void UpdateJunction(SqliteConnection conn, SqliteTransaction trans, long saveId, long campaignId)
        //{
        //    var cmd = conn.CreateCommand();
        //    cmd.Transaction = trans;
        //    cmd.CommandText = "INSERT OR IGNORE INTO SaveData_Campaign_has (savedata_id, campaign_id) VALUES (@sid, @cid)";
        //    cmd.Parameters.AddWithValue("@sid", saveId);
        //    cmd.Parameters.AddWithValue("@cid", campaignId);
        //    cmd.ExecuteNonQuery();
        //}

        //private static long GetOrCreateChapter(SqliteConnection conn, SqliteTransaction trans, long campaignId, string sid, string name, string mode)
        //{
        //    var cmd = conn.CreateCommand();
        //    cmd.Transaction = trans;
        //    cmd.CommandText = "SELECT id FROM Chapter WHERE campaign_id = @cid AND sid = @sid AND mode = @mode";
        //    cmd.Parameters.AddWithValue("@cid", campaignId);
        //    cmd.Parameters.AddWithValue("@sid", sid);
        //    cmd.Parameters.AddWithValue("@mode", mode);
        //    var result = cmd.ExecuteScalar();
        //    if (result != null && result != DBNull.Value) return (long)result;

        //    cmd.CommandText = "INSERT INTO Chapter (campaign_id, sid, name, mode) VALUES (@cid, @sid, @name, @mode); SELECT last_insert_rowid();";
        //    cmd.Parameters.AddWithValue("@name", name ?? (object)DBNull.Value);
        //    object? insertResult = cmd.ExecuteScalar();
        //    return insertResult is long id ? id : -1;
        //}

        //private static long InsertRun(SqliteConnection conn, SqliteTransaction trans, long saveId, long chapterId, RunStats stats)
        //{
        //    var cmd = conn.CreateCommand();
        //    cmd.Transaction = trans;
        //    cmd.CommandText = @"
        //        INSERT INTO Run (save_id, chapter_id, completion_time, time_ticks, screens, deaths, strawberries, golden)
        //        VALUES (@sid, @chid, @time, @ticks, @screens, @deaths, @strawberries, @golden);
        //        SELECT last_insert_rowid();";
        //    cmd.Parameters.AddWithValue("@sid", saveId);
        //    cmd.Parameters.AddWithValue("@chid", chapterId);
        //    cmd.Parameters.AddWithValue("@time", (object?)stats.CompletionTime ?? DBNull.Value);
        //    cmd.Parameters.AddWithValue("@ticks", stats.TimeTicks);
        //    cmd.Parameters.AddWithValue("@screens", stats.Screens);
        //    cmd.Parameters.AddWithValue("@deaths", stats.Deaths);
        //    cmd.Parameters.AddWithValue("@strawberries", stats.Strawberries);
        //    cmd.Parameters.AddWithValue("@golden", stats.Golden ? 1 : 0);
        //    object? insertResult = cmd.ExecuteScalar();
        //    return insertResult is long id ? id : -1;
        //}

        //public static void UpdateRunStats(long runId, RunStats stats)
        //{
        //    try
        //    {
        //        using (var connection = new SqliteConnection(ConnectionString))
        //        {
        //            connection.Open();
        //            using (var transaction = connection.BeginTransaction())
        //            {
        //                var cmd = connection.CreateCommand();
        //                cmd.Transaction = transaction;
        //                cmd.CommandText = @"
        //                    UPDATE Run 
        //                    SET completion_time = @time, 
        //                        time_ticks = @ticks, 
        //                        screens = @screens, 
        //                        deaths = @deaths, 
        //                        strawberries = @strawberries, 
        //                        golden = @golden
        //                    WHERE id = @rid";
        //                cmd.Parameters.AddWithValue("@rid", runId);
        //                cmd.Parameters.AddWithValue("@time", (object?)stats.CompletionTime ?? DBNull.Value);
        //                cmd.Parameters.AddWithValue("@ticks", stats.TimeTicks);
        //                cmd.Parameters.AddWithValue("@screens", stats.Screens);
        //                cmd.Parameters.AddWithValue("@deaths", stats.Deaths);
        //                cmd.Parameters.AddWithValue("@strawberries", stats.Strawberries);
        //                cmd.Parameters.AddWithValue("@golden", stats.Golden ? 1 : 0);
        //                cmd.ExecuteNonQuery();

        //                UpdateRoomDeaths(connection, transaction, runId, stats.RoomDeaths);

        //                transaction.Commit();
        //            }
        //        }
        //    }
        //    catch (Exception ex)
        //    {
        //        Logger.Log(LogLevel.Error, "TheCelesteTracker_Mod", $"Database Update Error: {ex}");
        //    }
        //}

        //public static bool IsSaveSlotPopulated(int slot)
        //{
        //    try
        //    {
        //        using (var connection = new SqliteConnection(ConnectionString))
        //        {
        //            connection.Open();
        //            var cmd = connection.CreateCommand();
        //            cmd.CommandText = "SELECT COUNT(*) FROM SaveData WHERE slot_number = @slot";
        //            cmd.Parameters.AddWithValue("@slot", slot);
        //            var result = cmd.ExecuteScalar();
        //            return result != null && (long)result > 0;
        //        }
        //    }
        //    catch (Exception ex)
        //    {
        //        Logger.Log(LogLevel.Error, "TheCelesteTracker_Mod", $"Database Check Error: {ex}");
        //        return false;
        //    }
        //}

        //public static void PopulateFromVanilla(SaveData vanillaData)
        //{
        //    try
        //    {
        //        using (var connection = new SqliteConnection(ConnectionString))
        //        {
        //            connection.Open();
        //            using (var transaction = connection.BeginTransaction())
        //            {
        //                long userId = GetOrCreateUser(connection, transaction, "Celeste Climber");
        //                long saveId = GetOrCreateSaveData(connection, transaction, userId, vanillaData.FileSlot, vanillaData.Name);

        //                foreach (AreaStats area in vanillaData.Areas)
        //                {
        //                    string campaignName = area.LevelSet;
        //                    long campaignId = GetOrCreateCampaign(connection, transaction, campaignName);
        //                    UpdateJunction(connection, transaction, saveId, campaignId);

        //                    for (int i = 0; i < area.Modes.Length; i++)
        //                    {
        //                        AreaModeStats modeStats = area.Modes[i];
        //                        if (modeStats.TimePlayed <= 0 && modeStats.Deaths <= 0 && modeStats.TotalStrawberries <= 0)
        //                            continue;

        //                        string modeName = ((AreaMode)i).ToString();
        //                        long chapterId = GetOrCreateChapter(connection, transaction, campaignId, area.SID, Dialog.Clean(AreaData.Get(area.ID).Name), modeName);

        //                        // Create a historical run for the total stats
        //                        RunStats histStats = new RunStats
        //                        {
        //                            CompletionTime = "Historical",
        //                            TimeTicks = modeStats.TimePlayed,
        //                            Screens = 0,
        //                            Deaths = modeStats.Deaths,
        //                            Strawberries = modeStats.TotalStrawberries,
        //                            Golden = modeStats.Completed // Simple heuristic: if mode is completed, mark as golden or similar status if needed. 
        //                        };

        //                        InsertRun(connection, transaction, saveId, chapterId, histStats);
        //                    }
        //                }

        //                transaction.Commit();
        //                Logger.Log(LogLevel.Info, "TheCelesteTracker_Mod", $"Populated database from vanilla save slot {vanillaData.FileSlot}");
        //            }
        //        }
        //    }
        //    catch (Exception ex)
        //    {
        //        Logger.Log(LogLevel.Error, "TheCelesteTracker_Mod", $"Database Population Error: {ex}");
        //    }
        //}

        //private static void UpdateRoomDeaths(SqliteConnection conn, SqliteTransaction trans, long runId, Dictionary<string, int> roomDeaths)
        //{
        //    var deleteCmd = conn.CreateCommand();
        //    deleteCmd.Transaction = trans;
        //    deleteCmd.CommandText = "DELETE FROM RoomDeath WHERE run_id = @rid";
        //    deleteCmd.Parameters.AddWithValue("@rid", runId);
        //    deleteCmd.ExecuteNonQuery();

        //    InsertRoomDeaths(conn, trans, runId, roomDeaths);
        //}

        //private static void InsertRoomDeaths(SqliteConnection conn, SqliteTransaction trans, long runId, Dictionary<string, int> roomDeaths)
        //{
        //    foreach (var kvp in roomDeaths)
        //    {
        //        var cmd = conn.CreateCommand();
        //        cmd.Transaction = trans;
        //        cmd.CommandText = "INSERT INTO RoomDeath (run_id, room_name, deaths) VALUES (@rid, @room, @deaths)";
        //        cmd.Parameters.AddWithValue("@rid", runId);
        //        cmd.Parameters.AddWithValue("@room", kvp.Key ?? (object)DBNull.Value);
        //        cmd.Parameters.AddWithValue("@deaths", kvp.Value);
        //        cmd.ExecuteNonQuery();
        //    }
        //}
    }
}